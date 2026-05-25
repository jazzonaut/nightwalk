using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scoring;
using Nightwalk.Core.Tools;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Beacons;
using Nightwalk.Godot.Player.Tools;
using Nightwalk.Godot.Ziplines;

namespace Nightwalk.Godot.Player;

/// <summary>
/// Manages player tools in a registry-based extensible system.
/// Tools can be passive (lights) or interactive (zipline placement).
/// </summary>
public sealed partial class PlayerToolManager : Node3D
{
    private readonly List<ITool> _tools = new();
    private int _currentIndex = -1; // -1 = None
    private ITool? _currentTool;
    private ToolContext? _context;
    private GameDataService _data = null!;
    private IEventBus? _eventBus;
    private ZiplineRegistry? _ziplineRegistry;
    private bool _wasInPrismPOV;

    /// <summary>
    /// Callback for UI updates. Passes null for no tool, or tool display name.
    /// </summary>
    public event Action<string?>? OnToolChanged;

    /// <summary>
    /// Current active tool.
    /// </summary>
    public ITool? CurrentTool => _currentTool;

    /// <summary>
    /// Current tool ID, or null if no tool selected.
    /// </summary>
    public string? CurrentToolId => _currentTool?.Id;

    /// <summary>
    /// Number of registered tools (not including "None").
    /// </summary>
    public int ToolCount => _tools.Count;

    /// <summary>
    /// Configures the tool manager with game data service.
    /// Call this before _Ready.
    /// </summary>
    public void Configure(GameDataService data)
    {
        _data = data;
    }

