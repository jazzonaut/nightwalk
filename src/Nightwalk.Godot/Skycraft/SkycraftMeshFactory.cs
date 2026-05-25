using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Skycraft;

namespace Nightwalk.Godot.Skycraft;

/// <summary>
/// Builds the three placeholder profile meshes (one per <see cref="SkycraftProfile"/>).
/// Per ADR-0016 §Visual identity: three meshes total, no within-profile mesh variants.
/// Per-instance variation rides on transform scale + accent color in the far-tier MultiMesh
/// buffer, or material parameters on the near-tier node — so geometry is shared.
///
/// Each profile mesh has three surfaces, composited from many small primitive pieces by
/// <see cref="SkycraftMeshComposer"/>:
///   0 — chassis (dark metallic <see cref="StandardMaterial3D"/>)
///   1 — emissive trim (custom <c>skycraft_emissive.gdshader</c>, reads MultiMesh per-instance COLOR)
///   2 — glass canopy (transparent tinted <see cref="StandardMaterial3D"/>)
/// </summary>
public static class SkycraftMeshFactory
{
    private const int SurfChassis = 0;
    private const int SurfTrim = 1;
    private const int SurfCanopy = 2;

    private static Shader? _trimShader;
    private static Shader TrimShader => _trimShader ??=
        GD.Load<Shader>("res://shaders/skycraft_emissive.gdshader");

    /// <summary>
    /// Builds the body mesh for a profile. Silhouette family:
    /// Express → dart, Commuter → multi-rotor pod, Drifter → faceted hovercar.
    /// </summary>
    public static Mesh BuildBody(SkycraftProfile profile, Color accent)
    {
        return profile switch
        {
            SkycraftProfile.Express => BuildExpress(accent),
            SkycraftProfile.Commuter => BuildCommuter(accent),
            SkycraftProfile.Drifter => BuildDrifter(accent),
            _ => BuildCommuter(accent),
        };
    }

    // -----------------------------------------------------------------
    // Express — sleek aerial wedge (image 3 reference).
    // Forward = -Z (matches SkycraftNode headlight convention).
    // Silhouette: ~5m fuselage + 2m nose, 3.2m wingspan, 0.7m tail height.
    // -----------------------------------------------------------------
    private static Mesh BuildExpress(Color accent)
    {
        var c = new SkycraftMeshComposer();

        // Chassis: spine, hull underplate, nose taper, wings, tail fin.
        c.Add(new BoxMesh { Size = new Vector3(1.2f, 0.5f, 5.0f) },
            T(0, 0, 0), SurfChassis);
        c.Add(new BoxMesh { Size = new Vector3(1.5f, 0.18f, 4.4f) },
            T(0, -0.28f, 0.1f), SurfChassis);
        c.Add(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.6f, Height = 2.0f, RadialSegments = 12, Rings = 1 },
            TR(0, 0, -3.5f, -Mathf.Pi / 2f, 0, 0), SurfChassis);
        c.Add(new BoxMesh { Size = new Vector3(3.2f, 0.08f, 0.9f) },
            T(0, -0.1f, 0.8f), SurfChassis);
        c.Add(new BoxMesh { Size = new Vector3(0.08f, 0.7f, 1.0f) },
            T(0, 0.6f, 2.0f), SurfChassis);

        // Canopy: stretched bubble.
        c.Add(new SphereMesh { Radius = 0.5f, Height = 1.0f, RadialSegments = 18, Rings = 9 },
            TS(0, 0.3f, -1.0f, 1.0f, 0.55f, 1.8f), SurfCanopy);

