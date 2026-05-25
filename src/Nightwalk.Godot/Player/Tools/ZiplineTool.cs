using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Scoring;
using Nightwalk.Core.Tools;
using Nightwalk.Core.Tools.Events;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Physics;
using Nightwalk.Godot.Ziplines;
using GodotVector3 = Godot.Vector3;
using SystemVector3 = System.Numerics.Vector3;

namespace Nightwalk.Godot.Player.Tools;

/// <summary>
/// Zipline tool for creating traversal lines between two anchor points.
/// </summary>
public sealed partial class ZiplineTool : BaseTool, IPlaceableTool
{
    /// <inheritdoc/>
    public override string Id => ToolIds.Zipline;

    /// <inheritdoc/>
    public override string DisplayName => "Zipline";

    /// <inheritdoc/>
    public override bool RequiresUpdate => true;

    /// <inheritdoc/>
    public override bool RequiresInput => true;

    /// <inheritdoc/>
    public float MaxPlacementDistance =>
        Context.DifficultyService?.GetZiplineMaxDistance(Config.MaxDistance) ?? Config.MaxDistance;

    /// <summary>
    /// The currently hovered zipline cable (for transfer targeting), or null if none.
    /// </summary>
    public ZiplineCable? HoveredCable => _hoveredCable;

    private ZiplineConfig Config => Context.Data.Tools.Zipline;
    private ZiplineState _state = ZiplineState.Idle;

    // Visuals
    private MeshInstance3D? _previewDot;
    private StandardMaterial3D? _previewMaterial;
    private ZiplineAnchor? _anchor1;
    private ZiplineAnchor? _anchor2;
    private ZiplineCable? _cable;

    // Placement data
    private GodotVector3 _anchor1Position;
    private GodotVector3 _anchor1Normal;
    private ulong _anchor1ColliderId;
    private GodotVector3 _previewPosition;
    private GodotVector3 _previewNormal;
    private ulong _previewColliderId;
    private PlacementStatus _placementStatus;
    private float _placementSag;

    // Hover state for removal highlighting
    private ZiplineCable? _hoveredCable;
    private ZiplineAnchor? _hoveredAnchor1;
    private ZiplineAnchor? _hoveredAnchor2;

    private enum PlacementStatus
    {
        Valid,
        Obstructed,
        OutOfRange,
        InsufficientScore
    }

    /// <inheritdoc/>
    protected override void OnInitialize()
    {
        CreatePreviewDot();
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        // Dispose preview material to free GPU resources
        _previewMaterial?.Dispose();
        _previewMaterial = null;
    }

    private void CreatePreviewDot()
    {
        _previewDot = new MeshInstance3D { Name = "PreviewDot" };
        _previewDot.Mesh = new SphereMesh();
        _previewMaterial = new StandardMaterial3D
        {
            EmissionEnabled = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        _previewDot.MaterialOverride = _previewMaterial;
        _previewDot.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _previewDot.Visible = false;

        UpdatePreviewDotVisuals();

        // Add to scene root for world positioning
        AddChild(_previewDot);
    }

    private void UpdatePreviewDotVisuals()
    {
        if (_previewDot?.Mesh is SphereMesh mesh)
        {
            mesh.Radius = Config.PreviewDotRadius;
        }
        if (_previewMaterial != null)
        {
            _previewMaterial.Emission = Color.FromHtml(Config.PreviewColor);
            _previewMaterial.EmissionEnergyMultiplier = Config.PreviewEmission;
            _previewMaterial.AlbedoColor = new Color(1, 1, 1, 0.7f);
        }
    }

    /// <summary>
    /// Reconfigures the tool visuals from current config. Call on hot reload.
    /// </summary>
    public void Reconfigure()
    {
        UpdatePreviewDotVisuals();
    }

    /// <inheritdoc/>
    public override void Enter()
    {
        base.Enter();
        _state = ZiplineState.Idle;
        if (_previewDot != null) _previewDot.Visible = true;
        UpdatePreview();
    }

    /// <inheritdoc/>
    public override void Exit()
    {
        base.Exit();
        // Only cancel if first anchor is placed - we want to discard incomplete placements
        if (_state == ZiplineState.FirstAnchor)
        {
            CancelPlacement();
        }
        ClearHoverHighlight();
        if (_previewDot != null) _previewDot.Visible = false;
    }

    /// <inheritdoc/>
    public override void Update(float delta)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        // Clear previous hover highlight first
        ClearHoverHighlight();

        if (_previewDot == null) return;

        // Check for hover over existing ziplines (priority over placement preview)
        CheckForHoveredZipline();

        // If hovering over a zipline, don't show placement preview
        if (_hoveredCable != null)
        {
            _previewDot.Visible = false;
            return;
        }

        var result = PerformPlacementRaycast();

        if (result.HasValue)
        {
            _previewPosition = result.Value.Position;
            _previewNormal = result.Value.Normal;
            _previewColliderId = result.Value.ColliderId;
            _previewDot.GlobalPosition = _previewPosition;
            _previewDot.Visible = true;

            // Check placement validity after positioning the dot
            _placementStatus = GetPlacementStatus(_previewPosition);
            UpdatePreviewColor();
        }
        else
        {
            // No hit - show dot at max range to indicate out of range
            var camera = Context.Camera;
            var forward = -camera.GlobalTransform.Basis.Z;
            _previewPosition = camera.GlobalPosition + forward * MaxPlacementDistance;
            _previewDot.GlobalPosition = _previewPosition;
            _previewDot.Visible = true;
            _placementStatus = PlacementStatus.OutOfRange;
            _previewNormal = GodotVector3.Zero;
            _previewColliderId = 0;
            UpdatePreviewColor();
        }
    }

