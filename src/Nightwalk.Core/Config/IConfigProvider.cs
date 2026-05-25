namespace Nightwalk.Core.Config;

/// <summary>
/// Interface for loading configuration from TOML files.
/// </summary>
public interface IConfigProvider
{
    T Load<T>(string name) where T : class, new();
    string LoadRaw(string name);
    bool Exists(string name);
    T Reload<T>(string name) where T : class, new();
}
