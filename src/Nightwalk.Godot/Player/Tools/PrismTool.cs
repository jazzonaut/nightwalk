using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Tools;
using Nightwalk.Godot.Beacons;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Physics;
using Nightwalk.Godot.Util;
using GodotVector3 = Godot.Vector3;
using SystemVector3 = System.Numerics.Vector3;

namespace Nightwalk.Godot.Player.Tools;

/// <summary>
/// Tool for placing and rotating prisms to route beacon signal beams.
/// Prisms are spawned automatically when activating beacons, but can also be placed manually.
/// </summary>
public sealed partial class PrismTool : BaseTool, IPlaceableTool
{
    /// <inheritdoc/>
    public override string Id => ToolIds.Prism;

    /// <inheritdoc/>
    public override string DisplayName => "Prism";

    /// <inheritdoc/>
    public override bool RequiresUpdate => true;

    /// <inheritdoc/>
    public override bool RequiresInput => true;

    /// <inheritdoc/>
    public float MaxPlacementDistance => Config?.MaxPlacementDistance ?? 50f;

    private PrismSettings? Config => Context.Data.Beacons?.Prism;
    private BeaconSyncManager? SyncManager => Context.BeaconSyncManager;

    // State
    private PrismToolState _state = PrismToolState.Idle;
    private PrismNode? _selectedPrism;

    // Visuals
    private Node3D? _previewPrism;
    private MeshInstance3D? _previewEdges;
    private StandardMaterial3D? _previewMaterial;
    private GodotVector3 _previewPosition;
    private PlacementStatus _placementStatus;

    // Hover state
    private PrismNode? _hoveredPrism;

    // Snap transition
    private const float SnapTransitionDuration = 0.1f;
    private SmoothTransition _snapTransition;
    private GodotVector3 _currentPreviewPos;
    private bool _wasSnapped;

    private enum PrismToolState
    {
        Idle,
        Rotating
    }

    private enum PlacementStatus
    {
        Valid,
        Invalid,
        AtPrismLimit,
        InsufficientScore,
        OutOfRange
    }

    /// <inheritdoc/>
    protected override void OnInitialize()
    {
        CreatePreviewPrism();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _previewMaterial?.Dispose();
        _previewMaterial = null;
    }