    private void CheckForHoveredZipline()
    {
        // Raycast to find where the player is looking
        var spaceState = GetSpaceState();
        if (spaceState == null) return;

        var result = RaycastHelper.CameraRaycast(
            Context.Camera,
            spaceState,
            Config.HighlightDistance
        );

        if (!result.HasValue) return;

        // Check if there's a cable near the hit point
        var nearestEntry = Context.ZiplineRegistry.FindNearestEntry(result.Value.Position, 1.5f);
        if (nearestEntry.HasValue)
        {
            SetHoverHighlight(nearestEntry.Value.entry);
        }
    }

    private void SetHoverHighlight(ZiplineEntry entry)
    {
        _hoveredCable = entry.Cable;
        _hoveredAnchor1 = entry.Anchor1;
        _hoveredAnchor2 = entry.Anchor2;

        var highlightColor = Color.FromHtml(Config.HighlightColor);
        _hoveredCable?.SetHighlighted(highlightColor);
        _hoveredAnchor1?.SetHighlighted(highlightColor);
        _hoveredAnchor2?.SetHighlighted(highlightColor);
    }

    private void ClearHoverHighlight()
    {
        _hoveredCable?.ClearHighlight();
        _hoveredAnchor1?.ClearHighlight();
        _hoveredAnchor2?.ClearHighlight();

        _hoveredCable = null;
        _hoveredAnchor1 = null;
        _hoveredAnchor2 = null;
    }

    private void UpdatePreviewColor()
    {
        if (_previewMaterial == null) return;

        var color = _placementStatus switch
        {
            PlacementStatus.Valid => Color.FromHtml(Config.ValidPlacementColor),
            PlacementStatus.Obstructed => Color.FromHtml(Config.ObstructedPlacementColor),
            PlacementStatus.OutOfRange => Color.FromHtml(Config.InvalidPlacementColor),
            PlacementStatus.InsufficientScore => Color.FromHtml(Config.InvalidPlacementColor),
            _ => Color.FromHtml(Config.InvalidPlacementColor)
        };

        _previewMaterial.AlbedoColor = new Color(color.R, color.G, color.B, 0.7f);
        _previewMaterial.Emission = color;
    }

    private PhysicsDirectSpaceState3D? GetSpaceState()
    {
        return Context.Player.GetWorld3D()?.DirectSpaceState;
    }

    private RaycastResult? PerformPlacementRaycast()
    {
        var spaceState = GetSpaceState();
        if (spaceState == null) return null;

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
        // Get scaled cost (0 if free placements enabled)
        var baseCost = Config.PlacementCost;
        var cost = Context.DifficultyService?.GetZiplinePlacementCost(baseCost) ?? baseCost;

        // For first anchor, check if player has enough score to place
        if (_state == ZiplineState.Idle)
        {
            if (cost > 0 && Context.ScoreService != null && Context.ScoreService.Score < cost)
            {
                return PlacementStatus.InsufficientScore;
            }
        }

        // For second anchor, check cable length constraints and score
        if (_state == ZiplineState.FirstAnchor)
        {
            // Check if player has enough score to place
            if (cost > 0 && Context.ScoreService != null && Context.ScoreService.Score < cost)
            {
                return PlacementStatus.InsufficientScore;
            }

            // Disallow placing both anchors on the same wall face.
            if (_anchor1ColliderId != 0 && _anchor1ColliderId == _previewColliderId)
            {
                if (_anchor1Normal.LengthSquared() > 0.0001f && _previewNormal.LengthSquared() > 0.0001f)
                {
                    // Block if both anchors are on the same face (normals nearly aligned).
                    if (_anchor1Normal.Normalized().Dot(_previewNormal.Normalized()) > 0.95f)
                        return PlacementStatus.Obstructed;
                }
            }

            float cableLength = _anchor1Position.DistanceTo(position);
            if (cableLength > MaxPlacementDistance) return PlacementStatus.OutOfRange;
            if (cableLength < Config.MinCableLength) return PlacementStatus.OutOfRange;

            // Check that cable path isn't obstructed (e.g., going through buildings)
            // Offset along surface normals to start from outside the buildings
            var spaceState = GetSpaceState();
            if (spaceState != null)
            {
                var offsetStart = _anchor1Position + _anchor1Normal * 0.1f;
                var offsetEnd = position + _previewNormal * 0.1f;
                var hit = RaycastHelper.Raycast(spaceState, offsetStart, offsetEnd);
                if (hit != null)
                {
                    return PlacementStatus.Obstructed;
                }

            }
        }

        return PlacementStatus.Valid;
    }

