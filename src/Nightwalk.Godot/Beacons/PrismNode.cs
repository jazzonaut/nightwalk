using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Beacons;

/// <summary>
/// A floating hexagonal prism that refracts signal beams.
/// Prisms spawn automatically above beacons when a sync is started.
/// The beam passes through the center and exits in the prism's forward direction.
/// Looks like glowing glass with visible edges.
/// </summary>
public sealed partial class PrismNode : Node3D
{
    private const int HexSegments = 6;
    // Offset to align edge vertices with CylinderMesh vertices (flat side forward)
    private const float HexAngleOffset = Mathf.Pi / 6f; // 30 degrees

    // Shared resources (created once, reused by all prisms)
    private static StandardMaterial3D? s_glassMaterial;
    private static StandardMaterial3D? s_edgeMaterial;
    private static CylinderMesh? s_glassBodyMesh;
    private static PrismSettings? s_cachedConfig;
    private static Color s_cachedEdgeColor;
    private static Color s_cachedTintColor;
    private static Color s_cachedExitLightColor;

    private Node3D? _pivot;
    private MeshInstance3D? _glassMesh;
    private MeshInstance3D? _edgesMesh;
    private OmniLight3D? _exitLight;
    private StaticBody3D? _collisionBody;
    private PrismSettings _config = null!;
    private bool _highlighted;
    private bool _locked;

    // Per-instance material overrides (created only when needed)
    private StandardMaterial3D? _instanceGlassMaterial;
    private StandardMaterial3D? _instanceEdgeMaterial;

    /// <summary>
    /// The forward direction of the prism (where the beam exits).
    /// </summary>
    public Vector3 BeamDirection => _pivot != null ? -_pivot.GlobalTransform.Basis.Z : -GlobalTransform.Basis.Z;

    /// <summary>
    /// Finds a PrismNode from a collider by walking up the node tree.
    /// </summary>
    /// <param name="collider">The collider from a raycast hit.</param>
    /// <returns>The PrismNode if found, null otherwise.</returns>
    public static PrismNode? FindFromCollider(GodotObject? collider)
    {
        if (collider is not Node node)
            return null;

        Node? current = node;
        while (current != null)
        {
            if (current is PrismNode prism)
                return prism;
            current = current.GetParent();
        }

        return null;
    }

    /// <summary>
    /// World position of the prism center (where beams pass through).
    /// </summary>
    public Vector3 PrismPosition => _pivot?.GlobalPosition ?? GlobalPosition;

    /// <summary>
    /// Rotation of the prism pivot (for serialization).
    /// </summary>
    public Vector3 PrismRotation => _pivot?.Rotation ?? Rotation;

    /// <summary>
    /// Sets the rotation of the prism pivot (for deserialization).
    /// </summary>
    public void SetPrismRotation(Vector3 rotation)
    {
        if (_pivot != null)
            _pivot.Rotation = rotation;
    }

    /// <summary>
    /// Orients the prism so its forward direction (-Z) points toward the target.
    /// Uses LookAt for proper 3D orientation.
    /// </summary>
    public void LookToward(Vector3 targetDirection)
    {
        if (_pivot == null || targetDirection.LengthSquared() < 0.001f)
            return;

        var target = _pivot.GlobalPosition + targetDirection.Normalized();
        // Avoid colinear up/target vectors (causes LookAt warnings and unstable roll).
        var direction = targetDirection.Normalized();
        var upVector = Mathf.Abs(direction.Dot(Vector3.Up)) > 0.99f
            ? Vector3.Forward
            : Vector3.Up;
        _pivot.LookAt(target, upVector);
    }

    /// <summary>
    /// Gets the RID of the collision body for raycast exclusion.
    /// </summary>
    public Rid CollisionRid => _collisionBody?.GetRid() ?? default;

    /// <summary>
    /// Configures the prism with appearance settings.
    /// </summary>
    public void Configure(PrismSettings config)
    {
        _config = config;
        Name = "Prism";

        EnsureSharedResources(config);
        CreateMesh();
    }