        // Emissive trim: ventral strip, side seams, wing leading edge, exhaust ring.
        c.Add(new BoxMesh { Size = new Vector3(0.18f, 0.04f, 4.4f) },
            T(0, -0.38f, 0.1f), SurfTrim);
        c.Add(new BoxMesh { Size = new Vector3(0.03f, 0.04f, 4.4f) },
            T(0.62f, -0.08f, 0.1f), SurfTrim);
        c.Add(new BoxMesh { Size = new Vector3(0.03f, 0.04f, 4.4f) },
            T(-0.62f, -0.08f, 0.1f), SurfTrim);
        c.Add(new BoxMesh { Size = new Vector3(3.0f, 0.03f, 0.05f) },
            T(0, -0.06f, 0.36f), SurfTrim);
        c.Add(new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.36f, Height = 0.08f, RadialSegments = 16, Rings = 1 },
            TR(0, -0.05f, 2.55f, -Mathf.Pi / 2f, 0, 0), SurfTrim);

        return c.Build(SurfaceMaterials(accent));
    }

    // -----------------------------------------------------------------
    // Commuter — stealth-wedge taxi: central ovoid pod pierced by three
    // staggered horizontal triangular wings, sharp tips converging forward.
    // -----------------------------------------------------------------
    private static Mesh BuildCommuter(Color accent)
    {
        var c = new SkycraftMeshComposer();

        // Central pod (stretched ovoid). Narrow X + short Y so the pod reads as a
        // slim cabin pierced by the wings rather than a fat blob.
        c.Add(new SphereMesh { Radius = 1.0f, Height = 2.0f, RadialSegments = 20, Rings = 10 },
            TS(0, 0, 0, 0.95f, 0.7f, 1.6f), SurfChassis);

        // Three staggered horizontal triangular wings, all sharp tips pointing -Z
        // (forward, per the Express convention). Z-centers are chosen so all three
        // tips converge at z=-1.80 (just past the body nose at z=-1.60), while back
        // edges stagger into a stepped trailing-edge silhouette: bottom wing widest
        // and furthest back, top wing narrowest and furthest forward. Each wing
        // passes through the pod (y inside body Y-range ±0.70) but its corners
        // emerge well past body X (back corners at x=±r·√3/2 vs body X ≤ 0.95).
        AddCommuterTriangle(c, y: -0.30f, z:  0.00f, r: 2.50f); // bottom (largest)
        AddCommuterTriangle(c, y:  0.00f, z: -0.30f, r: 2.20f); // middle
        AddCommuterTriangle(c, y:  0.30f, z: -0.60f, r: 1.90f); // top (smallest)

        // Forward-leaning canopy bubble.
        c.Add(new SphereMesh { Radius = 0.55f, Height = 1.1f, RadialSegments = 18, Rings = 9 },
            TS(0, 0.55f, -0.4f, 1.0f, 0.7f, 1.2f), SurfCanopy);

        // Rear backlight pods.
        c.Add(new BoxMesh { Size = new Vector3(0.18f, 0.08f, 0.3f) },
            T(-0.55f, -0.05f, 1.0f), SurfTrim);
        c.Add(new BoxMesh { Size = new Vector3(0.18f, 0.08f, 0.3f) },
            T(0.55f, -0.05f, 1.0f), SurfTrim);

        return c.Build(SurfaceMaterials(accent));
    }

    private static void AddCommuterTriangle(SkycraftMeshComposer c, float y, float z, float r)
    {
        // CylinderMesh with RadialSegments=3 makes an equilateral triangular prism;
        // small Height makes it a flat plate. Godot's CylinderMesh places its first
        // radial vertex at +Z (its source computes x=sin(u·τ), z=cos(u·τ) with u=0
        // for vertex 0 — *not* the +X start you'd expect from polar conventions).
        // A π yaw flips the +Z-default tip vertex to -Z (forward, per Express).
        // Post-rotation vertex positions, relative to (0, y, z):
        //   tip        (        0, ±h/2,  -r  )  ← sharp leading edge at -Z
        //   back-right (+r·√3/2, ±h/2, +r/2)
        //   back-left  (-r·√3/2, ±h/2, +r/2)
        var basis = Basis.FromEuler(new Vector3(0, Mathf.Pi, 0));
        c.Add(new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.10f, RadialSegments = 3, Rings = 1 },
            new Transform3D(basis, new Vector3(0, y, z)), SurfChassis);

        // Emissive trailing-edge strip: thin glowing box flush behind the prism's
        // back face (the rectangular face spanning the two back vertices, which sits
        // in the plane z = z_center + r/2). Strip is sized to the back face exactly:
        // X-length = r·√3 (the back edge), Y-height = 0.10 (the prism's full height).
        // 0.03m Z-offset past the chassis face avoids shared-surface z-fighting.
        float backFaceZ = z + r * 0.5f;
        c.Add(new BoxMesh { Size = new Vector3(r * Mathf.Sqrt(3f), 0.10f, 0.04f) },
            T(0, y, backFaceZ + 0.03f), SurfTrim);
    }

    // -----------------------------------------------------------------
    // Drifter — faceted hovercar (image 2 reference).
    // Tiered low wedge + rear thruster pods + neon waistlines.
    // -----------------------------------------------------------------
    private static Mesh BuildDrifter(Color accent)
    {
        var c = new SkycraftMeshComposer();

        // Tiered hull: lower wide base + upper narrower deck + tail wing.
        c.Add(new BoxMesh { Size = new Vector3(2.0f, 0.4f, 3.6f) },
            T(0, -0.2f, 0), SurfChassis);
        c.Add(new BoxMesh { Size = new Vector3(1.7f, 0.35f, 2.8f) },
            T(0, 0.18f, 0.1f), SurfChassis);
        c.Add(new BoxMesh { Size = new Vector3(2.2f, 0.05f, 0.4f) },
            T(0, 0.5f, 1.8f), SurfChassis);

        // Rear thruster pods (cylinders along Z). Pod center at z=2.15 places the pod
        // range at [1.85, 2.45] — 5cm behind the lower hull's rear face (z=1.8) so the
        // cylinder walls don't pierce the hull's top/bottom faces. Original design had
        // the pod at z=1.85, which interpenetrated.
        const float PodZ = 2.15f;
        const float PodLen = 0.6f;
        const float PodRearR = 0.32f;
        const float PodFrontR = 0.30f;
        c.Add(new CylinderMesh { TopRadius = PodFrontR, BottomRadius = PodRearR, Height = PodLen, RadialSegments = 14, Rings = 1 },
            TR(-0.78f, -0.1f, PodZ, -Mathf.Pi / 2f, 0, 0), SurfChassis);
        c.Add(new CylinderMesh { TopRadius = PodFrontR, BottomRadius = PodRearR, Height = PodLen, RadialSegments = 14, Rings = 1 },
            TR(0.78f, -0.1f, PodZ, -Mathf.Pi / 2f, 0, 0), SurfChassis);

        // Pod accent rings: two emissive trim bands wrapped around each pod (front-quarter
        // and rear-quarter positions). InnerRadius 0.33 > pod's max radius 0.32 so the ring
        // sits proud of the pod surface; OuterRadius 0.38 = ~5cm band. Pi/2 X rotation stands
        // the torus up so its hole points along Z (around the pod axis).
        foreach (float podX in new[] { -0.78f, 0.78f })
        {
            c.Add(new TorusMesh { InnerRadius = 0.33f, OuterRadius = 0.38f, RingSegments = 24, Rings = 6 },
                TR(podX, -0.1f, PodZ - 0.15f, Mathf.Pi / 2f, 0, 0), SurfTrim);
            c.Add(new TorusMesh { InnerRadius = 0.33f, OuterRadius = 0.38f, RingSegments = 24, Rings = 6 },
                TR(podX, -0.1f, PodZ + 0.15f, Mathf.Pi / 2f, 0, 0), SurfTrim);
        }

        // Canopy: low-profile stretched dome over the front deck.
        c.Add(new SphereMesh { Radius = 0.6f, Height = 1.2f, RadialSegments = 18, Rings = 9 },
            TS(0, 0.5f, -0.5f, 1.0f, 0.45f, 1.5f), SurfCanopy);

        // Emissive trim: waistlines, front bar, underbelly strip.
        c.Add(new BoxMesh { Size = new Vector3(0.03f, 0.08f, 3.4f) },
            T(1.015f, -0.02f, 0), SurfTrim);
        c.Add(new BoxMesh { Size = new Vector3(0.03f, 0.08f, 3.4f) },
            T(-1.015f, -0.02f, 0), SurfTrim);
        c.Add(new BoxMesh { Size = new Vector3(0.8f, 0.06f, 0.05f) },
            T(0, 0.05f, -1.83f), SurfTrim);
        c.Add(new BoxMesh { Size = new Vector3(1.2f, 0.04f, 3.0f) },
            T(0, -0.42f, 0), SurfTrim);

        // Blue metallic chassis (Drifter only). Albedo tints the dielectric F0 toward blue
        // and shows through wherever environment reflections are weak; the Metallic=0.88 from
        // BuildChassisMaterial keeps most of the look reflective.
        return c.Build(SurfaceMaterials(accent, new Color(0.10f, 0.18f, 0.35f)));
    }

    // --- materials -----------------------------------------------------

    // Default chassis: near-black blue-grey. Drifter overrides with a richer blue (see
    // BuildDrifter). Pass `chassisColor` to override per-profile.
    private static IReadOnlyDictionary<int, Material> SurfaceMaterials(Color accent, Color? chassisColor = null) =>
        new Dictionary<int, Material>
        {
            [SurfChassis] = BuildChassisMaterial(chassisColor ?? new Color(0.07f, 0.08f, 0.11f)),
            [SurfTrim] = BuildTrimMaterial(accent),
            [SurfCanopy] = BuildCanopyMaterial(accent),
        };

    private static StandardMaterial3D BuildChassisMaterial(Color albedo) => new()
    {
        AlbedoColor = albedo,
        Metallic = 0.88f,
        MetallicSpecular = 0.65f,
        Roughness = 0.28f,
    };

    private static ShaderMaterial BuildTrimMaterial(Color accent)
    {
        var mat = new ShaderMaterial { Shader = TrimShader };
        mat.SetShaderParameter("base_emission", new Vector3(accent.R, accent.G, accent.B));
        mat.SetShaderParameter("energy", 4.5f);
        return mat;
    }

    /// <summary>
    /// Drops the static shader caches. Called from <c>SkycraftRenderer.Cleanup</c> so a
    /// subsequent <c>SkycraftSystemSetup.Configure</c> (e.g. world recreate, test harness)
    /// re-resolves shaders cleanly. Resolving from disk on next use is cheap.
    /// </summary>
    public static void ResetCaches()
    {
        _trimShader = null;
    }

    private static StandardMaterial3D BuildCanopyMaterial(Color accent) => new()
    {
        AlbedoColor = new Color(accent.R * 0.25f, accent.G * 0.25f, accent.B * 0.25f, 0.55f),
        Metallic = 0.45f,
        Roughness = 0.08f,
        EmissionEnabled = true,
        Emission = accent,
        EmissionEnergyMultiplier = 0.35f,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    // --- transform helpers --------------------------------------------

    private static Transform3D T(float x, float y, float z) =>
        new(Basis.Identity, new Vector3(x, y, z));

    private static Transform3D TR(float x, float y, float z, float rx, float ry, float rz) =>
        new(Basis.FromEuler(new Vector3(rx, ry, rz)), new Vector3(x, y, z));

    private static Transform3D TS(float x, float y, float z, float sx, float sy, float sz) =>
        new(Basis.Identity.Scaled(new Vector3(sx, sy, sz)), new Vector3(x, y, z));
}
