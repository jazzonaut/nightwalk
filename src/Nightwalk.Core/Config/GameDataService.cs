using System.Collections.Generic;
using System.IO;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Core.Config;

/// <summary>
/// Centralized access to all game configuration.
/// </summary>
public sealed class GameDataService : IDisposable
{
    private readonly IConfigProvider _config;
    private bool _disposed;

    // File watching for hot reload (dev only)
    private FileSystemWatcher? _systemWatcher;
    private FileSystemWatcher? _userWatcher;
    private volatile bool _pendingReload;
    private volatile bool _suppressUserWatcher;
    private DateTime _lastReloadTime;
    private readonly object _reloadLock = new();
    private readonly HashSet<string> _changedConfigs = new();
    private const int DebounceMs = 300;

    /// <summary>
    /// Fired after configs have been reloaded. Parameter contains config names that changed (e.g., "worldgen", "physics").
    /// </summary>
    public event Action<IReadOnlySet<string>>? OnConfigReloaded;

    public CoreConfig Core { get; private set; } = null!;
    public PhysicsConfig Physics { get; private set; } = null!;
    public GameplayConfig Gameplay { get; private set; } = null!;
    public WorldgenConfig Worldgen { get; private set; } = null!;
    public AudioConfig Audio { get; private set; } = null!;
    public ToolsConfig Tools { get; private set; } = null!;
    public BeaconsConfig Beacons { get; private set; } = null!;
    public AlivenessConfig Aliveness { get; private set; } = null!;
    public ResonanceConfig Resonance { get; private set; } = null!;

    /// <summary>
    /// User preferences service for setting and saving user overrides.
    /// Null if user preferences have not been configured.
    /// </summary>
    public UserPreferencesService? UserPreferences { get; private set; }

