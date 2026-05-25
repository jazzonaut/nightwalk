using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Skycraft;

namespace Nightwalk.Godot.Skycraft;

/// <summary>
/// Near-tier Skycraft renderer: full Node3D with a real shadowless SpotLight3D
/// headlight, two backlight OmniLights, and a <see cref="RiderAnchor"/> marker for the
/// future hitching feature. Per ADR-0016 §2 headlight shadows are deliberately disabled
/// (cargo-cult risk from <see cref="Nightwalk.Godot.Resonance.SeekerNode"/> noted there).
/// </summary>
public sealed partial class SkycraftNode : Node3D
{
    private MeshInstance3D? _body;
    private SpotLight3D? _headlight;
    private OmniLight3D? _backlightL;
    private OmniLight3D? _backlightR;

    // Cruise / dwell light energies, read once from config at Configure. Stored on the node
    // so AssignInstance and SetDwelling restore the configured values rather than the
    // hard-coded literals that previously divorced the runtime from RenderSettings.
    private float _headlightCruiseEnergy = 4f;
    private float _headlightDwellEnergy = 0.2f;
    private float _backlightCruiseEnergy = 1.5f;
    private float _backlightDwellEnergy = 2.2f;

    // Cached so UpdateTransform can rebuild the basis without an interop read on
    // Transform.Basis.Scale, and so SetDwelling can no-op when state hasn't changed.
    private float _scaleJitter = 1f;
    private bool _dwelling;

    /// <summary>
    /// World-relative marker the future "press E to board" feature will read.
    /// Today no code consumes it; tomorrow the player camera attaches here.
    /// </summary>
    public Node3D? RiderAnchor { get; private set; }

    public SkycraftId InstanceId { get; private set; }

    /// <summary>
    /// Configures the node with a shared body mesh resource. The mesh is built once per
    /// profile by <see cref="SkycraftRenderer"/> and reused across every near-tier node of
    /// that profile, so pool warm-up doesn't pay the composer cost per allocation.
    /// </summary>
    public void Configure(Mesh bodyMesh, RenderSettings render)
    {
        _headlightCruiseEnergy = render.HeadlightEnergy;
        _headlightDwellEnergy = render.HeadlightDwellEnergy;
        _backlightCruiseEnergy = render.BacklightEnergy;
        _backlightDwellEnergy = render.BacklightDwellEnergy;

        _body = new MeshInstance3D
        {
            Name = "Body",
            Mesh = bodyMesh,
        };
        AddChild(_body);

        // Headlight — shadows OFF. Per ADR-0016 §2 this is the deliberate divergence from SeekerNode.
        var hlColor = Color.FromHtml(render.HeadlightColor);
        hlColor.A = 1f;
        _headlight = new SpotLight3D
        {
            Name = "Headlight",
            LightColor = hlColor,
            LightEnergy = render.HeadlightEnergy,
            SpotRange = render.HeadlightRange,
            SpotAngle = render.HeadlightConeDegrees,
            SpotAttenuation = 0.05f,
            SpotAngleAttenuation = 0.15f,
            ShadowEnabled = false,
        };
        // Headlight points down the local -Z axis (Godot SpotLight3D convention).
        // Mount slightly forward of the body center; tilt up a touch so cruise altitudes
        // illuminate building sides naturally.
        _headlight.Position = new Vector3(0f, -0.2f, -0.5f);
        _headlight.RotationDegrees = new Vector3(-5f, 0f, 0f);
        AddChild(_headlight);

        // Backlights (warm amber, no shadows).
        var blColor = Color.FromHtml(render.BacklightColor);
        blColor.A = 1f;
        _backlightL = new OmniLight3D
        {
            Name = "BacklightL",
            LightColor = blColor,
            LightEnergy = render.BacklightEnergy,
            OmniRange = render.BacklightRange,
            ShadowEnabled = false,
        };
        _backlightL.Position = new Vector3(-0.6f, 0f, 0.8f);
        AddChild(_backlightL);

        _backlightR = new OmniLight3D
        {
            Name = "BacklightR",
            LightColor = blColor,
            LightEnergy = render.BacklightEnergy,
            OmniRange = render.BacklightRange,
            ShadowEnabled = false,
        };
        _backlightR.Position = new Vector3(0.6f, 0f, 0.8f);
        AddChild(_backlightR);

        // Rider anchor — sits just above the hull, where a future player camera would mount.
        RiderAnchor = new Node3D
        {
            Name = "RiderAnchor",
            Position = new Vector3(0f, 0.8f, 0f),
        };
        AddChild(RiderAnchor);

        Visible = false;
    }

    public void AssignInstance(SkycraftId id, Vector3 position, Vector3 forward, float scaleJitter)
    {
        InstanceId = id;
        _scaleJitter = scaleJitter;
        WriteTransform(position, forward);
        // Reset dwelling state and force light energies to the cruise values; subsequent
        // SetDwelling calls will only touch interop when state changes.
        _dwelling = false;
        if (_headlight != null) _headlight.LightEnergy = _headlightCruiseEnergy;
        if (_backlightL != null) _backlightL.LightEnergy = _backlightCruiseEnergy;
        if (_backlightR != null) _backlightR.LightEnergy = _backlightCruiseEnergy;
        Visible = true;
    }

    public void UpdateTransform(Vector3 position, Vector3 forward)
    {
        WriteTransform(position, forward);
    }

    public void SetDwelling(bool dwelling)
    {
        // No-op when state hasn't changed — at 60fps × 20 near crafts that's ~3600 wasted
        // interop calls/sec the previous unconditional write was paying.
        if (dwelling == _dwelling) return;
        _dwelling = dwelling;
        if (_headlight != null) _headlight.LightEnergy = dwelling ? _headlightDwellEnergy : _headlightCruiseEnergy;
        if (_backlightL != null) _backlightL.LightEnergy = dwelling ? _backlightDwellEnergy : _backlightCruiseEnergy;
        if (_backlightR != null) _backlightR.LightEnergy = dwelling ? _backlightDwellEnergy : _backlightCruiseEnergy;
    }

    public void Release()
    {
        Visible = false;
        InstanceId = default;
    }

    // Single interop write per call: previously this path was
    //   GlobalPosition = pos          (interop set)
    //   ... read GlobalPosition       (interop get)
    //   Transform = new Transform3D(...)  (interop set)
    // The Transform write also wiped the Scale set in AssignInstance (LookingAt returns
    // an orthonormal basis), so near-tier crafts were silently rendering at scale 1.0
    // instead of their jittered value.
    private void WriteTransform(Vector3 position, Vector3 forward)
    {
        if (forward.LengthSquared() < 1e-4f) forward = new Vector3(0f, 0f, 1f);
        // Body front maps to local -Z (matches SpotLight3D convention).
        var basis = Basis.LookingAt(forward, Vector3.Up);
        if (_scaleJitter != 1f)
        {
            basis = basis.Scaled(new Vector3(_scaleJitter, _scaleJitter, _scaleJitter));
        }
        GlobalTransform = new Transform3D(basis, position);
    }
}
