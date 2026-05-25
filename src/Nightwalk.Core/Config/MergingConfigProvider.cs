using Tomlyn;

namespace Nightwalk.Core.Config;

/// <summary>
/// Config provider that merges user preferences over system defaults.
/// Decorates an existing IConfigProvider (system configs) with a user config reader.
/// </summary>
public sealed class MergingConfigProvider : IConfigProvider
{
    private readonly IConfigProvider _systemProvider;
    private readonly Func<string, string?> _userConfigReader;
    private readonly Dictionary<string, object> _mergedCache = new();
    private readonly Dictionary<string, string> _rawCache = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a merging config provider.
    /// </summary>
    /// <param name="systemProvider">Provider for system default configs.</param>
    /// <param name="userConfigReader">Function that reads user config TOML by name, returns null if not found.</param>
    public MergingConfigProvider(IConfigProvider systemProvider, Func<string, string?> userConfigReader)
    {
        _systemProvider = systemProvider ?? throw new ArgumentNullException(nameof(systemProvider));
        _userConfigReader = userConfigReader ?? throw new ArgumentNullException(nameof(userConfigReader));
    }

    public T Load<T>(string name) where T : class, new()
    {
        lock (_lock)
        {
            if (_mergedCache.TryGetValue(name, out var cached))
                return (T)cached;

            var mergedToml = GetMergedToml(name);
            var result = Toml.ToModel<T>(mergedToml);
            _mergedCache[name] = result;
            return result;
        }
    }

    public string LoadRaw(string name)
    {
        lock (_lock)
        {
            if (_rawCache.TryGetValue(name, out var cached))
                return cached;

            var mergedToml = GetMergedToml(name);
            _rawCache[name] = mergedToml;
            return mergedToml;
        }
    }

    public bool Exists(string name) => _systemProvider.Exists(name);

    public T Reload<T>(string name) where T : class, new()
    {
        lock (_lock)
        {
            _mergedCache.Remove(name);
            _rawCache.Remove(name);
        }

        // Force reload of system config
        _systemProvider.Reload<T>(name);

        return Load<T>(name);
    }

    /// <summary>
    /// Clears all cached merged configs. Call when user preferences change.
    /// </summary>
    public void ClearCache()
    {
        lock (_lock)
        {
            _mergedCache.Clear();
            _rawCache.Clear();
        }
    }

    private string GetMergedToml(string name)
    {
        var systemToml = _systemProvider.LoadRaw(name);
        var userToml = _userConfigReader(name);
        return TomlMerger.Merge(systemToml, userToml);
    }
}
