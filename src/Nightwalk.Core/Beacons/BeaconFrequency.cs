namespace Nightwalk.Core.Beacons;

/// <summary>
/// Represents a beacon frequency. Beacons of the same frequency can sync.
/// </summary>
public readonly struct BeaconFrequency : IEquatable<BeaconFrequency>
{
    /// <summary>
    /// Frequency index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Number of available frequencies. Set from config at startup.
    /// </summary>
    public static int Count { get; set; }

    public BeaconFrequency(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Frequency must be 0-{Count - 1}");
        Index = index;
    }

    public bool Equals(BeaconFrequency other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is BeaconFrequency other && Equals(other);
    public override int GetHashCode() => Index;
    public override string ToString() => $"Frequency({Index})";

    public static bool operator ==(BeaconFrequency left, BeaconFrequency right) => left.Equals(right);
    public static bool operator !=(BeaconFrequency left, BeaconFrequency right) => !left.Equals(right);
}
