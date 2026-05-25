using System;
using System.Collections.Generic;
using Godot;

namespace Nightwalk.Godot.Camera;

/// <summary>
/// Catmull-Rom spline path for smooth flyby camera movement.
/// Provides arc-length parameterization for constant-speed traversal.
/// </summary>
public sealed class FlybyPath
{
    private const int ArcLengthSamples = 100;

    private readonly Vector3[] _waypoints;
    private readonly float[] _arcLengthTable;
    private readonly float _totalLength;

    /// <summary>
    /// Total arc length of the path in meters.
    /// </summary>
    public float TotalLength => _totalLength;

    /// <summary>
    /// Number of waypoints in the path.
    /// </summary>
    public int WaypointCount => _waypoints.Length;

    /// <summary>
    /// Creates a flyby path from waypoints with vertical offset.
    /// </summary>
    /// <param name="waypoints">Ordered list of waypoints.</param>
    /// <param name="verticalOffset">Height to add above each waypoint.</param>
    public FlybyPath(IReadOnlyList<Vector3> waypoints, float verticalOffset)
    {
        if (waypoints.Count < 2)
            throw new ArgumentException("Path requires at least 2 waypoints", nameof(waypoints));

        // Apply vertical offset to waypoints
        _waypoints = new Vector3[waypoints.Count];
        for (int i = 0; i < waypoints.Count; i++)
        {
            _waypoints[i] = waypoints[i] + Vector3.Up * verticalOffset;
        }

        // Build arc-length parameterization table
        _arcLengthTable = new float[ArcLengthSamples + 1];
        _arcLengthTable[0] = 0f;

        var prev = GetRawPositionAtT(0f);
        for (int i = 1; i <= ArcLengthSamples; i++)
        {
            float t = (float)i / ArcLengthSamples;
            var curr = GetRawPositionAtT(t);
            _arcLengthTable[i] = _arcLengthTable[i - 1] + prev.DistanceTo(curr);
            prev = curr;
        }

        _totalLength = _arcLengthTable[ArcLengthSamples];
    }

    /// <summary>
    /// Gets position at arc-length parameterized t (0-1).
    /// Provides constant-speed movement along the path.
    /// </summary>
    public Vector3 GetPositionAtT(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        float rawT = ArcLengthToRawT(t * _totalLength);
        return GetRawPositionAtT(rawT);
    }

    /// <summary>
    /// Gets tangent direction at arc-length parameterized t (0-1).
    /// </summary>
    public Vector3 GetTangentAtT(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        float rawT = ArcLengthToRawT(t * _totalLength);
        return GetRawTangentAtT(rawT);
    }

    /// <summary>
    /// Gets curvature at arc-length parameterized t (0-1).
    /// Higher values indicate sharper turns.
    /// </summary>
    public float GetCurvatureAtT(float t)
    {
        const float epsilon = 0.005f;
        t = Mathf.Clamp(t, 0f, 1f);

        // Compute curvature as the rate of change of tangent direction
        var t1 = GetTangentAtT(Mathf.Max(0f, t - epsilon));
        var t2 = GetTangentAtT(Mathf.Min(1f, t + epsilon));

        // Curvature = |dT/ds| where T is unit tangent and s is arc length
        var tangentDelta = t2 - t1;
        float arcDelta = epsilon * 2f * _totalLength;

        if (arcDelta < 0.001f)
            return 0f;

        return tangentDelta.Length() / arcDelta;
    }

    /// <summary>
    /// Gets the turn direction at t (-1 = left, +1 = right, based on world up).
    /// </summary>
    public float GetTurnDirectionAtT(float t)
    {
        const float epsilon = 0.01f;
        t = Mathf.Clamp(t, 0f, 1f);

        var tangent = GetTangentAtT(t);
        var tangentAhead = GetTangentAtT(Mathf.Min(1f, t + epsilon));

        // Cross product with world up to determine turn direction
        var cross = tangent.Cross(tangentAhead);
        return Mathf.Sign(cross.Y);
    }

    /// <summary>
    /// Gets raw position using Catmull-Rom interpolation (non-arc-length parameterized).
    /// </summary>
    private Vector3 GetRawPositionAtT(float t)
    {
        if (_waypoints.Length == 2)
        {
            // Simple linear interpolation for 2-point paths
            return _waypoints[0].Lerp(_waypoints[1], t);
        }

        // Map t to segment index
        float scaledT = t * (_waypoints.Length - 1);
        int segmentIndex = Mathf.Min((int)scaledT, _waypoints.Length - 2);
        float localT = scaledT - segmentIndex;

        // Get control points for Catmull-Rom (p0, p1, p2, p3)
        var p0 = GetControlPoint(segmentIndex - 1);
        var p1 = _waypoints[segmentIndex];
        var p2 = _waypoints[segmentIndex + 1];
        var p3 = GetControlPoint(segmentIndex + 2);

        return CatmullRom(p0, p1, p2, p3, localT);
    }

    /// <summary>
    /// Gets raw tangent direction (non-arc-length parameterized).
    /// </summary>
    private Vector3 GetRawTangentAtT(float t)
    {
        const float epsilon = 0.001f;
        var p1 = GetRawPositionAtT(Mathf.Max(0f, t - epsilon));
        var p2 = GetRawPositionAtT(Mathf.Min(1f, t + epsilon));
        var tangent = p2 - p1;
        return tangent.LengthSquared() > 0.0001f ? tangent.Normalized() : Vector3.Forward;
    }

    /// <summary>
    /// Gets control point, extrapolating for endpoints.
    /// </summary>
    private Vector3 GetControlPoint(int index)
    {
        if (index < 0)
        {
            // Extrapolate before first point
            var dir = _waypoints[0] - _waypoints[1];
            return _waypoints[0] + dir;
        }
        if (index >= _waypoints.Length)
        {
            // Extrapolate after last point
            var dir = _waypoints[^1] - _waypoints[^2];
            return _waypoints[^1] + dir;
        }
        return _waypoints[index];
    }

    /// <summary>
    /// Catmull-Rom spline interpolation.
    /// </summary>
    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    /// <summary>
    /// Converts arc length to raw parameter t using binary search.
    /// </summary>
    private float ArcLengthToRawT(float arcLength)
    {
        arcLength = Mathf.Clamp(arcLength, 0f, _totalLength);

        // Binary search in arc length table
        int low = 0;
        int high = ArcLengthSamples;

        while (low < high - 1)
        {
            int mid = (low + high) / 2;
            if (_arcLengthTable[mid] < arcLength)
                low = mid;
            else
                high = mid;
        }

        // Interpolate within the found segment
        float segmentArcStart = _arcLengthTable[low];
        float segmentArcEnd = _arcLengthTable[high];
        float segmentFraction = 0f;

        if (segmentArcEnd > segmentArcStart)
        {
            segmentFraction = (arcLength - segmentArcStart) / (segmentArcEnd - segmentArcStart);
        }

        return (low + segmentFraction) / ArcLengthSamples;
    }
}
