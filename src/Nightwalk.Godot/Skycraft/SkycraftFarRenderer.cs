using System;
using Godot;
using Nightwalk.Core.Skycraft;

namespace Nightwalk.Godot.Skycraft;

/// <summary>
/// Far-tier renderer: a single shared <see cref="MultiMeshInstance3D"/> per profile.
/// All per-instance variation rides on the packed transform + color buffer, per
/// ADR-0016 §Visual identity ("All within-profile variation is material-only").
///
/// Per profile the MultiMesh buffer is sized to <c>maxPoolSize</c> (not the per-profile
/// sub-budget) so a hot-reload of <c>[mix]</c> can shift ratios without overflowing any
/// profile's slots. Each frame, EndFrame sets <c>VisibleInstanceCount</c> to the number
/// actually written — unrendered slots can hold any stale transform.
/// </summary>
public sealed partial class SkycraftFarRenderer : Node3D
{
    private readonly MultiMeshInstance3D[] _byProfile = new MultiMeshInstance3D[3];
    private readonly int[] _instanceCount = new int[3];

    // Last colour written into each slot, per profile. We compare against this before
    // calling SetInstanceColor — accent tint is fixed per Skycraft (assigned once at
    // spawn) and the active-list order is stable except when an instance despawns, so in
    // steady state every slot's colour is already correct after the first write. The
    // sentinel uses alpha=-1 so the first real write always fires.
    private readonly Color[]?[] _lastColors = new Color[3][];

    /// <summary>
    /// Configures one MultiMesh per profile. Each buffer is sized to <c>maxPoolSize</c>
    /// (not the per-profile sub-budget) so a hot-reload of <c>[mix]</c> ratios can rebalance
    /// the active set without overflowing any profile's slots. The extra slots cost ~23 KB
    /// total — negligible against the interop savings of never reallocating.
    /// </summary>
    public void Configure(int maxPoolSize, Mesh[] bodyMeshes)
    {
        for (int p = 0; p < 3; p++)
        {
            var profile = (SkycraftProfile)p;
            int perProfile = Math.Max(1, maxPoolSize);

            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = bodyMeshes[p],
                InstanceCount = perProfile,
                // Render the first N instances where N is set per-frame by EndFrame. Skips
                // both the startup pre-zero loop (was 2 × maxPerProfile interop calls × 3
                // profiles) and per-frame tail-clearing — unrendered slots can have any
                // transform value, they won't draw.
                VisibleInstanceCount = 0,
            };

            var node = new MultiMeshInstance3D
            {
                Name = $"SkycraftFar_{profile}",
                Multimesh = mm,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // Skycraft transforms are advanced from _Process (render-time), not _PhysicsProcess.
                // Without this, Godot warns each time SetInstanceTransform fires outside the
                // physics tick. Physics interpolation isn't useful for an ambient system whose
                // own movement is already smoothed in the controller.
                PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off,
            };
            AddChild(node);
            _byProfile[p] = node;

            var colors = new Color[perProfile];
            // Sentinel: alpha=-1 guarantees the first real write always passes the inequality.
            var sentinel = new Color(0f, 0f, 0f, -1f);
            for (int i = 0; i < perProfile; i++) colors[i] = sentinel;
            _lastColors[p] = colors;
        }
    }

    /// <summary>
    /// Begin a frame's batch for a profile — resets the per-profile write cursor.
    /// </summary>
    public void BeginFrame(SkycraftProfile profile)
    {
        _instanceCount[(int)profile] = 0;
    }

    /// <summary>
    /// Writes one far-tier instance. Returns false if the per-profile buffer is full —
    /// since every profile is sized to <c>maxPoolSize</c> this should be unreachable in
    /// practice; the return is kept as a defensive guard rather than an expected branch.
    /// </summary>
    public bool WriteInstance(SkycraftProfile profile, in Transform3D transform, in Color tint)
    {
        var mm = _byProfile[(int)profile].Multimesh;
        int idx = _instanceCount[(int)profile];
        if (idx >= mm.InstanceCount) return false;
        mm.SetInstanceTransform(idx, transform);
        // Skip the colour write when the slot already holds this tint. Accent jitter is
        // assigned once at spawn and never changes, so in steady state we only call
        // SetInstanceColor when the active list reorders (despawn shifts slots down). At
        // density=100 this halves the far-tier interop call count from 2×N to ~N per frame.
        var last = _lastColors[(int)profile]!;
        if (last[idx] != tint)
        {
            mm.SetInstanceColor(idx, tint);
            last[idx] = tint;
        }
        _instanceCount[(int)profile] = idx + 1;
        return true;
    }

    /// <summary>
    /// After all writes for the frame, clip the rendered range to the slots actually
    /// written. Unwritten slots beyond the cursor are not drawn regardless of their
    /// transform value — one interop call per profile instead of a tail-zero loop.
    /// </summary>
    public void EndFrame(SkycraftProfile profile)
    {
        var mm = _byProfile[(int)profile].Multimesh;
        mm.VisibleInstanceCount = _instanceCount[(int)profile];
    }

}
