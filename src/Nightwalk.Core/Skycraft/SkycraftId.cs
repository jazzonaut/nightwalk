using System;

namespace Nightwalk.Core.Skycraft;

/// <summary>
/// Stable identifier for a Skycraft instance. Survives pool reuse — events and
/// future hitching code reference Skycraft by Id, not by position in the pool.
/// </summary>
public readonly struct SkycraftId : IEquatable<SkycraftId>
{
    public readonly Guid Value;

    public SkycraftId(Guid value) => Value = value;

    public static SkycraftId NewId() => new(Guid.NewGuid());

    public bool Equals(SkycraftId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is SkycraftId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    /// <summary>
    /// Short (8-char) Guid prefix for log readability. Not unique — use <see cref="Value"/>
    /// directly if you need a stable identifier.
    /// </summary>
    public override string ToString() => Value.ToString("N")[..8];

    public static bool operator ==(SkycraftId a, SkycraftId b) => a.Equals(b);
    public static bool operator !=(SkycraftId a, SkycraftId b) => !a.Equals(b);
}