    /// <summary>
    /// Sets the event bus for tool events.
    /// </summary>
    public void SetEventBus(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Sets the zipline registry for cable tracking.
    /// </summary>
    public void SetZiplineRegistry(ZiplineRegistry registry)
    {
        _ziplineRegistry = registry;
    }

    /// <summary>
    /// Resets tool state for a new session.
    /// </summary>
    public void ResetForSession()
    {
        _ziplineRegistry?.ClearAll();

        // Clear any active decoys
        foreach (var tool in _tools)
        {
            if (tool is DecoyTool decoyTool)
            {
                decoyTool.ClearAllDecoys();
                break;
            }
        }

        if (_currentTool is ZiplineTool ziplineTool)
        {
            ziplineTool.Exit();
            ziplineTool.Enter();
        }
    }

    /// <summary>
    /// Sets the score service for tool placement costs.
    /// </summary>
    public void SetScoreService(IScoreService scoreService)
    {
        if (_context != null)
        {
            _context.ScoreService = scoreService;
        }
    }

    /// <summary>
    /// Sets the beacon sync manager for the prism tool.
    /// </summary>
    public void SetBeaconSyncManager(BeaconSyncManager syncManager)
    {
        if (_context != null)
        {
            _context.BeaconSyncManager = syncManager;
        }
    }

    /// <summary>
    /// Sets the difficulty service for scaling tool costs and limits.
    /// </summary>
    public void SetDifficultyService(IDifficultyService? difficultyService)
    {
        if (_context != null)
        {
            _context.DifficultyService = difficultyService;
        }
    }

    /// <summary>
    /// Sets the SFX manager for procedural tool sounds.
    /// </summary>
    public void SetSfxManager(SfxManager? sfxManager)
    {
        if (_context != null)
        {
            _context.SfxManager = sfxManager;
        }
    }

    /// <summary>
    /// Sets the synth loop manager for continuous procedural audio.
    /// </summary>
    public void SetSynthLoopManager(SynthLoopManager? synthLoopManager)
    {
        if (_context != null)
        {
            _context.SynthLoopManager = synthLoopManager;
        }
    }

    /// <summary>
    /// Initializes tools with player context.
    /// Should be called after Configure and after player is fully initialized.
    /// </summary>
    public void InitializeTools(PlayerEntity player)
    {
        if (player.Camera == null || player.Head == null)
        {
            Log.Warn(LogCategory.Entity, "Cannot initialize tools: player camera or head is null");
            return;
        }

        var spaceState = player.GetWorld3D()?.DirectSpaceState;
        if (spaceState == null)
        {
            Log.Warn(LogCategory.Entity, "Cannot initialize tools: physics space state is null");
            return;
        }

        _context = new ToolContext
        {
            Player = player,
            Camera = player.Camera,
            Head = player.Head,
            SpaceState = spaceState,
            EventBus = _eventBus ?? new EventBus(),
            Data = _data,
            ZiplineRegistry = _ziplineRegistry ?? new ZiplineRegistry()
        };

        RegisterDefaultTools();

        Log.Info(LogCategory.Entity, $"PlayerToolManager initialized with {_tools.Count} tools");
    }

    private void RegisterDefaultTools()
    {
        RegisterTool(new SpotlightTool());
        RegisterTool(new PrismTool());
        RegisterTool(new ZiplineTool());
        RegisterTool(new DecoyTool());
    }

    /// <summary>
    /// Registers a new tool with the manager.
    /// </summary>
    public void RegisterTool(ITool tool)
    {
        if (_context == null)
        {
            Log.Warn(LogCategory.Entity, $"Cannot register tool {tool.Id}: context not initialized");
            return;
        }

        if (tool is Node node)
        {
            AddChild(node);
        }

        if (tool is BaseTool baseTool)
        {
            baseTool.Initialize(_context);
        }

        _tools.Add(tool);
        Log.Debug(LogCategory.Entity, $"Registered tool: {tool.DisplayName}");
    }

    public override void _Ready()
    {
        SetTool(-1); // Start with no tool
        Log.Info(LogCategory.Entity, "PlayerToolManager ready");
    }

    public override void _Process(double delta)
    {
        var isInPrismPOV = _context?.Player.IsInPrismPOV == true;

        // Handle POV mode transitions - hide/show tool preview
        if (isInPrismPOV && !_wasInPrismPOV)
        {
            // Entering POV mode - hide tool
            _currentTool?.Exit();
        }
        else if (!isInPrismPOV && _wasInPrismPOV)
        {
            // Exiting POV mode - show tool
            _currentTool?.Enter();
        }
        _wasInPrismPOV = isInPrismPOV;

        // Don't update tools (preview) while in prism POV mode
        if (isInPrismPOV)
            return;

        if (_currentTool?.RequiresUpdate == true)
        {
            _currentTool.Update((float)delta);
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Don't process tool input when player input is disabled or in prism POV mode
        if (_context?.Player.InputEnabled != true) return;
        if (_context.Player.IsInPrismPOV) return;

        if (_currentTool is BaseTool baseTool && baseTool.RequiresInput)
        {
            if (baseTool.HandleInput(@event))
            {
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// Cycles to the next tool (wraps around).
    /// </summary>
    /// <param name="direction">1 for next, -1 for previous</param>
    public void CycleTool(int direction)
    {
        // Don't cycle tools while in prism POV mode
        if (_context?.Player.IsInPrismPOV == true)
            return;

        int count = _tools.Count + 1; // +1 for None
        int currentCyclePos = _currentIndex + 1; // Convert to 0-based (None=0)
        int next = (currentCyclePos + direction + count) % count;
        SetTool(next - 1); // Convert back (-1 = None)
    }

    /// <summary>
    /// Sets the active tool by index. -1 for no tool.
    /// </summary>
    public void SetTool(int index)
    {
        if (index == _currentIndex) return;

        // Exit current tool
        _currentTool?.Exit();
        _currentIndex = index;

        // Enter new tool
        if (index < 0 || index >= _tools.Count)
        {
            _currentTool = null;
            Log.Debug(LogCategory.Input, "Tool switched to: None");
            OnToolChanged?.Invoke(null);
        }
        else
        {
            _currentTool = _tools[index];
            _currentTool.Enter();
            Log.Debug(LogCategory.Input, $"Tool switched to: {_currentTool.DisplayName}");
            OnToolChanged?.Invoke(_currentTool.DisplayName);
        }
    }

    /// <summary>
    /// Sets the active tool by ID. Pass null for no tool.
    /// </summary>
    public void SetToolById(string? toolId)
    {
        if (toolId == null)
        {
            SetTool(-1);
            return;
        }

        for (int i = 0; i < _tools.Count; i++)
        {
            if (_tools[i].Id == toolId)
            {
                SetTool(i);
                return;
            }
        }

        Log.Warn(LogCategory.Input, $"Unknown tool ID: {toolId}");
    }

    /// <summary>
    /// Gets the currently hovered zipline cable if the zipline tool is active.
    /// </summary>
    public ZiplineCable? GetHoveredZipline()
    {
        return (_currentTool as ZiplineTool)?.HoveredCable;
    }

    /// <summary>
    /// Reconfigures all tools from current config. Call on hot reload.
    /// </summary>
    public void ReconfigureTools()
    {
        foreach (var tool in _tools)
        {
            if (tool is BaseLightTool lightTool)
            {
                lightTool.Reconfigure();
            }
            else if (tool is ZiplineTool ziplineTool)
            {
                ziplineTool.Reconfigure();
            }
            else if (tool is PrismTool prismTool)
            {
                prismTool.Reconfigure();
            }
        }
    }
}
