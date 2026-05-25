namespace Nightwalk.Core.Diagnostics;

/// <summary>
/// Logging categories for structured log output.
/// </summary>
public static class LogCategory
{
    public const string Sim = "Sim";
    public const string World = "World";
    public const string Chunk = "Chunk";
    public const string Entity = "Entity";
    public const string Config = "Config";
    public const string Perf = "Perf";
    public const string Input = "Input";
    public const string Audio = "Audio";
    public const string UI = "UI";
}

/// <summary>
/// Game logging interface - implemented by engine-specific loggers.
/// </summary>
public interface IGameLog
{
    void Info(string category, string message);
    void Warn(string category, string message);
    void Error(string category, string message, Exception? ex = null);
    void Debug(string category, string message);

    /// <summary>
    /// Returns true if debug logging is enabled for the given category.
    /// Callers should check this before formatting expensive log messages.
    /// </summary>
    bool IsDebugEnabled(string category);
}

/// <summary>
/// Null logger for when no logger is configured.
/// </summary>
public sealed class NullGameLog : IGameLog
{
    public static readonly NullGameLog Instance = new();
    public void Info(string category, string message) { }
    public void Warn(string category, string message) { }
    public void Error(string category, string message, Exception? ex = null) { }
    public void Debug(string category, string message) { }
    public bool IsDebugEnabled(string category) => false;
}

/// <summary>
/// Static accessor for the global game logger.
/// </summary>
public static class Log
{
    private static IGameLog _instance = NullGameLog.Instance;

    public static void SetLogger(IGameLog logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _instance = logger;
    }

    public static void Info(string category, string message) => _instance.Info(category, message);
    public static void Warn(string category, string message) => _instance.Warn(category, message);
    public static void Error(string category, string message, Exception? ex = null) => _instance.Error(category, message, ex);
    public static void Debug(string category, string message) => _instance.Debug(category, message);
    public static bool IsDebugEnabled(string category) => _instance.IsDebugEnabled(category);
}
