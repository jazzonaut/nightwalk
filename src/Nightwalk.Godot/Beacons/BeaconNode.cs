using Godot;
using Nightwalk.Core.Beacons;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Beacons;

/// <summary>
/// Visual representation of a beacon on a building rooftop.
/// Renders as a glowing glass hexagonal pyramid with highlighted edges.
/// </summary>
public sealed partial class BeaconNode : Node3D
{
    private const int HexSegments = 6;

    private MeshInstance3D? _glassMesh;
    private MeshInstance3D? _edgesMesh;
    private MeshInstance3D? _markerBeam;
    private OmniLight3D? _light;
    private StandardMaterial3D? _glassMaterial;
    private StandardMaterial3D? _edgeMaterial;
    private ShaderMaterial? _beamMaterial;

    private BeaconData _data;
    private BeaconState _state = BeaconState.Idle;
    private Color _frequencyColor;
    private Color _edgeColor;
    private float _baseEdgeIntensity;

    /// <summary>
    /// Beacon data for this node.
    /// </summary>
    public BeaconData Data => _data;

    /// <summary>
    /// Current state of this beacon.
    /// </summary>
    public BeaconState State => _state;

    /// <summary>
    /// Frequency color for this beacon.
    /// </summary>
    public Color FrequencyColor => _frequencyColor;

    /// <summary>
    /// Configures the beacon with its data and appearance settings.
    /// </summary>
    public void Configure(BeaconData data, BeaconsConfig config)
    {
        _data = data;
        Name = $"Beacon_{data.Id}";

        // Get frequency color
        var colorIndex = data.Frequency.Index;
        var colors = config.Beacon.Frequencies;
        _frequencyColor = colorIndex < colors.Length
            ? Color.FromHtml(colors[colorIndex])
            : Colors.White;

        _baseEdgeIntensity = config.Beacon.EdgeGlowIntensity;

        CreateGlassBody(config.Beacon);
        CreateEdgesMesh(config.Beacon);
        CreateMarkerBeam(config.Beacon);
        CreateLight(config.Beacon);

        UpdateVisuals(config.Beacon);
    }

    private void CreateGlassBody(BeaconSettings settings)
    {
        _glassMesh = new MeshInstance3D { Name = "GlassBody" };

        var mesh = CreateHexagonalPyramidMesh(settings.BaseRadius, settings.Height);
        _glassMesh.Mesh = mesh;

        var tintColor = Color.FromHtml(settings.TintColor);
        // Blend tint with frequency color for a subtle colored glass effect
        var blendedTint = tintColor.Lerp(_frequencyColor, 0.2f);

        _glassMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(blendedTint.R, blendedTint.G, blendedTint.B, settings.Transparency),
            Metallic = 0.0f,
            Roughness = 0.0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _glassMesh.MaterialOverride = _glassMaterial;
        _glassMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        AddChild(_glassMesh);
    }

    private void CreateEdgesMesh(BeaconSettings settings)
    {
        _edgesMesh = new MeshInstance3D { Name = "Edges" };

        var mesh = CreatePyramidEdgeMesh(settings.BaseRadius, settings.Height, settings.EdgeThickness);
        _edgesMesh.Mesh = mesh;

        // Blend edge color with frequency color
        var baseEdgeColor = Color.FromHtml(settings.EdgeColor);
        _edgeColor = baseEdgeColor.Lerp(_frequencyColor, 0.5f);

        _edgeMaterial = new StandardMaterial3D
        {
            AlbedoColor = _edgeColor,
            EmissionEnabled = true,
            Emission = _edgeColor,
            EmissionEnergyMultiplier = settings.EdgeGlowIntensity,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _edgesMesh.MaterialOverride = _edgeMaterial;
        _edgesMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        AddChild(_edgesMesh);
    }

    private void CreateMarkerBeam(BeaconSettings settings)
    {
        _markerBeam = new MeshInstance3D { Name = "MarkerBeam" };

        var cylinder = new CylinderMesh
        {
            TopRadius = settings.MarkerBeamWidth * 0.5f,
            BottomRadius = settings.MarkerBeamWidth * 0.5f,
            Height = settings.MarkerBeamHeight
        };
        _markerBeam.Mesh = cylinder;

        // Load the signal beam shader
        var shader = GD.Load<Shader>("res://shaders/signal_beam.gdshader");
        _beamMaterial = new ShaderMaterial { Shader = shader };
        _beamMaterial.SetShaderParameter("beam_color", _frequencyColor);
        _beamMaterial.SetShaderParameter("intensity", settings.MarkerBeamIntensity);

        _markerBeam.MaterialOverride = _beamMaterial;
        _markerBeam.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _markerBeam.Position = new Vector3(0, settings.MarkerBeamHeight / 2f + settings.Height, 0);
        _markerBeam.Visible = false;

        AddChild(_markerBeam);
    }

    private void CreateLight(BeaconSettings settings)
    {
        _light = new OmniLight3D
        {
            Name = "BeaconLight",
            LightColor = _frequencyColor,
            LightEnergy = settings.GlowIntensity,
            OmniRange = 8f,
            OmniAttenuation = 1.5f,
            Position = new Vector3(0, settings.Height * 0.4f, 0)
        };
        AddChild(_light);
    }

    private static ArrayMesh CreateHexagonalPyramidMesh(float radius, float height)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // Hexagon vertices at base (y=0)
        var baseVerts = new Vector3[HexSegments];
        for (int i = 0; i < HexSegments; i++)
        {
            float angle = i * Mathf.Pi / 3f;
            baseVerts[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius
            );
        }

        var apex = new Vector3(0, height, 0);

        // Side faces only (no bottom face - sits on roof surface)
        for (int i = 0; i < HexSegments; i++)
        {
            int next = (i + 1) % HexSegments;
            var v0 = baseVerts[i];
            var v1 = baseVerts[next];

            // Calculate face normal
            var edge1 = v1 - v0;
            var edge2 = apex - v0;
            var normal = edge1.Cross(edge2).Normalized();

            st.SetNormal(normal);
            st.AddVertex(v0);
            st.SetNormal(normal);
            st.AddVertex(v1);
            st.SetNormal(normal);
            st.AddVertex(apex);
        }

        return st.Commit();
    }

