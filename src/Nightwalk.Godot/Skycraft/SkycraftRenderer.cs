using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.Skycraft;
using Nightwalk.Core.Skycraft.Events;

namespace Nightwalk.Godot.Skycraft;

/// <summary>
/// Top-level Skycraft renderer. Subscribes to lifecycle events from
/// <see cref="SkycraftController"/> and per-frame walks the active set, choosing
/// between the near-tier <see cref="SkycraftNode"/> path (real lights, full Node3D)
/// and the far-tier <see cref="SkycraftFarRenderer"/> path (batched MultiMesh) with
/// a hysteresis margin to prevent flicker at the tier boundary.
/// </summary>
public sealed partial class SkycraftRenderer : Node3D
{
    private SkycraftController _controller = null!;
    private SkycraftConfig _config = null!;
    private IEventBus _eventBus = null!;

    private SkycraftFarRenderer _far = null!;

    // One body mesh per profile, built once and shared between every near-tier node and the
    // far-tier MultiMesh of that profile. Per ADR-0016 §Visual identity: "three meshes total
    // — one per profile" — so the geometry resource is profile-keyed, not instance-keyed.
    private readonly Mesh[] _bodyMeshes = new Mesh[3];

    // Pool of near-tier nodes per profile (the per-profile mesh forces a per-profile pool).
    private readonly Stack<SkycraftNode>[] _nearPool = new[]
    {
        new Stack<SkycraftNode>(),
        new Stack<SkycraftNode>(),
        new Stack<SkycraftNode>(),
    };
    private readonly Dictionary<SkycraftId, SkycraftNode> _nearActive = new();

    private Vector3 _cameraPosition;

    public void Initialize(SkycraftController controller, SkycraftConfig config, IEventBus eventBus)
    {
        _controller = controller;
        _config = config;
        _eventBus = eventBus;

        // Build the three body meshes once. Shared between near-tier nodes (one Mesh resource
        // referenced by every SkycraftNode of the profile) and the far-tier MultiMesh.
        for (int p = 0; p < 3; p++)
        {
            var profile = (SkycraftProfile)p;
            _bodyMeshes[p] = SkycraftMeshFactory.BuildBody(profile, AccentFor(profile));
        }

        _far = new SkycraftFarRenderer { Name = "SkycraftFar" };
        AddChild(_far);
        _far.Configure(config.Pool.MaxPoolSize, _bodyMeshes);

        _eventBus.Subscribe<SkycraftSpawnedEvent>(OnSpawned);
        _eventBus.Subscribe<SkycraftDespawningEvent>(OnDespawning);
        _eventBus.Subscribe<SkycraftLandedEvent>(OnLanded);
        _eventBus.Subscribe<SkycraftTakingOffEvent>(OnTakingOff);

        Log.Info(LogCategory.Sim, "SkycraftRenderer initialized");
    }

    public void SetCameraPosition(Vector3 cameraPosition) => _cameraPosition = cameraPosition;

    /// <summary>
    /// Picks up new render-tier and tier-swap distance values from a reloaded config.
    /// Hot-reload coverage by knob:
    ///   • Tier-swap distance + hysteresis — applied on the next frame.
    ///   • Far-tier accent jitter — applied per-instance on the next frame via the
    ///     per-instance COLOR delta in <c>WriteFar</c>.
    ///   • Near-tier accent colour, light energies, headlight cone — baked into the
    ///     shared per-profile mesh material and into each near-tier node at
    ///     <c>Configure</c> time. These DO NOT hot-reload; they take effect only after
    ///     a full session reset rebuilds the renderer (e.g. world recreate).
    /// </summary>
    public void Reconfigure(SkycraftConfig config)
    {
        _config = config;
    }

    private bool _farCleared = true;

