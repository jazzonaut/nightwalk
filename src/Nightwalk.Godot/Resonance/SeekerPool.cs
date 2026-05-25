using System.Collections.Generic;
using System.Linq;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Resonance.Seeker;

namespace Nightwalk.Godot.Resonance;

/// <summary>
/// Object pool for Seeker nodes to avoid allocation during gameplay.
/// </summary>
public sealed partial class SeekerPool : Node3D
{
    private SeekerConfig? _config;
    private readonly Stack<SeekerNode> _pool = new();
    private readonly Dictionary<System.Guid, SeekerNode> _active = new();
    private const int InitialPoolSize = 8;
    private const int MaxPoolSize = 16; // Prevent unbounded growth
    private float _shrinkTimer;

    /// <summary>
    /// Initializes the pool with config.
    /// </summary>
    public void Initialize(SeekerConfig config)
    {
        _config = config;

        // Pre-warm the pool
        for (int i = 0; i < InitialPoolSize; i++)
        {
            var seeker = CreateSeeker();
            _pool.Push(seeker);
        }
    }

    private SeekerNode CreateSeeker()
    {
        var seeker = new SeekerNode { Name = "PooledSeeker" };
        seeker.Initialize(_config!);
        AddChild(seeker);
        return seeker;
    }

    /// <summary>
    /// Gets a Seeker node for the given instance.
    /// </summary>
    public SeekerNode? Get(SeekerInstance instance)
    {
        if (_active.ContainsKey(instance.Id))
        {
            return _active[instance.Id];
        }

        SeekerNode seeker;
        if (_pool.Count > 0)
        {
            seeker = _pool.Pop();
        }
        else
        {
            seeker = CreateSeeker();
        }

        seeker.AssignInstance(instance);
        _active[instance.Id] = seeker;
        return seeker;
    }

    /// <summary>
    /// Returns a Seeker node to the pool.
    /// </summary>
    public void Return(SeekerInstance instance)
    {
        if (_active.TryGetValue(instance.Id, out var seeker))
        {
            seeker.ReturnToPool();
            _active.Remove(instance.Id);

            // Only pool up to max size, free excess
            if (_pool.Count < MaxPoolSize)
            {
                _pool.Push(seeker);
            }
            else
            {
                seeker.QueueFree();
            }
        }
    }

    /// <summary>
    /// Returns all active Seekers to the pool.
    /// </summary>
    public void ReturnAll()
    {
        var seekers = _active.Values.ToList();
        foreach (var seeker in seekers)
        {
            seeker.ReturnToPool();
            _pool.Push(seeker);
        }
        _active.Clear();
    }

    /// <summary>
    /// Updates line-of-sight checks for all active Seekers.
    /// </summary>
    public void UpdateLineOfSight(Vector3 playerPos)
    {
        foreach (var kvp in _active)
        {
            kvp.Value.CheckLineOfSight(playerPos);
        }
    }

    /// <summary>
    /// Updates collision avoidance for all active Seekers.
    /// </summary>
    public void UpdateCollisionAvoidance()
    {
        foreach (var kvp in _active)
        {
            kvp.Value.UpdateCollisionAvoidance();
        }
    }

    /// <summary>
    /// Periodically shrinks the pool if it exceeds the initial size.
    /// Call this from _Process to gradually free unused nodes.
    /// </summary>
    public void ProcessPoolShrink(float delta)
    {
        _shrinkTimer += delta;

        // Check every 5 seconds
        if (_shrinkTimer < 5f) return;
        _shrinkTimer = 0f;

        // Shrink pool toward initial size if no active seekers
        if (_active.Count == 0 && _pool.Count > InitialPoolSize)
        {
            var seeker = _pool.Pop();
            seeker.QueueFree();
        }
    }

    /// <summary>
    /// Cleans up all Seeker nodes.
    /// </summary>
    public void Cleanup()
    {
        ReturnAll();

        while (_pool.Count > 0)
        {
            var seeker = _pool.Pop();
            seeker.QueueFree();
        }
    }
}
