namespace Nightwalk.Core.Tools;

/// <summary>
/// Interface for player tools in the tool belt system.
/// Tools can be passive (lights) or interactive (zipline placement).
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique identifier for this tool type.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name for HUD.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this tool requires per-frame updates when active.
    /// </summary>
    bool RequiresUpdate { get; }

    /// <summary>
    /// Whether this tool handles input directly.
    /// </summary>
    bool RequiresInput { get; }

    /// <summary>
    /// Called when tool becomes active.
    /// </summary>
    void Enter();

    /// <summary>
    /// Called when switching away from this tool.
    /// </summary>
    void Exit();

    /// <summary>
    /// Called every frame when active (if RequiresUpdate is true).
    /// </summary>
    /// <param name="delta">Time since last frame in seconds.</param>
    void Update(float delta);
}
