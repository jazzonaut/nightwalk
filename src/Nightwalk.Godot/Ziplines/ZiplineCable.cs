using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Ziplines;

/// <summary>
/// Visual representation of a zipline cable with catenary sag.
/// Provides geometry queries for riding mechanics.
/// </summary>
public sealed partial class ZiplineCable : Node3D
{
    private const int SegmentCount = 20;
    private static Shader? _cableShader;

    private ArrayMesh? _mesh;
    private MeshInstance3D? _meshInstance;
    private ShaderMaterial? _material;

    /// <summary>
    /// Start position of the cable (first anchor).
    /// </summary>
    public Vector3 Start { get; private set; }

    /// <summary>
    /// End position of the cable (second anchor).
    /// </summary>
    public Vector3 End { get; private set; }

    /// <summary>
    /// Catenary sag amount at midpoint.
    /// </summary>
    public float Sag { get; private set; }

    /// <summary>
    /// Approximate length of the cable (straight line distance).
    /// </summary>
    public float Length => Start.DistanceTo(End);

    /// <summary>
    /// Configures and generates the cable between two points.
    /// </summary>
    /// <param name="config">Zipline configuration.</param>
    /// <param name="start">Start position (first anchor).</param>
    /// <param name="end">End position (second anchor).</param>
    /// <param name="sagOverride">Optional sag override (defaults to config).</param>
    public void Configure(ZiplineConfig config, Vector3 start, Vector3 end, float? sagOverride = null)
    {
        // Store geometry for riding queries
        Start = start;
        End = end;
        Sag = sagOverride ?? config.CableSag;

        // Load shader once (cached)
        _cableShader ??= GD.Load<Shader>("res://shaders/zipline_cable.gdshader");

        _material = new ShaderMaterial { Shader = _cableShader };
        _material.SetShaderParameter("emission_strength", config.CableEmission);

        _mesh = GenerateCatenaryCable(start, end, Sag, config.CableThickness);
        _meshInstance = new MeshInstance3D
        {
            Name = "CableMesh",
            Mesh = _mesh,
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_meshInstance);
    }

    private const int TubeSides = 6; // Hexagonal cross-section