    public GameDataService(IConfigProvider config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>
    /// Configures user preferences support. Call after construction.
    /// </summary>
    /// <param name="userConfigReader">Function to read user config TOML by name.</param>
    /// <param name="userConfigWriter">Action to write user config TOML (name, content).</param>
    public void ConfigureUserPreferences(
        Func<string, string?> userConfigReader,
        Action<string, string> userConfigWriter)
    {
        UserPreferences = new UserPreferencesService(
            userConfigReader,
            userConfigWriter,
            OnUserPreferencesChanged
        );
    }

    private void OnUserPreferencesChanged(IReadOnlySet<string> changedConfigs)
    {
        // Suppress watcher to prevent double reload
        _suppressUserWatcher = true;
        try
        {
            // Clear the merging provider cache if it supports it
            if (_config is MergingConfigProvider mergingProvider)
            {
                mergingProvider.ClearCache();
            }

            // Reload with error handling (same as hot-reload)
            var success = TryReloadAndNotify(changedConfigs);

            // Only clear pending state if reload succeeded
            if (success)
            {
                lock (_reloadLock)
                {
                    foreach (var config in changedConfigs)
                    {
                        _changedConfigs.Remove(config);
                    }
                    // Reset pending flag if no other configs are waiting
                    if (_changedConfigs.Count == 0)
                    {
                        _pendingReload = false;
                    }
                }
            }
        }
        finally
        {
            _suppressUserWatcher = false;
        }
    }

    /// <summary>
    /// Attempts to reload all configs and notify listeners.
    /// Catches parse errors to avoid crashing.
    /// </summary>
    private bool TryReloadAndNotify(IReadOnlySet<string> changedConfigs)
    {
        try
        {
            ReloadAll();
            OnConfigReloaded?.Invoke(changedConfigs);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(LogCategory.Config, "Config reload failed (keeping previous config)", ex);
            return false;
        }
    }

    public void LoadAll()
    {
        Core = _config.Load<CoreConfig>("core");
        Physics = _config.Load<PhysicsConfig>("physics");
        Gameplay = _config.Load<GameplayConfig>("gameplay");
        Worldgen = _config.Load<WorldgenConfig>("worldgen");
        Audio = _config.Load<AudioConfig>("audio");
        Tools = _config.Load<ToolsConfig>("tools");
        Beacons = _config.Load<BeaconsConfig>("beacons");
        Aliveness = _config.Load<AlivenessConfig>("aliveness");
        Resonance = _config.Load<ResonanceConfig>("resonance");
    }

    /// <summary>
    /// Reloads all config files. Throws on parse error.
    /// To avoid partial state, loads all into temp vars first.
    /// </summary>
    public void ReloadAll()
    {
        // Load all into temp vars first - if any throws, no state changes
        var core = _config.Reload<CoreConfig>("core");
        var physics = _config.Reload<PhysicsConfig>("physics");
        var gameplay = _config.Reload<GameplayConfig>("gameplay");
        var worldgen = _config.Reload<WorldgenConfig>("worldgen");
        var audio = _config.Reload<AudioConfig>("audio");
        var tools = _config.Reload<ToolsConfig>("tools");
        var beacons = _config.Reload<BeaconsConfig>("beacons");
        var aliveness = _config.Reload<AlivenessConfig>("aliveness");
        var resonance = _config.Reload<ResonanceConfig>("resonance");

        // All succeeded - commit changes
        Core = core;
        Physics = physics;
        Gameplay = gameplay;
        Worldgen = worldgen;
        Audio = audio;
        Tools = tools;
        Beacons = beacons;
        Aliveness = aliveness;
        Resonance = resonance;
    }

    public T LoadCustom<T>(string name) where T : class, new()
    {
        return _config.Load<T>(name);
    }

    /// <summary>
    /// Starts watching the data directory for TOML file changes.
    /// Call this in debug builds to enable hot reload.
    /// </summary>
    /// <param name="systemDataPath">Absolute path to the system data directory containing TOML files.</param>
    /// <param name="userDataPath">Optional absolute path to the user config directory. If provided and exists, watches for user config changes.</param>
    public void StartWatching(string systemDataPath, string? userDataPath = null)
    {
        if (_systemWatcher != null) return;

        _systemWatcher = new FileSystemWatcher(systemDataPath, "*.toml")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _systemWatcher.Changed += OnSystemFileChanged;
        _systemWatcher.Created += OnSystemFileChanged;

        // Watch user config directory if provided (create if needed)
        if (!string.IsNullOrEmpty(userDataPath))
        {
            Directory.CreateDirectory(userDataPath);

            _userWatcher = new FileSystemWatcher(userDataPath, "*.toml")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _userWatcher.Changed += OnUserFileChanged;
            _userWatcher.Created += OnUserFileChanged;
            _userWatcher.Deleted += OnUserFileChanged;
        }
    }

    /// <summary>
    /// Stops watching for file changes and cleans up resources.
    /// </summary>
    public void StopWatching()
    {
        if (_systemWatcher != null)
        {
            _systemWatcher.EnableRaisingEvents = false;
            _systemWatcher.Changed -= OnSystemFileChanged;
            _systemWatcher.Created -= OnSystemFileChanged;
            _systemWatcher.Dispose();
            _systemWatcher = null;
        }

        if (_userWatcher != null)
        {
            _userWatcher.EnableRaisingEvents = false;
            _userWatcher.Changed -= OnUserFileChanged;
            _userWatcher.Created -= OnUserFileChanged;
            _userWatcher.Deleted -= OnUserFileChanged;
            _userWatcher.Dispose();
            _userWatcher = null;
        }
    }

    /// <summary>
    /// Disposes resources held by this service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
    }

    /// <summary>
    /// Processes any pending config reload. Call this from the main game loop.
    /// Catches parse errors to avoid crashing during partial file writes.
    /// </summary>
    public void ProcessPendingReload()
    {
        if (!_pendingReload) return;

        HashSet<string> changed;
        lock (_reloadLock)
        {
            if (!_pendingReload) return;
            _pendingReload = false;
            changed = new HashSet<string>(_changedConfigs);
            _changedConfigs.Clear();
        }

        TryReloadAndNotify(changed);
    }

    private void OnSystemFileChanged(object sender, FileSystemEventArgs e)
    {
        HandleConfigFileChanged(e.Name);
    }

    private void OnUserFileChanged(object sender, FileSystemEventArgs e)
    {
        // Skip if this is from a programmatic save (avoids double reload)
        if (_suppressUserWatcher)
            return;

        // Clear merge cache when user config changes
        if (_config is MergingConfigProvider mergingProvider)
        {
            mergingProvider.ClearCache();
        }

        HandleConfigFileChanged(e.Name);
    }

    private void HandleConfigFileChanged(string? fileName)
    {
        lock (_reloadLock)
        {
            // Extract config name from filename (e.g., "worldgen.toml" -> "worldgen")
            var configName = Path.GetFileNameWithoutExtension(fileName);
            if (!string.IsNullOrEmpty(configName))
            {
                _changedConfigs.Add(configName);
            }

            var now = DateTime.Now;
            if ((now - _lastReloadTime).TotalMilliseconds < DebounceMs && _pendingReload)
                return;

            _lastReloadTime = now;
            _pendingReload = true;
        }
    }
}
