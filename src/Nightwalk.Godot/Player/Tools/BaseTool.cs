using Godot;
using Nightwalk.Core.Tools;

namespace Nightwalk.Godot.Player.Tools;

/// <summary>
/// Base implementation for all tools.
/// </summary>
public abstract partial class BaseTool : Node3D, ITool
{
    /// <inheritdoc/>
    public abstract string Id { get; }

    /// <inheritdoc/>
    public abstract string DisplayName { get; }

    /// <inheritdoc/>
    public virtual bool RequiresUpdate => false;

    /// <inheritdoc/>
    public virtual bool RequiresInput => false;

    /// <summary>
    /// Context providing access to player and world systems.
    /// </summary>
    protected ToolContext Context { get; private set; } = null!;

    /// <summary>
    /// Whether the tool has been initialized.
    /// </summary>
    protected bool Initialized { get; private set; }

    /// <summary>
    /// Initializes the tool with the given context.
    /// </summary>
    /// <param name="context">The tool context.</param>
    public void Initialize(ToolContext context)
    {
        Context = context;
        OnInitialize();
        Initialized = true;
    }

    /// <summary>
    /// Called during initialization. Override to perform setup.
    /// </summary>
    protected virtual void OnInitialize() { }

    /// <inheritdoc/>
    public virtual void Enter()
    {
        Visible = true;
    }

    /// <inheritdoc/>
    public virtual void Exit()
    {
        Visible = false;
    }

    /// <inheritdoc/>
    public virtual void Update(float delta) { }

    /// <summary>
    /// Handles input events.
    /// </summary>
    /// <param name="event">The input event.</param>
    /// <returns>True if the event was consumed.</returns>
    public virtual bool HandleInput(InputEvent @event) => false;
}
