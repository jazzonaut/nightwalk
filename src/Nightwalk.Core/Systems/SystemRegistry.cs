namespace Nightwalk.Core.Systems;

/// <summary>
/// Registry for game systems with topological sorting by phase and dependencies.
/// </summary>
public sealed class SystemRegistry : ISystemRegistry
{
    private readonly Dictionary<Type, ISystem> _systemsByType = new();
    private readonly List<ISystem> _sortedSystems = new();
    private bool _isInitialized;
    private bool _isDirty;

    public int Count => _systemsByType.Count;

    public void Register<T>(T system) where T : class, ISystem
    {
        ArgumentNullException.ThrowIfNull(system);

        if (_systemsByType.ContainsKey(typeof(T)))
            throw new InvalidOperationException($"System of type {typeof(T).Name} is already registered.");

        _systemsByType[typeof(T)] = system;
        _isDirty = true;

        // If already initialized, initialize the new system immediately
        if (_isInitialized)
        {
            system.Initialize();
            RebuildSortedList();
        }
    }

    public T? Get<T>() where T : class, ISystem
    {
        return _systemsByType.TryGetValue(typeof(T), out var system) ? (T)system : null;
    }

    public T GetRequired<T>() where T : class, ISystem
    {
        return Get<T>() ?? throw new KeyNotFoundException($"System {typeof(T).Name} not found");
    }

    public bool TryGet<T>(out T? system) where T : class, ISystem
    {
        if (_systemsByType.TryGetValue(typeof(T), out var s))
        {
            system = (T)s;
            return true;
        }
        system = null;
        return false;
    }

    public bool Has<T>() where T : class, ISystem => _systemsByType.ContainsKey(typeof(T));

    public bool Unregister<T>() where T : class, ISystem
    {
        if (!_systemsByType.Remove(typeof(T)))
            return false;

        _isDirty = true;
        return true;
    }

    public void InitializeAll()
    {
        if (_isInitialized)
            throw new InvalidOperationException("Systems have already been initialized.");

        RebuildSortedList();

        foreach (var system in _sortedSystems)
        {
            system.Initialize();
        }

        _isInitialized = true;
    }

    public void TickAll(ulong currentTick)
    {
        if (_isDirty)
            RebuildSortedList();

        foreach (var system in _sortedSystems)
        {
            if (system.IsEnabled)
                system.Tick(currentTick);
        }
    }

    public void ShutdownAll()
    {
        // Shutdown in reverse order
        for (int i = _sortedSystems.Count - 1; i >= 0; i--)
        {
            _sortedSystems[i].Shutdown();
        }

        _systemsByType.Clear();
        _sortedSystems.Clear();
        _isInitialized = false;
    }

    public IReadOnlyList<ISystem> GetAllSystems() => _sortedSystems;

    private void RebuildSortedList()
    {
        _sortedSystems.Clear();
        ValidateCrossPhaseDependencies();

        // Group by phase, then topological sort within each phase
        var byPhase = _systemsByType.Values
            .GroupBy(s => s.Phase)
            .OrderBy(g => (int)g.Key);

        foreach (var phaseGroup in byPhase)
        {
            var sorted = TopologicalSort(phaseGroup.ToList());
            _sortedSystems.AddRange(sorted);
        }

        _isDirty = false;
    }

    /// <summary>
    /// Validates that dependencies do not cross phases.
    /// Cross-phase dependencies are not supported; use events instead.
    /// </summary>
    private void ValidateCrossPhaseDependencies()
    {
        foreach (var (type, system) in _systemsByType)
        {
            foreach (var depType in system.Dependencies)
            {
                if (!_systemsByType.TryGetValue(depType, out var depSystem))
                    continue;

                if (depSystem.Phase != system.Phase)
                {
                    throw new InvalidOperationException(
                        $"System {type.Name} (phase {system.Phase}) depends on {depType.Name} " +
                        $"(phase {depSystem.Phase}). Cross-phase dependencies are not supported; " +
                        "align phases or refactor to event-driven coordination.");
                }
            }
        }
    }

    private List<ISystem> TopologicalSort(List<ISystem> systems)
    {
        if (systems.Count <= 1)
            return systems;

        var typeToSystem = systems.ToDictionary(s => s.GetType());
        var inDegree = systems.ToDictionary(s => s, _ => 0);
        var dependents = systems.ToDictionary(s => s, _ => new List<ISystem>());

        // Build dependency graph
        foreach (var system in systems)
        {
            foreach (var depType in system.Dependencies)
            {
                if (typeToSystem.TryGetValue(depType, out var depSystem))
                {
                    inDegree[system]++;
                    dependents[depSystem].Add(system);
                }
            }
        }

        // Kahn's algorithm with PriorityQueue for O(n log n) instead of O(n² log n)
        var result = new List<ISystem>(systems.Count);
        var available = new PriorityQueue<ISystem, int>();

        foreach (var system in systems)
        {
            if (inDegree[system] == 0)
                available.Enqueue(system, system.Priority);
        }

        while (available.Count > 0)
        {
            var current = available.Dequeue();
            result.Add(current);

            foreach (var dependent in dependents[current])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    available.Enqueue(dependent, dependent.Priority);
            }
        }

        if (result.Count != systems.Count)
        {
            var resultSet = new HashSet<ISystem>(result);
            var unresolved = systems.Where(s => !resultSet.Contains(s)).Select(s => s.GetType().Name);
            throw new InvalidOperationException(
                $"Circular dependency detected among systems: {string.Join(", ", unresolved)}");
        }

        return result;
    }
}