    private void CreatePreviewPrism()
    {
        if (Config == null) return;

        _previewMaterial = new StandardMaterial3D
        {
            EmissionEnabled = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        UpdatePreviewMaterial();

        _previewPrism = new Node3D { Name = "PrismPreview" };
        _previewPrism.Visible = false;
        AddChild(_previewPrism);

        // Use shared edge mesh from PrismNode
        _previewEdges = new MeshInstance3D { Name = "PreviewEdges" };
        _previewEdges.Mesh = PrismNode.CreateEdgeMesh(Config.Radius, Config.Length, Config.EdgeThickness);
        _previewEdges.MaterialOverride = _previewMaterial;
        _previewEdges.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _previewPrism.AddChild(_previewEdges);
    }

    private void UpdatePreviewMaterial()
    {
        if (_previewMaterial == null || Config == null)
            return;

        var color = _placementStatus == PlacementStatus.Valid
            ? Color.FromHtml(Config.PreviewColor)
            : Color.FromHtml(Config.InvalidColor);

        _previewMaterial.Emission = color;
        _previewMaterial.EmissionEnergyMultiplier = 2.0f;
        _previewMaterial.AlbedoColor = new Color(color.R, color.G, color.B, 0.7f);
    }

    /// <inheritdoc/>
    public override void Enter()
    {
        base.Enter();
        _state = PrismToolState.Idle;
        _wasSnapped = false;
        _snapTransition.Cancel();
        if (_previewPrism != null) _previewPrism.Visible = true;
        UpdatePreview(0f);
    }

    /// <inheritdoc/>
    public override void Exit()
    {
        base.Exit();
        if (_state == PrismToolState.Rotating)
        {
            _state = PrismToolState.Idle;
            _selectedPrism = null;
        }
        ClearHoverHighlight();
        if (_previewPrism != null) _previewPrism.Visible = false;
    }

    /// <inheritdoc/>
    public override void Update(float delta)
    {
        if (_state == PrismToolState.Rotating && _selectedPrism != null)
        {
            // Handle rotation during drag
            return;
        }

        UpdatePreview(delta);
    }

    private void UpdatePreview(float delta)
    {
        ClearHoverHighlight();

        if (_previewPrism == null || Config == null)
            return;

        // Don't show preview or check hover in POV mode
        if (Context.Player.IsInPrismPOV)
        {
            _previewPrism.Visible = false;
            return;
        }

        // Check for hover over existing prisms
        CheckForHoveredPrism();

        // If hovering over a prism, don't show placement preview
        if (_hoveredPrism != null)
        {
            _previewPrism.Visible = false;
            return;
        }

        var result = PerformPlacementRaycast();

        if (result.HasValue)
        {
            _previewPosition = result.Value.Position;

            // Determine offset based on surface type:
            // Wall (mostly horizontal normal) -> offset along normal
            // Floor/ceiling (mostly vertical normal) -> offset upward
            var normal = result.Value.Normal;
            var isWall = Mathf.Abs(normal.Y) < 0.5f; // Normal within ~60 degrees of horizontal
            GodotVector3 rawPos;
            if (isWall)
            {
                rawPos = _previewPosition + normal * Config.WallOffset;
            }
            else
            {
                rawPos = _previewPosition + GodotVector3.Up * Config.SpawnHeight;
            }

            // Try to snap to nearby beam
            var snappedPos = TrySnapToBeam(rawPos, Config.PlacementSnapThreshold);
            bool isSnapped = snappedPos.HasValue;
            var targetPos = isSnapped ? snappedPos!.Value : rawPos;

            // Start smooth transition when snap state changes
            if (isSnapped != _wasSnapped)
            {
                _snapTransition.Begin(_currentPreviewPos, targetPos, SnapTransitionDuration);
                _wasSnapped = isSnapped;
            }

            // Step transition or use target directly
            if (_snapTransition.TryStepPosition(delta, out var interpolatedPos))
            {
                _currentPreviewPos = interpolatedPos;
            }
            else
            {
                _currentPreviewPos = targetPos;
            }

            _previewPrism.GlobalPosition = _currentPreviewPos;
            _previewPrism.Visible = true;

            // Rotate preview along beam direction if near beam, otherwise towards player
            RotatePreviewAlongBeam(_currentPreviewPos);

            _placementStatus = GetPlacementStatus(_previewPosition);
            UpdatePreviewMaterial();
        }
        else
        {
            // No hit - try to snap to beam along the camera ray
            var camera = Context.Camera;
            var forward = -camera.GlobalTransform.Basis.Z;
            var snappedPos = TrySnapToBeamAlongRay(camera.GlobalPosition, forward, MaxPlacementDistance, Config.PlacementSnapThreshold);

            if (snappedPos.HasValue)
            {
                // Snap preview directly to the beam line in open space.
                var targetPos = snappedPos.Value;

                if (!_wasSnapped)
                {
                    _snapTransition.Begin(_currentPreviewPos, targetPos, SnapTransitionDuration);
                    _wasSnapped = true;
                }

                if (_snapTransition.TryStepPosition(delta, out var interpolatedPos))
                {
                    _currentPreviewPos = interpolatedPos;
                }
                else
                {
                    _currentPreviewPos = targetPos;
                }

                _previewPosition = _currentPreviewPos;
                _previewPrism.GlobalPosition = _currentPreviewPos;
                _previewPrism.Visible = true;

                RotatePreviewAlongBeam(_currentPreviewPos);

                _placementStatus = GetPlacementStatus(_previewPosition);
                UpdatePreviewMaterial();
                return;
            }

            // No beam to snap to - show preview at max range
            _previewPosition = camera.GlobalPosition + forward * MaxPlacementDistance;
            _currentPreviewPos = _previewPosition;
            _previewPrism.GlobalPosition = _previewPosition;
            _previewPrism.Visible = true;
            _wasSnapped = false;
            _snapTransition.Cancel();

            // Rotate preview to face player
            RotatePreviewAwayFromPlayer();

            _placementStatus = PlacementStatus.OutOfRange;
            UpdatePreviewMaterial();
        }
    }

    private GodotVector3? TrySnapToBeam(GodotVector3 position, float threshold)
    {
        if (SyncManager == null || threshold <= 0)
            return null;

        return SyncManager.GetBeamSnapPosition(position, threshold);
    }

    private GodotVector3? TrySnapToBeamAlongRay(GodotVector3 origin, GodotVector3 direction, float maxDistance, float threshold)
    {
        if (SyncManager == null || threshold <= 0)
            return null;

        return SyncManager.GetBeamSnapPositionAlongRay(origin, direction, maxDistance, threshold);
    }

    private void RotatePreviewAlongBeam(GodotVector3 position)
    {
        if (_previewPrism == null || SyncManager == null || Config == null)
            return;

        // Get beam direction at this position
        var beamDir = SyncManager.GetBeamDirectionAt(position, Config.PlacementSnapThreshold);
        if (!beamDir.HasValue || beamDir.Value.LengthSquared() < 0.01f)
        {
            RotatePreviewAwayFromPlayer();
            return;
        }

        // Point the prism along the beam direction
        var target = _previewPrism.GlobalPosition + beamDir.Value;

        // Use Forward as up vector when beam is nearly vertical to avoid colinear warning
        var upVector = Mathf.Abs(beamDir.Value.Dot(GodotVector3.Up)) > 0.99f
            ? GodotVector3.Forward
            : GodotVector3.Up;
        _previewPrism.LookAt(target, upVector);
    }

    // Prism collision layer (layer 16, bit 15)
    private const uint PrismCollisionLayer = 1u << 15;

    private void CheckForHoveredPrism()
    {
        // Don't highlight prisms while in POV mode
        if (SyncManager == null || Context.Player.IsInPrismPOV)
            return;

        var spaceState = GetSpaceState();
        if (spaceState == null)
            return;

        // Raycast only against prism collision layer to avoid hitting other geometry
        var result = RaycastHelper.CameraRaycast(
            Context.Camera,
            spaceState,
            MaxPlacementDistance,
            PrismCollisionLayer
        );

        if (!result.HasValue)
            return;

        // Check if we hit a prism's collider
        var hitPrism = PrismNode.FindFromCollider(result.Value.Collider);
        if (hitPrism != null && SyncManager.ContainsPrism(hitPrism))
        {
            SetHoverHighlight(hitPrism);
        }
    }

    private void SetHoverHighlight(PrismNode prism)
    {
        _hoveredPrism = prism;
        prism.SetHighlighted(true);
    }

    private void ClearHoverHighlight()
    {
        // Check if prism is still part of active sync (removed when sync cancelled)
        if (_hoveredPrism != null && SyncManager?.ContainsPrism(_hoveredPrism) == true)
        {
            _hoveredPrism.SetHighlighted(false);
        }
        _hoveredPrism = null;
    }

    private void RotatePreviewAwayFromPlayer()
    {
        if (_previewPrism == null)
            return;

        var playerPos = Context.Player.GlobalPosition;
        var awayFromPlayer = _previewPrism.GlobalPosition - playerPos;
        awayFromPlayer.Y = 0; // Only rotate horizontally

        if (awayFromPlayer.LengthSquared() < 0.01f)
            return;

        var target = _previewPrism.GlobalPosition + awayFromPlayer.Normalized();
        _previewPrism.LookAt(target, GodotVector3.Up);
    }

    private PhysicsDirectSpaceState3D? GetSpaceState()
    {
        return Context.Player.GetWorld3D()?.DirectSpaceState;
    }

    private RaycastResult? PerformPlacementRaycast()
    {
        var spaceState = GetSpaceState();
        if (spaceState == null)
            return null;

        var exclude = new global::Godot.Collections.Array<Rid>();
        RaycastHelper.CollectCollisionRids(Context.Player, exclude);

        var result = RaycastHelper.CameraRaycastExclude(
            Context.Camera,
            spaceState,
            MaxPlacementDistance,
            exclude
        );

        return result;
    }

    private PlacementStatus GetPlacementStatus(GodotVector3 position)
    {
        if (SyncManager == null)
            return PlacementStatus.Invalid;

        // Check prism limit (0 = unlimited)
        var maxPrisms = SyncManager.MaxPrisms;
        if (maxPrisms > 0 && SyncManager.PrismCount >= maxPrisms)
            return PlacementStatus.AtPrismLimit;

        // Check scaled cost
        if (Config != null && Config.PlacementCost > 0 && Context.ScoreService != null)
        {
            var scaledCost = Context.DifficultyService?.GetPrismPlacementCost(Config.PlacementCost)
                ?? Config.PlacementCost;
            if (scaledCost > 0 && Context.ScoreService.Score < scaledCost)
                return PlacementStatus.InsufficientScore;
        }

        return PlacementStatus.Valid;
    }

    /// <inheritdoc/>
    public bool CanPlaceAt(GodotVector3 position)
    {
        return GetPlacementStatus(position) == PlacementStatus.Valid;
    }

    bool IPlaceableTool.CanPlaceAt(SystemVector3 position)
    {
        return CanPlaceAt(new GodotVector3(position.X, position.Y, position.Z));
    }

    /// <inheritdoc/>
    public override bool HandleInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            return HandleMouseButton(mb);
        }