    private static bool TryFindClearSag(
        PhysicsDirectSpaceState3D spaceState,
        GodotVector3 start,
        GodotVector3 startNormal,
        GodotVector3 end,
        GodotVector3 endNormal,
        float maxSag,
        global::Godot.Collections.Array<Rid> exclude,
        out float clearSag)
    {
        // If max sag is already clear, keep it.
        if (!IsSaggedCableObstructed(spaceState, start, startNormal, end, endNormal, maxSag, exclude))
        {
            clearSag = maxSag;
            return true;
        }

        // If even straight cable is obstructed, fail (should be rare due to straight raycast).
        if (IsSaggedCableObstructed(spaceState, start, startNormal, end, endNormal, 0f, exclude))
        {
            clearSag = 0f;
            return false;
        }

        // Binary search for the maximum sag that stays clear.
        float low = 0f;
        float high = maxSag;
        for (int i = 0; i < 8; i++)
        {
            float mid = (low + high) * 0.5f;
            if (IsSaggedCableObstructed(spaceState, start, startNormal, end, endNormal, mid, exclude))
                high = mid;
            else
                low = mid;
        }

        clearSag = low;
        return true;
    }

    private static bool IsSaggedCableObstructed(
        PhysicsDirectSpaceState3D spaceState,
        GodotVector3 start,
        GodotVector3 startNormal,
        GodotVector3 end,
        GodotVector3 endNormal,
        float sag,
        global::Godot.Collections.Array<Rid> exclude)
    {
        const int samples = 20;
        const float surfaceOffset = 0.1f;

        var points = new GodotVector3[samples + 1];
        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            var basePos = start.Lerp(end, t);
            float sagAmount = sag * 4f * t * (1f - t);
            points[i] = basePos - new GodotVector3(0f, sagAmount, 0f);
        }

        // Push the endpoints outward from their surfaces to avoid immediate self-intersection.
        points[0] += startNormal * surfaceOffset;
        points[^1] += endNormal * surfaceOffset;

        for (int i = 0; i < samples; i++)
        {
            var hit = RaycastHelper.RaycastExclude(spaceState, points[i], points[i + 1], exclude);
            if (hit != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool CanPlaceAt(GodotVector3 position)
    {
        return GetPlacementStatus(position) == PlacementStatus.Valid;
    }

    // IPlaceableTool interface implementation
    bool IPlaceableTool.CanPlaceAt(SystemVector3 position)
    {
        return CanPlaceAt(new GodotVector3(position.X, position.Y, position.Z));
    }

    /// <inheritdoc/>
    public override bool HandleInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mb || !mb.Pressed) return false;

        if (mb.ButtonIndex == MouseButton.Left)
        {
            return TryPlaceAnchor();
        }
        else if (mb.ButtonIndex == MouseButton.Right)
        {
            // Priority 1: Remove hovered zipline
            if (_hoveredCable != null)
            {
                Context.ZiplineRegistry.RemoveZipline(_hoveredCable);
                ClearHoverHighlight();
                Log.Debug(LogCategory.Input, "Zipline: Removed existing zipline");
                return true;
            }

            // Priority 2: Cancel only if first anchor is placed
            if (_state == ZiplineState.FirstAnchor)
            {
                CancelPlacement();
                return true;
            }

            // In Idle state, right-click does nothing
            return false;
        }

        return false;
    }

    private bool TryPlaceAnchor()
    {
        if (_hoveredCable != null) return false;
        if (_placementStatus != PlacementStatus.Valid) return false;

        switch (_state)
        {
            case ZiplineState.Idle:
                PlaceFirstAnchor(_previewPosition);
                return true;

            case ZiplineState.FirstAnchor:
                PlaceSecondAnchor(_previewPosition);
                return true;

            default:
                return false;
        }
    }

    private void PlaceFirstAnchor(GodotVector3 position)
    {
        _anchor1Position = position;
        _anchor1Normal = _previewNormal;
        _anchor1ColliderId = _previewColliderId;
        _anchor1 = CreateAnchor(position);
        _state = ZiplineState.FirstAnchor;

        // Play anchor placement sound
        if (Context.SfxManager != null)
            Context.SfxManager.PlayGenerated(SfxSynth.AnchorPlace());

        Log.Debug(LogCategory.Input, $"Zipline: First anchor at {position}");
    }

