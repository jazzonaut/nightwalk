using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Beacons;

/// <summary>
/// Renders a signal beam path as connected cylinder segments.
/// Uses pooled mesh instances to avoid per-frame allocations during beam updates.
/// </summary>
public sealed partial class SignalBeamRenderer : Node3D
{
    private readonly List<MeshInstance3D> _activeSegments = new();
    private readonly List<MeshInstance3D> _segmentPool = new();
    private readonly List<MeshInstance3D> _activeJoints = new();
    private readonly List<MeshInstance3D> _jointPool = new();
    private ShaderMaterial? _beamMaterial;
    private ShaderMaterial? _brokenBeamMaterial;
    private SphereMesh? _jointMesh;
    private BeaconSettings _config = null!;
    private Color _beamColor = Colors.Cyan;

    /// <summary>
    /// Total distance of the current beam path in meters.
    /// </summary>
    public float TotalDistance { get; private set; }

    /// <summary>
    /// Configures the renderer with settings.
    /// </summary>
    public void Configure(BeaconSettings config)
    {
        _config = config;
        Name = "SignalBeamRenderer";

        // Load the signal beam shader
        var shader = GD.Load<Shader>("res://shaders/signal_beam.gdshader");
        _beamMaterial = new ShaderMaterial { Shader = shader };
        _brokenBeamMaterial = new ShaderMaterial { Shader = shader };
        UpdateMaterialColor();

        // Create shared sphere mesh for joints
        _jointMesh = new SphereMesh
        {
            Radius = config.SignalBeamWidth * 0.5f,
            Height = config.SignalBeamWidth * 1.2f,
            RadialSegments = 8,
            Rings = 4
        };
    }

    /// <summary>
    /// Sets the beam color (typically from the active frequency).
    /// </summary>
    public void SetColor(Color color)
    {
        _beamColor = color;
        UpdateMaterialColor();
    }

    private void UpdateMaterialColor()
    {
        _beamMaterial?.SetShaderParameter("beam_color", _beamColor);
        _beamMaterial?.SetShaderParameter("intensity", 2.0f);

        _brokenBeamMaterial?.SetShaderParameter("beam_color", _beamColor);
        _brokenBeamMaterial?.SetShaderParameter("intensity", 2.0f * _config.BrokenBeamOpacity);
    }

    /// <summary>
    /// Updates the beam path visualization.
    /// </summary>
    /// <param name="points">List of points defining the beam path.</param>
    /// <param name="isBroken">If true, the last segment uses reduced opacity (beam reached max length).</param>
    public void UpdatePath(IReadOnlyList<Vector3> points, bool isBroken = false)
    {
        // Return all active segments to pool
        foreach (var segment in _activeSegments)
        {
            segment.Visible = false;
            _segmentPool.Add(segment);
        }
        _activeSegments.Clear();

        // Return all active joints to pool
        foreach (var joint in _activeJoints)
        {
            joint.Visible = false;
            _jointPool.Add(joint);
        }
        _activeJoints.Clear();

        if (points.Count < 2)
        {
            TotalDistance = 0;
            return;
        }

        TotalDistance = 0;

        // Create/reuse a segment between each pair of points
        for (int i = 0; i < points.Count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            var segmentLength = start.DistanceTo(end);

            if (segmentLength < 0.01f)
                continue;

            TotalDistance += segmentLength;

            // Use broken material for the last segment if beam is broken
            bool isLastSegment = i == points.Count - 2;
            var useBrokenMaterial = isBroken && isLastSegment;
            ConfigureSegment(start, end, segmentLength, useBrokenMaterial);

            // Add joint sphere at intermediate points (where beams meet)
            // Joint is at segment start (prism/source), so never fade it
            ConfigureJoint(start);
        }
    }

    private void ConfigureSegment(Vector3 start, Vector3 end, float length, bool isBroken = false)
    {
        MeshInstance3D segment;

        // Reuse from pool or create new
        if (_segmentPool.Count > 0)
        {
            segment = _segmentPool[^1];
            _segmentPool.RemoveAt(_segmentPool.Count - 1);
            segment.Visible = true;

            // Update cylinder height
            if (segment.Mesh is CylinderMesh cylinder)
            {
                cylinder.Height = length;
            }
        }
        else
        {
            segment = new MeshInstance3D { Name = $"BeamSegment_{_activeSegments.Count}" };

            var cylinder = new CylinderMesh
            {
                TopRadius = _config.SignalBeamWidth / 2f,
                BottomRadius = _config.SignalBeamWidth / 2f,
                Height = length
            };
            segment.Mesh = cylinder;
            segment.MaterialOverride = _beamMaterial;
            segment.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

            AddChild(segment);
        }

        _activeSegments.Add(segment);

        // Use broken material for faded appearance
        segment.MaterialOverride = isBroken ? _brokenBeamMaterial : _beamMaterial;

        // Position at midpoint
        var midpoint = (start + end) / 2f;
        segment.GlobalPosition = midpoint;

        // Rotate to point from start to end
        var direction = (end - start).Normalized();

        // Choose up vector that isn't colinear with direction
        var up = Mathf.Abs(direction.Dot(Vector3.Up)) > 0.9f ? Vector3.Forward : Vector3.Up;

        segment.LookAt(segment.GlobalPosition + direction, up);
        segment.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);
    }

    private void ConfigureJoint(Vector3 position)
    {
        MeshInstance3D joint;

        // Reuse from pool or create new
        if (_jointPool.Count > 0)
        {
            joint = _jointPool[^1];
            _jointPool.RemoveAt(_jointPool.Count - 1);
            joint.Visible = true;
        }
        else
        {
            joint = new MeshInstance3D { Name = $"BeamJoint_{_activeJoints.Count}" };
            joint.Mesh = _jointMesh;
            joint.MaterialOverride = _beamMaterial;
            joint.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            AddChild(joint);
        }

        _activeJoints.Add(joint);
        joint.GlobalPosition = position;
    }

    /// <summary>
    /// Clears all beam segments (returns to pool).
    /// </summary>
    public void Clear()
    {
        foreach (var segment in _activeSegments)
        {
            segment.Visible = false;
            _segmentPool.Add(segment);
        }
        _activeSegments.Clear();

        foreach (var joint in _activeJoints)
        {
            joint.Visible = false;
            _jointPool.Add(joint);
        }
        _activeJoints.Clear();

        TotalDistance = 0;
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        // Free all pooled segments
        foreach (var segment in _segmentPool)
        {
            segment.QueueFree();
        }
        _segmentPool.Clear();

        foreach (var segment in _activeSegments)
        {
            segment.QueueFree();
        }
        _activeSegments.Clear();

        // Free all pooled joints
        foreach (var joint in _jointPool)
        {
            joint.QueueFree();
        }
        _jointPool.Clear();

        foreach (var joint in _activeJoints)
        {
            joint.QueueFree();
        }
        _activeJoints.Clear();

        _beamMaterial?.Dispose();
        _brokenBeamMaterial?.Dispose();
        _jointMesh?.Dispose();
    }
}