    public override void _Process(double delta)
    {
        if (_controller == null) return;

        // Density=0 hard off-switch: when the system is dormant (no active crafts AND no
        // pool budget) we skip the tier-swap loop and the BeginFrame/EndFrame interop
        // pokes entirely. Honours ADR-0016 §Consequences ("zero perf cost at Density=0").
        // _nearActive guards the transient frames after the slider drops where ActiveCount
        // has reached zero but near-tier demotes haven't fully drained yet.
        if (_controller.EffectivePoolCap == 0 && _controller.ActiveCount == 0 && _nearActive.Count == 0)
        {
            // One-shot tail-clear: on the transition into the dormant state the previous
            // frame's EndFrame left VisibleInstanceCount > 0, which would freeze stale
            // far-tier transforms on screen. Push 0 once (BeginFrame zeroes the cursor,
            // EndFrame writes it through), then go fully idle.
            if (!_farCleared)
            {
                for (int p = 0; p < 3; p++)
                {
                    _far.BeginFrame((SkycraftProfile)p);
                    _far.EndFrame((SkycraftProfile)p);
                }
                _farCleared = true;
            }
            return;
        }
        _farCleared = false;

        float nearIn = _config.Render.NearTierDistance - _config.Render.NearTierHysteresis;
        float nearOut = _config.Render.NearTierDistance + _config.Render.NearTierHysteresis;
        float nearInSq = nearIn * nearIn;
        float nearOutSq = nearOut * nearOut;

        for (int p = 0; p < 3; p++) _far.BeginFrame((SkycraftProfile)p);

        var instances = _controller.ActiveInstances;
        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];
            var pos = new Vector3(inst.X, inst.Y, inst.Z);
            float distSq = (pos - _cameraPosition).LengthSquared();

            // _nearActive doubles as the "is this craft in the near tier?" set — no
            // separate HashSet needed since dictionary key lookup is O(1).
            bool wasNear = _nearActive.ContainsKey(inst.Id);
            bool nowNear = wasNear ? distSq <= nearOutSq : distSq <= nearInSq;