    private static ArrayMesh CreatePyramidEdgeMesh(float radius, float height, float edgeThickness)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // Hexagon vertices at base (y=0)
        var baseVerts = new Vector3[HexSegments];
        for (int i = 0; i < HexSegments; i++)
        {
            float angle = i * Mathf.Pi / 3f;
            baseVerts[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius
            );
        }

        var apex = new Vector3(0, height, 0);

        // Base ring edges
        for (int i = 0; i < HexSegments; i++)
        {
            int next = (i + 1) % HexSegments;
            AddCylinderSegment(st, baseVerts[i], baseVerts[next], edgeThickness);
        }

        // Slant edges from base to apex
        for (int i = 0; i < HexSegments; i++)
        {
            AddCylinderSegment(st, baseVerts[i], apex, edgeThickness);
        }

        // Sphere at apex to cap the joint
        AddSphere(st, apex, edgeThickness);

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

    private static void AddSphere(SurfaceTool st, Vector3 center, float radius)
    {
        // Simple icosphere approximation using octahedron subdivided once
        const int rings = 4;
        const int sectors = 6;

        for (int r = 0; r < rings; r++)
        {
            float theta1 = r * Mathf.Pi / rings;
            float theta2 = (r + 1) * Mathf.Pi / rings;

            for (int s = 0; s < sectors; s++)
            {
                float phi1 = s * Mathf.Tau / sectors;
                float phi2 = (s + 1) * Mathf.Tau / sectors;

                // Four corners of the quad
                var v1 = center + SphericalToCartesian(radius, theta1, phi1);
                var v2 = center + SphericalToCartesian(radius, theta2, phi1);
                var v3 = center + SphericalToCartesian(radius, theta2, phi2);
                var v4 = center + SphericalToCartesian(radius, theta1, phi2);

                // Two triangles
                st.AddVertex(v1);
                st.AddVertex(v2);
                st.AddVertex(v3);

                st.AddVertex(v1);
                st.AddVertex(v3);
                st.AddVertex(v4);
            }
        }
    }

    private static Vector3 SphericalToCartesian(float r, float theta, float phi)
    {
        float sinTheta = Mathf.Sin(theta);
        return new Vector3(
            r * sinTheta * Mathf.Cos(phi),
            r * Mathf.Cos(theta),
            r * sinTheta * Mathf.Sin(phi)
        );
    }

    /// <summary>
    /// Sets the beacon state and updates visuals accordingly.
    /// </summary>
    public void SetState(BeaconState state, BeaconSettings settings)
    {
        _state = state;
        UpdateVisuals(settings);
    }

    /// <summary>
    /// Shows or hides the marker beam.
    /// </summary>
    public void SetMarkerVisible(bool visible)
    {
        if (_markerBeam != null)
            _markerBeam.Visible = visible;
    }

    private void UpdateVisuals(BeaconSettings settings)
    {
        // Check if materials are still valid (not disposed)
        if (!GodotObject.IsInstanceValid(_edgeMaterial) || !GodotObject.IsInstanceValid(_light))
            return;

        float intensityMultiplier = _state switch
        {
            BeaconState.Idle => 1.0f,
            BeaconState.Source => settings.ActiveGlowIntensity / settings.GlowIntensity,
            BeaconState.Receiving => (settings.ActiveGlowIntensity * 0.8f) / settings.GlowIntensity,
            BeaconState.Completed => 0.3f,
            _ => 1.0f
        };

        // Update edge glow intensity
        _edgeMaterial.EmissionEnergyMultiplier = _baseEdgeIntensity * intensityMultiplier;

        // Update light intensity
        _light.LightEnergy = settings.GlowIntensity * intensityMultiplier;

        // Dim completed beacons
        if (_state == BeaconState.Completed)
        {
            var dimColor = _edgeColor.Darkened(0.5f);
            _edgeMaterial.AlbedoColor = dimColor;
            _edgeMaterial.Emission = dimColor;
            _light.LightColor = _frequencyColor.Darkened(0.5f);

            if (GodotObject.IsInstanceValid(_glassMaterial))
            {
                var dimGlass = _glassMaterial.AlbedoColor;
                _glassMaterial.AlbedoColor = new Color(dimGlass.R * 0.5f, dimGlass.G * 0.5f, dimGlass.B * 0.5f, dimGlass.A);
            }
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _glassMaterial?.Dispose();
        _edgeMaterial?.Dispose();
        _beamMaterial?.Dispose();
    }
}