    private static void EnsureSharedResources(PrismSettings config)
    {
        var edgeColor = Color.FromHtml(config.EdgeColor);
        var tintColor = Color.FromHtml(config.TintColor);
        var exitLightColor = Color.FromHtml(config.ExitGlowColor);

        // Check if we need to recreate resources (config changed)
        bool configChanged = s_cachedConfig == null ||
            !s_cachedEdgeColor.IsEqualApprox(edgeColor) ||
            !s_cachedTintColor.IsEqualApprox(tintColor) ||
            !s_cachedExitLightColor.IsEqualApprox(exitLightColor) ||
            s_cachedConfig.Radius != config.Radius ||
            s_cachedConfig.Length != config.Length ||
            s_cachedConfig.Transparency != config.Transparency ||
            s_cachedConfig.EdgeGlowIntensity != config.EdgeGlowIntensity ||
            s_cachedConfig.EdgeThickness != config.EdgeThickness;

        if (configChanged)
        {
            s_cachedConfig = config;
            s_cachedEdgeColor = edgeColor;
            s_cachedTintColor = tintColor;
            s_cachedExitLightColor = exitLightColor;

            // Dispose old resources
            s_glassMaterial?.Dispose();
            s_edgeMaterial?.Dispose();
            s_glassBodyMesh?.Dispose();

            // Glass body material - transparent with slight tint
            s_glassMaterial = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(tintColor.R, tintColor.G, tintColor.B, config.Transparency),
                Metallic = 0.0f,
                Roughness = 0.0f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };

            // Edge material - bright glowing lines
            s_edgeMaterial = new StandardMaterial3D
            {
                AlbedoColor = edgeColor,
                EmissionEnabled = true,
                Emission = edgeColor,
                EmissionEnergyMultiplier = config.EdgeGlowIntensity,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };

            // Glass body mesh (reused)
            s_glassBodyMesh = new CylinderMesh
            {
                TopRadius = config.Radius,
                BottomRadius = config.Radius,
                Height = config.Length,
                RadialSegments = HexSegments
            };
        }
    }

    /// <summary>
    /// Disposes shared static resources. Call on scene/game exit.
    /// </summary>
    public static void DisposeSharedResources()
    {
        s_glassMaterial?.Dispose();
        s_glassMaterial = null;
        s_edgeMaterial?.Dispose();
        s_edgeMaterial = null;
        s_glassBodyMesh?.Dispose();
        s_glassBodyMesh = null;
        s_cachedConfig = null;
    }

    private void CreateMesh()
    {
        _pivot = new Node3D { Name = "Pivot" };
        AddChild(_pivot);

        CreateGlassBody();
        CreateEdgesMesh();
        CreateExitLight();
        CreateCollision();
    }

    private void CreateGlassBody()
    {
        if (_pivot == null || s_glassBodyMesh == null || s_glassMaterial == null) return;

        _glassMesh = new MeshInstance3D { Name = "GlassBody" };
        _glassMesh.Mesh = s_glassBodyMesh;
        _glassMesh.RotateX(Mathf.Pi / 2f); // Orient along Z axis (beam direction)
        _glassMesh.MaterialOverride = s_glassMaterial;
        _glassMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _pivot.AddChild(_glassMesh);
    }

    private void CreateEdgesMesh()
    {
        if (_pivot == null || s_edgeMaterial == null) return;

        _edgesMesh = new MeshInstance3D { Name = "Edges" };
        _edgesMesh.Mesh = CreateEdgeMesh(_config.Radius, _config.Length, _config.EdgeThickness);
        _edgesMesh.MaterialOverride = s_edgeMaterial;
        _edgesMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _pivot.AddChild(_edgesMesh);
    }

    private void CreateExitLight()
    {
        if (_pivot == null) return;

        _exitLight = new OmniLight3D
        {
            Name = "ExitLight",
            LightColor = s_cachedExitLightColor,
            LightEnergy = _config.ExitLightEnergy,
            OmniRange = _config.ExitLightRange,
            OmniAttenuation = 1.3f,
            ShadowEnabled = false
        };
        // Position at exit face, slightly outside
        _exitLight.Position = new Vector3(0, 0, -_config.Length / 2f - 0.2f);
        _pivot.AddChild(_exitLight);
    }

    /// <summary>
    /// Creates a hexagonal prism edge wireframe mesh. Shared by PrismNode and preview rendering.
    /// </summary>
    /// <param name="radius">Hexagon radius.</param>
    /// <param name="length">Prism length along beam axis.</param>
    /// <param name="edgeThickness">Thickness of edge tubes.</param>
    /// <returns>The generated edge mesh.</returns>
    public static ArrayMesh CreateEdgeMesh(float radius, float length, float edgeThickness)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var halfLength = length / 2f;

        // Calculate hex vertices (with offset to match CylinderMesh)
        var hexVertices = new Vector3[HexSegments];
        for (int i = 0; i < HexSegments; i++)
        {
            var angle = i * Mathf.Tau / HexSegments + HexAngleOffset;
            hexVertices[i] = new Vector3(
                radius * Mathf.Cos(angle),
                radius * Mathf.Sin(angle),
                0
            );
        }

        // Front face ring
        for (int i = 0; i < HexSegments; i++)
        {
            var start = hexVertices[i] with { Z = halfLength };
            var end = hexVertices[(i + 1) % HexSegments] with { Z = halfLength };
            AddCylinderSegment(st, start, end, edgeThickness);
        }

        // Back face ring
        for (int i = 0; i < HexSegments; i++)
        {
            var start = hexVertices[i] with { Z = -halfLength };
            var end = hexVertices[(i + 1) % HexSegments] with { Z = -halfLength };
            AddCylinderSegment(st, start, end, edgeThickness);
        }

        // Vertical edges connecting front and back
        for (int i = 0; i < HexSegments; i++)
        {
            var start = hexVertices[i] with { Z = -halfLength };
            var end = hexVertices[i] with { Z = halfLength };
            AddCylinderSegment(st, start, end, edgeThickness);
        }

        st.GenerateNormals();
        return st.Commit();
    }

    private static void AddCylinderSegment(SurfaceTool st, Vector3 start, Vector3 end, float thickness)
    {
        // Create a cylinder between two points using 4 quads (8 triangles)
        var direction = (end - start).Normalized();

        // Find perpendicular vectors for the cylinder cross-section
        var perp1 = direction.Cross(Vector3.Up).Normalized();
        if (perp1.LengthSquared() < 0.001f)
            perp1 = direction.Cross(Vector3.Right).Normalized();
        var perp2 = direction.Cross(perp1).Normalized();

        // 4 vertices around each end
        const int segments = 4;
        var startVerts = new Vector3[segments];
        var endVerts = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            var angle = i * Mathf.Tau / segments;
            var offset = (perp1 * Mathf.Cos(angle) + perp2 * Mathf.Sin(angle)) * thickness;
            startVerts[i] = start + offset;
            endVerts[i] = end + offset;
        }

        // Create quads (2 triangles each)
        for (int i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;

            // Triangle 1
            st.AddVertex(startVerts[i]);
            st.AddVertex(endVerts[i]);
            st.AddVertex(startVerts[next]);

            // Triangle 2
            st.AddVertex(startVerts[next]);
            st.AddVertex(endVerts[i]);
            st.AddVertex(endVerts[next]);
        }
    }

    private void CreateCollision()
    {
        if (_pivot == null) return;

        _collisionBody = new StaticBody3D { Name = "PrismCollision" };
        // Layer 16 for interactables - raycasts can hit but player doesn't collide
        _collisionBody.CollisionLayer = 1 << 15;
        _collisionBody.CollisionMask = 0;
        _pivot.AddChild(_collisionBody);

        var collisionShape = new CollisionShape3D { Name = "PrismShape" };
        collisionShape.Shape = new CylinderShape3D
        {
            Radius = _config.Radius + 0.3f,
            Height = _config.Length
        };
        collisionShape.RotateX(Mathf.Pi / 2f);
        _collisionBody.AddChild(collisionShape);
    }

    /// <summary>
    /// Disables collision so raycasts pass through.
    /// Call before QueueFree to prevent raycast hits on pending-deletion prisms.
    /// </summary>
    public void DisableCollision()
    {
        if (_collisionBody != null)
        {
            _collisionBody.CollisionLayer = 0;
            _collisionBody.CollisionMask = 0;
        }
    }

    /// <summary>
    /// Locks the prism, making it non-interactable (disables collision).
    /// Used for completed sync prisms that remain as decoration.
    /// </summary>
    public void Lock()
    {
        if (_locked) return;
        _locked = true;

        DisableCollision();
    }

    /// <summary>
    /// Whether this prism is locked (non-interactable).
    /// </summary>
    public bool IsLocked => _locked;

    /// <summary>
    /// Rotates the prism by the given yaw and pitch deltas.
    /// </summary>
    public void RotatePrism(float yawDelta, float pitchDelta)
    {
        if (_pivot == null)
            return;

        _pivot.RotateY(yawDelta);

        var currentPitch = _pivot.Rotation.X;
        var newPitch = Mathf.Clamp(currentPitch + pitchDelta, -Mathf.Pi / 2f + 0.1f, Mathf.Pi / 2f - 0.1f);
        var rotation = _pivot.Rotation;
        rotation.X = newPitch;
        _pivot.Rotation = rotation;
    }

    /// <summary>
    /// Calculates the refracted direction of the beam passing through the prism.
    /// </summary>
    public Vector3 RefractBeam(Vector3 incomingDirection)
    {
        return BeamDirection;
    }

    /// <summary>
    /// Sets the prism to highlighted state (for interaction feedback).
    /// Uses per-instance material to avoid affecting other prisms.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        if (_highlighted == highlighted)
            return;

        _highlighted = highlighted;

        EnsureInstanceGlassMaterial();
        if (_instanceGlassMaterial == null)
            return;

        if (highlighted)
        {
            // Make glass faces glow subtly
            _instanceGlassMaterial.EmissionEnabled = true;
            _instanceGlassMaterial.Emission = _exitLight?.LightColor ?? Colors.White;
            _instanceGlassMaterial.EmissionEnergyMultiplier = 0.2f;
        }
        else
        {
            // Restore transparent glass (no emission)
            _instanceGlassMaterial.EmissionEnabled = false;
            _instanceGlassMaterial.Emission = Colors.Black;
            _instanceGlassMaterial.EmissionEnergyMultiplier = 0f;
        }
    }

    /// <summary>
    /// Sets the color of the prism edges and exit glow (for frequency matching or effects).
    /// Uses per-instance material to avoid affecting other prisms.
    /// </summary>
    public void SetColor(Color color)
    {
        // Create per-instance edge material if needed
        EnsureInstanceEdgeMaterial();
        if (_instanceEdgeMaterial != null)
        {
            // Blend edge color with frequency: mostly white with slight tint
            var blended = s_cachedEdgeColor.Lerp(color, 0.3f);
            _instanceEdgeMaterial.AlbedoColor = blended;
            _instanceEdgeMaterial.Emission = blended;
        }

        // Set exit light to beam color
        SetExitLightColor(color);
    }

    /// <summary>
    /// Sets the color of the exit light.
    /// Called when a beam passes through the prism.
    /// </summary>
    public void SetExitLightColor(Color color)
    {
        if (_exitLight != null)
        {
            _exitLight.LightColor = color;
        }
    }

    /// <summary>
    /// Sets the prism opacity for POV mode.
    /// Uses per-instance material to avoid affecting other prisms.
    /// </summary>
    public void SetPrismOpacity(float opacity)
    {
        // Create per-instance glass material if needed
        EnsureInstanceGlassMaterial();
        if (_instanceGlassMaterial == null)
            return;

        _instanceGlassMaterial.AlbedoColor = new Color(s_cachedTintColor.R, s_cachedTintColor.G, s_cachedTintColor.B, opacity);
    }

    private void EnsureInstanceEdgeMaterial()
    {
        if (_instanceEdgeMaterial != null || s_edgeMaterial == null || _edgesMesh == null)
            return;

        // Clone the shared material for per-instance modifications
        _instanceEdgeMaterial = (StandardMaterial3D)s_edgeMaterial.Duplicate();
        _edgesMesh.MaterialOverride = _instanceEdgeMaterial;
    }

    private void EnsureInstanceGlassMaterial()
    {
        if (_instanceGlassMaterial != null || s_glassMaterial == null || _glassMesh == null)
            return;

        // Clone the shared material for per-instance modifications
        _instanceGlassMaterial = (StandardMaterial3D)s_glassMaterial.Duplicate();
        _glassMesh.MaterialOverride = _instanceGlassMaterial;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _instanceGlassMaterial?.Dispose();
        _instanceGlassMaterial = null;
        _instanceEdgeMaterial?.Dispose();
        _instanceEdgeMaterial = null;
    }
}