            if (nowNear)
            {
                if (!wasNear) PromoteToNear(inst);
                UpdateNear(inst, pos);
            }
            else
            {
                if (wasNear) DemoteToFar(inst.Id, inst.Profile);
                WriteFar(inst, pos);
            }
        }

        for (int p = 0; p < 3; p++) _far.EndFrame((SkycraftProfile)p);
    }

    private void PromoteToNear(SkycraftInstance inst)
    {
        var node = RentNearNode(inst.Profile);
        var fwd = new Vector3(inst.ForwardX, inst.ForwardY, inst.ForwardZ);
        if (fwd.LengthSquared() < 1e-4f) fwd = new Vector3(0f, 0f, 1f);
        node.AssignInstance(inst.Id, new Vector3(inst.X, inst.Y, inst.Z), fwd, inst.ScaleJitter);
        _nearActive[inst.Id] = node;
    }

    private void DemoteToFar(SkycraftId id, SkycraftProfile profile)
    {
        if (!_nearActive.TryGetValue(id, out var node)) return;
        _nearActive.Remove(id);
        node.Release();
        _nearPool[(int)profile].Push(node);
    }

    private void UpdateNear(SkycraftInstance inst, Vector3 pos)
    {
        if (!_nearActive.TryGetValue(inst.Id, out var node)) return;
        var fwd = new Vector3(inst.ForwardX, inst.ForwardY, inst.ForwardZ);
        if (fwd.LengthSquared() < 1e-4f) fwd = new Vector3(0f, 0f, 1f);
        node.UpdateTransform(pos, fwd);
        node.SetDwelling(inst.State == SkycraftState.Dwelling);
    }

    private void WriteFar(SkycraftInstance inst, Vector3 pos)
    {
        // Build a Transform3D oriented along the forward vector. Reused logic from SkycraftNode
        // but inline for the far path (no per-instance node, just a transform write).
        var fwd = new Vector3(inst.ForwardX, inst.ForwardY, inst.ForwardZ);
        if (fwd.LengthSquared() < 1e-4f) fwd = new Vector3(0f, 0f, 1f);
        var basis = Basis.LookingAt(fwd, Vector3.Up).Scaled(new Vector3(inst.ScaleJitter, inst.ScaleJitter, inst.ScaleJitter));
        var t = new Transform3D(basis, pos);

        // Per-instance tint is a *delta multiplier* against the material's baked base_emission
        // (which is the profile accent). Writing the final jittered color directly would
        // double-multiply through `base_emission * instance_tint` in the shader, squaring
        // the chromaticity and dimming the far tier. With the delta, near-tier (COLOR=white,
        // delta=1) and far-tier (COLOR=delta) both render the intended emission color.
        var accent = AccentFor(inst.Profile);
        var tinted = ShiftHue(accent, inst.AccentHueShiftDegrees);
        var delta = TintDelta(accent, tinted);
        _far.WriteInstance(inst.Profile, t, delta);
    }

    private static Color TintDelta(Color accent, Color tinted)
    {
        // Per-channel ratio that, when multiplied by the baked accent in the shader, yields
        // the jittered color. Epsilon guard avoids blowing up on near-zero channels (the
        // three profile accents all have channels > 0.3 in practice, so this is defensive).
        const float Epsilon = 0.01f;
        return new Color(
            accent.R > Epsilon ? tinted.R / accent.R : 1f,
            accent.G > Epsilon ? tinted.G / accent.G : 1f,
            accent.B > Epsilon ? tinted.B / accent.B : 1f,
            1f);
    }

    private SkycraftNode RentNearNode(SkycraftProfile profile)
    {
        var pool = _nearPool[(int)profile];
        if (pool.Count > 0)
        {
            return pool.Pop();
        }
        var node = new SkycraftNode { Name = $"Skycraft_{profile}" };
        node.Configure(_bodyMeshes[(int)profile], _config.Render);
        AddChild(node);
        return node;
    }

    private Color AccentFor(SkycraftProfile profile) => profile switch
    {
        SkycraftProfile.Express => Color.FromHtml(_config.Express.AccentColor),
        SkycraftProfile.Commuter => Color.FromHtml(_config.Commuter.AccentColor),
        SkycraftProfile.Drifter => Color.FromHtml(_config.Drifter.AccentColor),
        _ => Colors.White,
    };

    private static Color ShiftHue(Color c, float degrees)
    {
        if (Mathf.IsZeroApprox(degrees)) return c;
        // Convert to HSV, shift hue, convert back. Godot's Color exposes H/S/V directly.
        float h = c.H + degrees / 360f;
        h -= Mathf.Floor(h);
        return Color.FromHsv(h, c.S, c.V, c.A);
    }

    // --- lifecycle events ---------------------------------------------------

    private void OnSpawned(SkycraftSpawnedEvent e)
    {
        // Reserved for one-shot spawn cues (SFX, particles). Per-frame state read in
        // _Process drives the visual lifecycle, so no per-instance bookkeeping is needed here.
    }

    private void OnDespawning(SkycraftDespawningEvent e)
    {
        if (_nearActive.TryGetValue(e.Id, out var node))
        {
            _nearActive.Remove(e.Id);
            node.Release();
            _nearPool[(int)e.Profile].Push(node);
        }
    }

    private void OnLanded(SkycraftLandedEvent e)
    {
        // Renderer-side hook: SetDwelling is also applied by per-frame state read; this is
        // an opportunity to play a one-shot SFX or particles in the future.
    }

    private void OnTakingOff(SkycraftTakingOffEvent e)
    {
        // Symmetric to OnLanded — reserved for one-shot takeoff cues.
    }

    public void Cleanup()
    {
        _eventBus.Unsubscribe<SkycraftSpawnedEvent>(OnSpawned);
        _eventBus.Unsubscribe<SkycraftDespawningEvent>(OnDespawning);
        _eventBus.Unsubscribe<SkycraftLandedEvent>(OnLanded);
        _eventBus.Unsubscribe<SkycraftTakingOffEvent>(OnTakingOff);

        foreach (var node in _nearActive.Values) node.QueueFree();
        _nearActive.Clear();
        for (int p = 0; p < 3; p++)
        {
            while (_nearPool[p].Count > 0)
            {
                var n = _nearPool[p].Pop();
                n.QueueFree();
            }
        }

        // Drop the static MeshFactory shader caches so the next renderer instance
        // re-resolves shaders cleanly — see SkycraftMeshFactory.ResetCaches.
        SkycraftMeshFactory.ResetCaches();
    }
}
