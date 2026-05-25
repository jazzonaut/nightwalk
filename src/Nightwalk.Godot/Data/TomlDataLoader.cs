using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Tomlyn;
using FileNotFoundException = System.IO.FileNotFoundException;

namespace Nightwalk.Godot.Data;

/// <summary>
/// Godot-specific TOML data loader using global::Godot.FileAccess.
/// </summary>
public sealed class TomlDataLoader : IConfigProvider
{
    private readonly string _basePath;
    private readonly Dictionary<string, string> _textCache = new();
    private readonly Dictionary<string, object> _configCache = new();

    public TomlDataLoader(string basePath)
    {
        _basePath = basePath;
    }

    public string LoadRaw(string name)
    {
        var key = name;
        if (_textCache.TryGetValue(key, out var cached))
            return cached;

        var fullPath = _basePath + name + ConfigConsts.FileExtension;
        using var file = global::Godot.FileAccess.Open(fullPath, global::Godot.FileAccess.ModeFlags.Read);

        if (file == null)
        {
            var error = global::Godot.FileAccess.GetOpenError();
            throw new FileNotFoundException($"File not found: {fullPath} (Error: {error})");
        }

        var content = file.GetAsText() ?? "";
        _textCache[key] = content;
        return content;
    }

    public T Load<T>(string name) where T : class, new()
    {
        var key = name;
        if (_configCache.TryGetValue(key, out var cached))
            return (T)cached;

        var toml = LoadRaw(name);
        var result = Toml.ToModel<T>(toml);
        _configCache[key] = result;
        return result;
    }

    public bool Exists(string name)
    {
        var fullPath = _basePath + name + ConfigConsts.FileExtension;
        return global::Godot.FileAccess.FileExists(fullPath);
    }

    public T Reload<T>(string name) where T : class, new()
    {
        _textCache.Remove(name);
        _configCache.Remove(name);
        return Load<T>(name);
    }

    public void ClearCache()
    {
        _textCache.Clear();
        _configCache.Clear();
    }
}
