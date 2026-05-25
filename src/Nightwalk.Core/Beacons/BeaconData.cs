using System.Numerics;

namespace Nightwalk.Core.Beacons;

/// <summary>
/// Lightweight data for a beacon's generation state.
/// </summary>
public readonly struct BeaconData
{
    /// <summary>
    /// Unique identifier for this beacon.
    /// </summary>
    public BeaconId Id { get; }

    /// <summary>
    /// World position of the beacon (on rooftop).
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// Frequency of this beacon.
    /// </summary>
    public BeaconFrequency Frequency { get; }

    public BeaconData(BeaconId id, Vector3 position, BeaconFrequency frequency)
    {
        Id = id;
        Position = position;
        Frequency = frequency;
    }
}
