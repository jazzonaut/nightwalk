using System;
using System.Collections.Generic;

namespace Nightwalk.Core.Resonance;

/// <summary>
/// Type of noise ping.
/// </summary>
public enum PingType
{
    /// <summary>Normal noise from collectible pickup.</summary>
    Noise,
    /// <summary>Decoy created by player spotlight.</summary>
    Decoy,
    /// <summary>Player was detected by a Seeker - highest priority.</summary>
    Detection
}

/// <summary>
/// Represents a noise ping location that Seekers may investigate.
/// Uses object pooling for efficient allocation.
/// </summary>
public sealed class NoisePing
{
    private static int _nextId;
    private static readonly Stack<NoisePing> _pool = new(32);

    /// <summary>
    /// Unique identifier for this ping.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// World position of the ping.
    /// </summary>
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Z { get; private set; }

    /// <summary>
    /// Time when the ping was created (for expiration).
    /// </summary>
    public float CreationTime { get; private set; }

    /// <summary>
    /// The type of this ping.
    /// </summary>
    public PingType Type { get; private set; }

    /// <summary>
    /// Priority multiplier for this ping.
    /// </summary>
    public float Priority { get; private set; }

    /// <summary>
    /// Whether this ping has been fully investigated.
    /// </summary>
    public bool Investigated { get; set; }

    /// <summary>
    /// Number of seekers currently investigating this ping.
    /// </summary>
    public int AssignedSeekers { get; set; }

    private NoisePing() { }

    /// <summary>
    /// Gets a ping from the pool or creates a new one.
    /// </summary>
    public static NoisePing Create(float x, float y, float z, float creationTime, PingType type, float priority)
    {
        NoisePing ping;
        if (_pool.Count > 0)
        {
            ping = _pool.Pop();
        }
        else
        {
            ping = new NoisePing();
        }

        ping.Id = _nextId++;
        ping.X = x;
        ping.Y = y;
        ping.Z = z;
        ping.CreationTime = creationTime;
        ping.Type = type;
        ping.Priority = priority;
        ping.Investigated = false;
        ping.AssignedSeekers = 0;

        return ping;
    }

    /// <summary>
    /// Returns this ping to the pool for reuse.
    /// </summary>
    public void Return()
    {
        // Reset state
        Investigated = false;
        AssignedSeekers = 0;

        // Only pool up to a reasonable limit
        if (_pool.Count < 64)
        {
            _pool.Push(this);
        }
    }

    /// <summary>
    /// Resets the static ID counter and clears the pool.
    /// Call when starting a new session.
    /// </summary>
    public static void ResetPool()
    {
        _nextId = 0;
        _pool.Clear();
    }
}
