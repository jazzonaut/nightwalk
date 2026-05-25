using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;

namespace Nightwalk.Godot.Rendering.Sky;

/// <summary>
/// Renders a ring of distant city building silhouettes at the horizon.
/// Creates the illusion of an infinite city beyond the loaded chunks.
/// </summary>
public sealed partial class DistantCitySilhouette : Node3D
{
    /// <summary>
    /// Fixed seed for silhouette generation. Intentionally not derived from the world seed —
    /// the horizon silhouette stays visually stable across sessions and worlds.
    /// </summary>
    private const int SilhouetteSeed = 42;

    private MeshInstance3D? _silhouetteMesh;
    private Node3D? _followTarget;
    private SkyEffectsConfig _config = null!;

    public static DistantCitySilhouette Create(SkyEffectsConfig config)
    {
        var instance = new DistantCitySilhouette { Name = "DistantCitySilhouette" };
        instance._config = config;
        return instance;
    }

    public void SetFollowTarget(Node3D target)
    {
        _followTarget = target;
    }

    /// <summary>
    /// Reconfigure from updated config (hot-reload support).
    /// </summary>
    public void Configure(SkyEffectsConfig config)
    {
        _config = config;

        if (IsInsideTree())
        {
            Recreate();
        }
    }

    public override void _Ready()
    {
        Recreate();
    }

    private void Recreate()
    {
        // Clean up old mesh
        if (_silhouetteMesh != null)
        {
            _silhouetteMesh.QueueFree();
            _silhouetteMesh = null;
        }

        if (!_config.SilhouettesEnabled)
        {
            return;
        }

        CreateSilhouetteMesh();
    }

    private void CreateSilhouetteMesh()
    {
        // Deterministic RNG with fixed seed — silhouette stays stable across world seeds.
        var rng = new DeterministicRng(SilhouetteSeed);

        float innerRadius = _config.SilhouetteInnerRadius;
        float outerRadius = _config.SilhouetteOuterRadius;
        int buildingCount = _config.SilhouetteBuildingCount;
        float minHeight = _config.SilhouetteMinHeight;
        float maxHeight = _config.SilhouetteMaxHeight;
        float minWidth = _config.SilhouetteMinWidth;
        float maxWidth = _config.SilhouetteMaxWidth;

        // Build a single combined mesh of all silhouette buildings
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < buildingCount; i++)
        {
            // Distribute buildings around the ring
            float angle = (i / (float)buildingCount) * Mathf.Pi * 2f;

            // Randomize position within ring depth
            float radius = innerRadius + rng.NextFloat() * (outerRadius - innerRadius);

            // Add some angular jitter
            angle += (rng.NextFloat() - 0.5f) * 0.03f;

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // Random building dimensions
            float width = minWidth + rng.NextFloat() * (maxWidth - minWidth);
            float depth = minWidth + rng.NextFloat() * (maxWidth - minWidth);
            float height = minHeight + rng.NextFloat() * (maxHeight - minHeight);

            // Taller buildings at inner edge (closer to player) to mask horizon
            float ringPos = (radius - innerRadius) / (outerRadius - innerRadius);
            if (ringPos < 0.4f && _config.SilhouetteInnerEdgeHeightBoost > 1f)
            {
                float boostRange = _config.SilhouetteInnerEdgeHeightBoost - 1f;
                height *= 1f + boostRange * (1f - ringPos / 0.4f);
            }

            // Tall towers
            if (rng.NextFloat() < _config.SilhouetteTowerChance)
            {
                float heightRange = _config.SilhouetteTowerHeightMax - _config.SilhouetteTowerHeightMin;
                height *= _config.SilhouetteTowerHeightMin + rng.NextFloat() * heightRange;
                width *= _config.SilhouetteTowerWidthMult;
                depth *= _config.SilhouetteTowerWidthMult;
            }

            // Super-tall spires
            if (rng.NextFloat() < _config.SilhouetteSpireChance)
            {
                height *= _config.SilhouetteSpireHeightMult;
                width *= _config.SilhouetteSpireWidthMult;
                depth *= _config.SilhouetteSpireWidthMult;
            }

            AddBuilding(surfaceTool, rng, x, z, width, depth, height, angle);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        // Parse color from config
        var silhouetteColor = Color.FromHtml(_config.SilhouetteColor);

        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = silhouetteColor,
            CullMode = BaseMaterial3D.CullModeEnum.Back
        };

        _silhouetteMesh = new MeshInstance3D
        {
            Name = "SilhouetteMesh",
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };

        AddChild(_silhouetteMesh);
    }

    private static void AddBuilding(SurfaceTool st, DeterministicRng rng, float x, float z, float width, float depth, float height, float angle)
    {
        // Rotate building to roughly face center (more interesting silhouette)
        float rot = angle + Mathf.Pi + (rng.NextFloat() - 0.5f) * 0.5f;
        float cos = Mathf.Cos(rot);
        float sin = Mathf.Sin(rot);

        float hw = width * 0.5f;
        float hd = depth * 0.5f;

        // Transform corners
        Vector3[] corners = new Vector3[8];
        float[] localX = { -hw, hw, hw, -hw };
        float[] localZ = { -hd, -hd, hd, hd };

        for (int i = 0; i < 4; i++)
        {
            float lx = localX[i];
            float lz = localZ[i];
            float worldX = x + lx * cos - lz * sin;
            float worldZ = z + lx * sin + lz * cos;
            corners[i] = new Vector3(worldX, 0, worldZ);         // Bottom
            corners[i + 4] = new Vector3(worldX, height, worldZ); // Top
        }

        // Add faces (front, right, back, left, top)
        AddQuad(st, corners[0], corners[1], corners[5], corners[4]);
        AddQuad(st, corners[1], corners[2], corners[6], corners[5]);
        AddQuad(st, corners[2], corners[3], corners[7], corners[6]);
        AddQuad(st, corners[3], corners[0], corners[4], corners[7]);
        AddQuad(st, corners[4], corners[5], corners[6], corners[7]);
    }

    private static void AddQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        st.AddVertex(a);
        st.AddVertex(b);
        st.AddVertex(c);
        st.AddVertex(a);
        st.AddVertex(c);
        st.AddVertex(d);
    }

    public override void _Process(double delta)
    {
        if (_followTarget != null)
        {
            GlobalPosition = new Vector3(
                _followTarget.GlobalPosition.X,
                0,
                _followTarget.GlobalPosition.Z
            );
        }
    }

    public override void _ExitTree()
    {
        _silhouetteMesh?.QueueFree();
        _silhouetteMesh = null;
    }
}
