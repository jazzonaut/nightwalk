using Godot;

namespace Nightwalk.Godot.Util;

/// <summary>
/// Smooth transition using SmoothStep easing.
/// Supports both position-only and full transform interpolation.
/// </summary>
public struct SmoothTransition
{
    public bool IsActive;
    public float Elapsed;
    public float Duration;
    public Transform3D Start;
    public Transform3D Target;

    /// <summary>
    /// Begins a position-only transition (rotation stays identity).
    /// </summary>
    public void Begin(Vector3 start, Vector3 target, float duration)
    {
        Start = new Transform3D(Basis.Identity, start);
        Target = new Transform3D(Basis.Identity, target);
        Duration = Mathf.Max(0.001f, duration);
        Elapsed = 0f;
        IsActive = true;
    }

    /// <summary>
    /// Begins a full transform transition (position and rotation).
    /// </summary>
    public void Begin(Transform3D start, Transform3D target, float duration)
    {
        Start = start;
        Target = target;
        Duration = Mathf.Max(0.001f, duration);
        Elapsed = 0f;
        IsActive = true;
    }

    /// <summary>
    /// Steps the transition and returns the interpolated position.
    /// </summary>
    public bool TryStepPosition(float dt, out Vector3 position)
    {
        if (!IsActive)
        {
            position = default;
            return false;
        }

        Elapsed += dt;
        float blend = Mathf.Clamp(Elapsed / Duration, 0f, 1f);
        float eased = Mathf.SmoothStep(0f, 1f, blend);
        position = Start.Origin.Lerp(Target.Origin, eased);

        if (blend >= 1f)
            IsActive = false;

        return true;
    }

    /// <summary>
    /// Steps the transition and returns the interpolated transform.
    /// </summary>
    public bool TryStepTransform(float dt, out Transform3D transform)
    {
        if (!IsActive)
        {
            transform = default;
            return false;
        }

        Elapsed += dt;
        float blend = Mathf.Clamp(Elapsed / Duration, 0f, 1f);
        float eased = Mathf.SmoothStep(0f, 1f, blend);
        transform = Start.InterpolateWith(Target, eased);

        if (blend >= 1f)
            IsActive = false;

        return true;
    }

    public void Cancel()
    {
        IsActive = false;
    }
}
