using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.Diagnostics;

/// <summary>
/// Godot-specific logger implementation using GD.Print/GD.PrintErr.
/// </summary>
public sealed class GodotLogger : IGameLog
{
    private readonly bool _debugEnabled;
    private readonly HashSet<string>? _debugCategories;

    /// <summary>
    /// Creates a logger with debug output globally enabled or disabled.
    /// </summary>
    public GodotLogger(bool debugEnabled = false)
    {
        _debugEnabled = debugEnabled;
        _debugCategories = null;
    }

    /// <summary>
    /// Creates a logger with debug output enabled only for specific categories.
    /// </summary>
    public GodotLogger(IEnumerable<string> debugCategories)
    {
        _debugEnabled = true;
        _debugCategories = new HashSet<string>(debugCategories);
    }

    /// <summary>
    /// Returns true if debug logging is enabled for the given category.
    /// Callers should check this before formatting expensive log messages.
    /// </summary>
    public bool IsDebugEnabled(string category)
    {
        if (!_debugEnabled) return false;
        return _debugCategories == null || _debugCategories.Contains(category);
    }

    public void Info(string category, string message)
    {
        GD.Print($"[{category}] {message}");
    }

    public void Warn(string category, string message)
    {
        GD.PrintRich($"[color=yellow][{category}] WARN: {message}[/color]");
    }

    public void Error(string category, string message, Exception? ex = null)
    {
        if (ex != null)
        {
            GD.PrintErr($"[{category}] ERROR: {message}");
            GD.PrintErr($"  Exception: {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace != null)
            {
                GD.PrintErr($"  Stack: {ex.StackTrace}");
            }
        }
        else
        {
            GD.PrintErr($"[{category}] ERROR: {message}");
        }
    }

    public void Debug(string category, string message)
    {
        if (IsDebugEnabled(category))
        {
            GD.Print($"[{category}] DEBUG: {message}");
        }
    }
}