    private ArrayMesh GenerateCatenaryCable(Vector3 start, Vector3 end, float sag, float thickness)
    {
        float radius = thickness * 0.5f;

        // Generate cable center points with sag
        var points = new Vector3[SegmentCount + 1];
        var tangents = new Vector3[SegmentCount + 1];

        for (int i = 0; i <= SegmentCount; i++)
        {
            float t = (float)i / SegmentCount;
            var basePos = start.Lerp(end, t);

            // Catenary sag: maximum at center (t=0.5), zero at ends
            float sagAmount = sag * 4f * t * (1f - t);
            points[i] = basePos - new Vector3(0, sagAmount, 0);

            // Calculate tangent at this point
            tangents[i] = GetTangentAtT(t);
        }

        // Use SurfaceTool for proper UV support
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // Generate tube mesh with quads (as triangle pairs)
        for (int i = 0; i < SegmentCount; i++)
        {
            float u0 = (float)i / SegmentCount;
            float u1 = (float)(i + 1) / SegmentCount;

            // Calculate perpendicular vectors at both segments
            var tangent0 = tangents[i];
            var perp1_0 = tangent0.Cross(Vector3.Up);
            if (perp1_0.LengthSquared() < 0.001f)
                perp1_0 = tangent0.Cross(Vector3.Right);
            perp1_0 = perp1_0.Normalized();
            var perp2_0 = tangent0.Cross(perp1_0).Normalized();

            var tangent1 = tangents[i + 1];
            var perp1_1 = tangent1.Cross(Vector3.Up);
            if (perp1_1.LengthSquared() < 0.001f)
                perp1_1 = tangent1.Cross(Vector3.Right);
            perp1_1 = perp1_1.Normalized();
            var perp2_1 = tangent1.Cross(perp1_1).Normalized();

            for (int side = 0; side < TubeSides; side++)
            {
                float angle0 = (float)side / TubeSides * Mathf.Tau;
                float angle1 = (float)(side + 1) / TubeSides * Mathf.Tau;

                // Four corners of the quad
                var p00 = points[i] + (perp1_0 * Mathf.Cos(angle0) + perp2_0 * Mathf.Sin(angle0)) * radius;
                var p01 = points[i] + (perp1_0 * Mathf.Cos(angle1) + perp2_0 * Mathf.Sin(angle1)) * radius;
                var p10 = points[i + 1] + (perp1_1 * Mathf.Cos(angle0) + perp2_1 * Mathf.Sin(angle0)) * radius;
                var p11 = points[i + 1] + (perp1_1 * Mathf.Cos(angle1) + perp2_1 * Mathf.Sin(angle1)) * radius;

                // UVs - u along length, v around circumference
                var uv00 = new Vector2(u0, (float)side / TubeSides);
                var uv01 = new Vector2(u0, (float)(side + 1) / TubeSides);
                var uv10 = new Vector2(u1, (float)side / TubeSides);
                var uv11 = new Vector2(u1, (float)(side + 1) / TubeSides);

                // Triangle 1: p00, p10, p01
                st.SetUV(uv00); st.AddVertex(p00);
                st.SetUV(uv10); st.AddVertex(p10);
                st.SetUV(uv01); st.AddVertex(p01);

                // Triangle 2: p01, p10, p11
                st.SetUV(uv01); st.AddVertex(p01);
                st.SetUV(uv10); st.AddVertex(p10);
                st.SetUV(uv11); st.AddVertex(p11);
            }
        }

        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>
    /// Updates the cable endpoints (for future use if anchors move).
    /// </summary>
    public void UpdateEndpoints(ZiplineConfig config, Vector3 start, Vector3 end)
    {
        if (_meshInstance == null) return;
        Start = start;
        End = end;
        Sag = config.CableSag;
        _mesh = GenerateCatenaryCable(start, end, config.CableSag, config.CableThickness);
        _meshInstance.Mesh = _mesh;
    }

    /// <summary>
    /// Gets the world position on the cable at parameter t.
    /// </summary>
    /// <param name="t">Position along cable (0 = start, 1 = end).</param>
    /// <returns>World position on the catenary curve.</returns>
    public Vector3 GetPositionAtT(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        var basePos = Start.Lerp(End, t);
        float sagAmount = Sag * 4f * t * (1f - t);
        return basePos - new Vector3(0, sagAmount, 0);
    }

    /// <summary>
    /// Gets the tangent (direction) of the cable at parameter t.
    /// </summary>
    /// <param name="t">Position along cable (0 = start, 1 = end).</param>
    /// <returns>Normalized direction vector along the cable.</returns>
    public Vector3 GetTangentAtT(float t)
    {
        const float epsilon = 0.001f;
        var p1 = GetPositionAtT(Mathf.Max(0f, t - epsilon));
        var p2 = GetPositionAtT(Mathf.Min(1f, t + epsilon));
        return (p2 - p1).Normalized();
    }

    /// <summary>
    /// Finds the closest point on the cable to the given position.
    /// </summary>
    /// <param name="position">World position to check.</param>
    /// <returns>Tuple of (t parameter, distance to cable).</returns>
    public (float t, float distance) FindClosestT(Vector3 position)
    {
        const int samples = 20;
        float bestT = 0f;
        float bestDistSq = float.MaxValue;

        // Sample along curve to find closest segment
        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            var cablePos = GetPositionAtT(t);
            float distSq = position.DistanceSquaredTo(cablePos);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestT = t;
            }
        }

        // Refine with binary search around best sample
        float step = 1f / samples / 2f;
        for (int i = 0; i < 5; i++)
        {
            float tLow = Mathf.Max(0f, bestT - step);
            float tHigh = Mathf.Min(1f, bestT + step);

            var posLow = GetPositionAtT(tLow);
            var posHigh = GetPositionAtT(tHigh);
            float distLow = position.DistanceSquaredTo(posLow);
            float distHigh = position.DistanceSquaredTo(posHigh);

            if (distLow < distHigh)
            {
                bestT = tLow;
                bestDistSq = distLow;
            }
            else
            {
                bestT = tHigh;
                bestDistSq = distHigh;
            }
            step *= 0.5f;
        }

        return (bestT, Mathf.Sqrt(bestDistSq));
    }

    /// <summary>
    /// Sets the cable to highlighted state with the specified color.
    /// </summary>
    public void SetHighlighted(Color highlightColor)
    {
        if (_material == null) return;
        _material.SetShaderParameter("highlight_color", new Vector3(highlightColor.R, highlightColor.G, highlightColor.B));
        _material.SetShaderParameter("highlight_mix", 1.0f);
    }

    /// <summary>
    /// Restores the cable to its normal cycling colors.
    /// </summary>
    public void ClearHighlight()
    {
        if (_material == null) return;
        _material.SetShaderParameter("highlight_mix", 0.0f);
    }
}