        if (@event is InputEventMouseMotion mm && _state == PrismToolState.Rotating)
        {
            return HandleMouseMotion(mm);
        }

        return false;
    }

    private bool HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                return HandleLeftMouseDown();
            }
            else
            {
                return HandleLeftMouseUp();
            }
        }

        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            return HandleRightClick();
        }

        return false;
    }

    private bool HandleLeftMouseDown()
    {
        // If hovering a prism, start rotating it
        if (_hoveredPrism != null)
        {
            _state = PrismToolState.Rotating;
            _selectedPrism = _hoveredPrism;
            Log.Debug(LogCategory.Input, "Prism: Started rotating");
            return true;
        }

        // Otherwise, try to place a new prism at the preview position
        if (_placementStatus == PlacementStatus.Valid && SyncManager != null && Config != null)
        {
            // Use the already-calculated preview position (includes wall/floor offset and beam snap)
            var placementPos = _currentPreviewPos;

            // Get orientation - always try beam direction first, fall back to player (horizontal only)
            GodotVector3? faceToward = null;
            var beamDir = SyncManager.GetBeamDirectionAt(placementPos, Config.PlacementSnapThreshold);
            if (beamDir.HasValue)
            {
                faceToward = placementPos + beamDir.Value;
            }
            else
            {
                // Face away from player (exit side opposite of player), stay horizontal
                var toPlayer = Context.Player.GlobalPosition - placementPos;
                toPlayer.Y = 0;
                if (toPlayer.LengthSquared() > 0.01f)
                {
                    faceToward = placementPos - toPlayer; // Opposite direction
                }
            }

            var prism = SyncManager.AddPrism(placementPos, faceToward);
            if (prism != null)
            {
                if (Context.SfxManager != null)
                    Context.SfxManager.PlayGenerated(SfxSynth.PrismPlace());
                Log.Debug(LogCategory.Input, $"Prism: Placed at {placementPos}");
                return true;
            }
        }

        return false;
    }

    private bool HandleLeftMouseUp()
    {
        if (_state == PrismToolState.Rotating)
        {
            _state = PrismToolState.Idle;
            _selectedPrism = null;
            Log.Debug(LogCategory.Input, "Prism: Stopped rotating");

            // Update beam path after rotation
            SyncManager?.UpdateBeamPath();
            return true;
        }

        return false;
    }

    private bool HandleRightClick()
    {
        // Remove hovered prism
        if (_hoveredPrism != null && SyncManager != null)
        {
            SyncManager.RemovePrism(_hoveredPrism);
            ClearHoverHighlight();
            Log.Debug(LogCategory.Input, "Prism: Removed existing prism");
            return true;
        }

        return false;
    }

    private bool HandleMouseMotion(InputEventMouseMotion mm)
    {
        // Only called when _state == PrismToolState.Rotating (checked in HandleInput)
        if (_selectedPrism == null || Config == null || SyncManager == null)
            return false;

        // Check if prism is still registered (might be removed during sync cancel)
        if (!SyncManager.ContainsPrism(_selectedPrism))
        {
            _state = PrismToolState.Idle;
            _selectedPrism = null;
            return false;
        }

        // Rotate prism based on mouse movement
        var sensitivity = Config.RotationSensitivity * 0.01f;
        var yawDelta = -mm.Relative.X * sensitivity;
        var pitchDelta = -mm.Relative.Y * sensitivity;

        _selectedPrism.RotatePrism(yawDelta, pitchDelta);

        // Update tracker and beam path in real-time
        SyncManager?.UpdatePrismRotation(_selectedPrism);
        SyncManager?.UpdateBeamPath();

        return true;
    }

    /// <inheritdoc/>
    public void CancelPlacement()
    {
        if (_state == PrismToolState.Rotating)
        {
            _state = PrismToolState.Idle;
            _selectedPrism = null;
        }

        if (_previewPrism != null && Initialized && Visible)
        {
            _previewPrism.Visible = true;
        }

        Log.Debug(LogCategory.Input, "Prism: Placement cancelled");
    }

    /// <summary>
    /// Reconfigures the tool visuals from current config. Call on hot reload.
    /// </summary>
    public void Reconfigure()
    {
        UpdatePreviewMaterial();
    }
}
