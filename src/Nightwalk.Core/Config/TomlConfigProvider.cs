using Tomlyn;

namespace Nightwalk.Core.Config;

/// <summary>
/// TOML-based configuration provider.
/// </summary>
public sealed class TomlConfigProvider : IConfigProvider
{
    private readonly Func<string, string> _fileReader;
    private readonly Func<string, bool> _fileExists;
    private readonly Dictionary<string, object> _cache = new();
    private readonly object _lock = new();

    public static TomlConfigProvider FromDirectory(string basePath)
    {
        return new TomlConfigProvider(
            name => File.ReadAllText(Path.Combine(basePath, $"{name}.toml")),
            name => File.Exists(Path.Combine(basePath, $"{name}.toml"))
        );
    }

    public TomlConfigProvider(Func<string, string> fileReader, Func<string, bool>? fileExists = null)
    {
        _fileReader = fileReader;
        _fileExists = fileExists ?? (_ => true);
    }

    public T Load<T>(string name) where T : class, new()
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(name, out var cached))
                return (T)cached;

            try
            {
                var toml = _fileReader(name);
                var result = Toml.ToModel<T>(toml);
                _cache[name] = result;
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load configuration '{name}' as {typeof(T).Name}. " +
                    $"Check that the TOML file exists and has valid syntax. Inner error: {ex.Message}",
                    ex);
            }
        }
    }

    public string LoadRaw(string name)
    {
        return _fileReader(name);
    }

    public bool Exists(string name)
    {
        return _fileExists(name);
    }

    public T Reload<T>(string name) where T : class, new()
    {
        lock (_lock) { _cache.Remove(name); }
        return Load<T>(name);
    }

    public void ClearCache()
    {
        lock (_lock) { _cache.Clear(); }
    }
}