    private void PlaceSecondAnchor(GodotVector3 position)
    {
        _anchor2 = CreateAnchor(position);

        // Play anchor placement sound
        if (Context.SfxManager != null)
            Context.SfxManager.PlayGenerated(SfxSynth.AnchorPlace());

        // Check max zipline limit BEFORE registering (0 = unlimited)
        var maxZiplines = Context.DifficultyService?.GetMaxZiplines(Config.MaxZiplines) ?? Config.MaxZiplines;
        if (maxZiplines > 0 && Context.ZiplineRegistry.Count >= maxZiplines)
        {
            Context.ZiplineRegistry.RemoveOldest();
            Log.Debug(LogCategory.Input, "Zipline: Removed oldest zipline (at limit)");
        }

        // Adjust sag at placement time to avoid clipping, without affecting placement validity.
        _placementSag = Config.CableSag;
        var spaceState = GetSpaceState();
        if (spaceState != null)
        {
            var exclude = new global::Godot.Collections.Array<Rid>();
            RaycastHelper.CollectCollisionRids(Context.Player, exclude);
            if (TryFindClearSag(spaceState, _anchor1Position, _anchor1Normal, position, _previewNormal, Config.CableSag, exclude, out var clearSag))
            {
                _placementSag = clearSag;
            }
        }

        _cable = CreateCable(_anchor1Position, position, _anchor1!, _anchor2);

        // Play cable creation sound
        if (Context.SfxManager != null)
            Context.SfxManager.PlayGenerated(SfxSynth.CableCreated());

        // Deduct placement cost (scaled by difficulty)
        var baseCost = Config.PlacementCost;
        var cost = Context.DifficultyService?.GetZiplinePlacementCost(baseCost) ?? baseCost;
        if (cost > 0)
        {
            Context.ScoreService?.RemoveScore(cost, ScoreCategory.ZiplinePlacement);
        }

        Log.Debug(LogCategory.Input, $"Zipline: Complete from {_anchor1Position} to {position}, cost={cost}");

        // Publish event for game systems
        Context.EventBus.Publish(new ZiplineCreatedEvent(
            new SystemVector3(_anchor1Position.X, _anchor1Position.Y, _anchor1Position.Z),
            new SystemVector3(position.X, position.Y, position.Z)
        ));

        // Auto-reset to allow immediate placement of next zipline
        ResetForNextPlacement();
    }

    private void ResetForNextPlacement()
    {
        // Clear references (don't destroy - they're now registered)
        _anchor1 = null;
        _anchor2 = null;
        _cable = null;
        _anchor1Normal = GodotVector3.Zero;
        _anchor1Position = GodotVector3.Zero;
        _anchor1ColliderId = 0;

        // Reset state to Idle
        _state = ZiplineState.Idle;

        // Show preview dot again
        if (_previewDot != null && Initialized && Visible)
        {
            _previewDot.Visible = true;
        }

        Log.Debug(LogCategory.Input, "Zipline: Ready for next placement");
    }

    private ZiplineAnchor CreateAnchor(GodotVector3 position)
    {
        var anchor = new ZiplineAnchor { Name = "ZiplineAnchor" };
        anchor.Configure(Config);

        // Add to current scene (not Root) so it's freed on scene change
        // Must add to tree FIRST, then set position (can't set GlobalPosition before in tree)
        GetTree().CurrentScene.AddChild(anchor);
        anchor.GlobalPosition = position;

        return anchor;
    }

    private ZiplineCable CreateCable(GodotVector3 start, GodotVector3 end, ZiplineAnchor anchor1, ZiplineAnchor anchor2)
    {
        var cable = new ZiplineCable { Name = "ZiplineCable" };
        cable.Configure(Config, start, end, _placementSag);

        // Add to current scene (not Root) so it's freed on scene change
        GetTree().CurrentScene.AddChild(cable);

        // Register with zipline registry for riding mechanics
        Context.ZiplineRegistry.Register(cable, anchor1, anchor2);

        return cable;
    }

    /// <inheritdoc/>
    public void CancelPlacement()
    {
        _anchor1?.QueueFree();

        _anchor1 = null;
        _anchor1Position = GodotVector3.Zero;
        _anchor1Normal = GodotVector3.Zero;
        _anchor1ColliderId = 0;
        _state = ZiplineState.Idle;

        // Show preview again
        if (_previewDot != null && Initialized && Visible)
        {
            _previewDot.Visible = true;
        }

        Log.Debug(LogCategory.Input, "Zipline: Placement cancelled");
    }
}
