using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Rendering.Materials;
using Nightwalk.Godot.Rendering.Pooling;
using Nightwalk.Godot.Rendering.SubRenderers;

namespace Nightwalk.Godot.Rendering;

/// <summary>
/// Coordinates chunk rendering by delegating to specialized sub-renderers.
/// Supports progressive rendering to spread work across multiple frames.
/// </summary>
public sealed class ChunkRenderer
{
    private readonly Node3D _parent;
    private readonly Dictionary<ChunkCoord, Node3D> _chunkNodes = new();

    // Shared resources
    private readonly NodePool _nodePool = new();
    private readonly MaterialFactory _materialFactory = new();

    // Pre-created materials
    private readonly StandardMaterial3D _groundMaterial;
    private readonly Material[] _buildingMaterials;
    private readonly StandardMaterial3D _windowUnlitMaterial;
    private readonly ShaderMaterial _windowLitMaterial;
    private readonly StandardMaterial3D _ledgeMaterial;
    private readonly ShaderMaterial _wetRoadMaterial;
    private readonly ShaderMaterial _wetSidewalkMaterial;
    private readonly StandardMaterial3D _roadMarkingMaterial;
    private readonly StandardMaterial3D _lampPoleMaterial;
    private readonly StandardMaterial3D _lampRingMaterial;
    private readonly StandardMaterial3D? _facadeDetailMaterial;

    // Phased sub-renderers keyed by phase
    private readonly Dictionary<ChunkRenderPhase, IPhasedSubRenderer> _phasedRenderers = new();

    // Progressive rendering state
    private readonly Queue<ChunkRenderState> _pendingRender = new();
    private readonly List<ChunkRenderState> _activeRenders = new();
    private readonly Dictionary<ChunkCoord, ChunkRenderState> _renderingChunks = new();

    // Tracks nodes with pending deferred AddChild calls to prevent race conditions
    private readonly HashSet<Node3D> _pendingAdd = new();

    // Progressive rendering configuration (from TOML)
    private readonly bool _progressiveRenderingEnabled;
    private readonly long _renderBudgetUsec;
    private readonly int _maxConcurrentRenders;
    private readonly bool _debugShowBoundaries;

    // Camera position for LOD calculations (updated each frame)
    private Vector3 _cameraPosition;

    // Phase order for progression
    private static readonly ChunkRenderPhase[] PhaseOrder =
    {
        ChunkRenderPhase.Ground,
        ChunkRenderPhase.Roads,
        ChunkRenderPhase.Manholes,
        ChunkRenderPhase.Pavement,
        ChunkRenderPhase.Buildings,
        ChunkRenderPhase.Neons,
        ChunkRenderPhase.StreetLamps
    };

    public ChunkRenderer(
        Node3D parent,
        ChunkConfig chunks,
        MaterialsConfig materials,
        RoadConfig roads,
        FacadeConfig facades,
        FacadeWeatheringConfig? facadeWeathering,
        FacadeDetailConfig? facadeDetails,
        WindowConfig windows,
        NeonConfig neon,
        RoadMarkingsConfig roadMarkings,
        WetSurfaceConfig wetSurface,
        StreetLightsConfig streetLights,
        ManholeConfig manholes,
        SmokeConfig smoke,
        OcclusionCullingConfig? occlusionCulling)
    {
        _parent = parent;

        // Progressive rendering configuration
        _progressiveRenderingEnabled = chunks.ProgressiveRenderingEnabled;
        _renderBudgetUsec = chunks.RenderBudgetMicroseconds;
        _maxConcurrentRenders = chunks.MaxConcurrentRenders;
        _debugShowBoundaries = chunks.DebugShowBoundaries;

        // Create materials
        _groundMaterial = MaterialFactory.CreateMaterial(materials.Ground);

        // Create building materials - use weathered shader if enabled, otherwise standard
        if (facadeWeathering?.Enabled == true)
        {
            _buildingMaterials = new Material[]
            {
                MaterialFactory.CreateWeatheredFacadeMaterial(materials.BuildingDark, facadeWeathering, facades),
                MaterialFactory.CreateWeatheredFacadeMaterial(materials.Building, facadeWeathering, facades),
                MaterialFactory.CreateWeatheredFacadeMaterial(materials.BuildingBlue, facadeWeathering, facades),
                MaterialFactory.CreateWeatheredFacadeMaterial(materials.BuildingPurple, facadeWeathering, facades),
                MaterialFactory.CreateWeatheredFacadeMaterial(materials.BuildingWarm, facadeWeathering, facades),
                MaterialFactory.CreateWeatheredFacadeMaterial(materials.BuildingWeathered, facadeWeathering, facades)
            };
        }
        else
        {
            _buildingMaterials = new Material[]
            {
                MaterialFactory.CreateMaterial(materials.BuildingDark),
                MaterialFactory.CreateMaterial(materials.Building),
                MaterialFactory.CreateMaterial(materials.BuildingBlue),
                MaterialFactory.CreateMaterial(materials.BuildingPurple),
                MaterialFactory.CreateMaterial(materials.BuildingWarm),
                MaterialFactory.CreateMaterial(materials.BuildingWeathered)
            };
        }
        _windowUnlitMaterial = MaterialFactory.CreateWindowUnlitMaterial(materials.WindowUnlit);
        _windowLitMaterial = MaterialFactory.CreateWindowLitMaterial(materials.WindowLit, windows);
        _ledgeMaterial = MaterialFactory.CreateMaterial(materials.Ledge);
        _wetRoadMaterial = MaterialFactory.CreateWetRoadMaterial(materials.Road, wetSurface);
        _wetSidewalkMaterial = MaterialFactory.CreateWetSidewalkMaterial(materials.Sidewalk, wetSurface);
        _roadMarkingMaterial = MaterialFactory.CreateMaterial(materials.RoadMarking);

        var lightColor = Color.FromHtml(streetLights.LightColor);
        _lampPoleMaterial = MaterialFactory.CreateLampPoleMaterial();
        _lampRingMaterial = MaterialFactory.CreateLampRingMaterial(lightColor);

        // Create facade detail material if enabled
        if (facadeDetails?.Enabled == true && facadeDetails.Material != null)
        {
            _facadeDetailMaterial = MaterialFactory.CreateMaterial(facadeDetails.Material);
        }

        // Create phased sub-renderers
        var groundRenderer = new GroundRenderer();
        var roadRenderer = new RoadRenderer(roads, roadMarkings);
        var manholeRenderer = new ManholeRenderer(manholes, smoke, roads);
        var pavementRenderer = new PlotPavementRenderer(roads);
        var buildingRenderer = new BuildingRenderer(facades, windows, facadeDetails, occlusionCulling);
        var neonRenderer = new NeonRenderer(neon);
        var streetLampRenderer = new StreetLampRenderer(streetLights, roads);

        _phasedRenderers[ChunkRenderPhase.Ground] = groundRenderer;
        _phasedRenderers[ChunkRenderPhase.Roads] = roadRenderer;
        _phasedRenderers[ChunkRenderPhase.Manholes] = manholeRenderer;
        _phasedRenderers[ChunkRenderPhase.Pavement] = pavementRenderer;
        _phasedRenderers[ChunkRenderPhase.Buildings] = buildingRenderer;
        _phasedRenderers[ChunkRenderPhase.Neons] = neonRenderer;
        _phasedRenderers[ChunkRenderPhase.StreetLamps] = streetLampRenderer;
    }

    /// <summary>
    /// Subscribe to a ChunkManager to receive load/unload events.
    /// </summary>
    public void SubscribeTo(ChunkManager manager)
    {
        manager.OnChunkLoaded += OnChunkLoaded;
        manager.OnChunkUnloaded += OnChunkUnloaded;
    }

    /// <summary>
    /// Unsubscribe from a ChunkManager.
    /// </summary>
    public void UnsubscribeFrom(ChunkManager manager)
    {
        manager.OnChunkLoaded -= OnChunkLoaded;
        manager.OnChunkUnloaded -= OnChunkUnloaded;
    }

    /// <summary>
    /// Clean up all resources including pooled nodes and cached materials.
    /// IMPORTANT: Caller must ensure this is called during scene teardown,
    /// after all chunk nodes have been removed from the scene tree.
    /// </summary>
    public void Cleanup()
    {
        // Clean up any in-progress renders first
        foreach (var state in _activeRenders)
        {
            state.Cleanup();
        }
        _activeRenders.Clear();
        _pendingRender.Clear();
        _renderingChunks.Clear();
        _pendingAdd.Clear();

        // Return all active chunk nodes to pool (queues them for freeing)
        foreach (var node in _chunkNodes.Values)
        {
            _nodePool.ReturnNode(node);
        }
        _chunkNodes.Clear();

        // Clean up the node pool (frees all pooled nodes)
        _nodePool.Cleanup();

        // Clear neon material cache (these are dynamically created per chunk)
        _materialFactory.ClearNeonMaterialCache();
        _materialFactory.DisposeTemplates();

        // Note: We intentionally do NOT dispose the constructor-created materials here.
        // They are Godot Resources (RefCounted) and may still be referenced by nodes
        // that are queued for freeing. Godot's resource system will handle cleanup
        // when all references are released. Explicit Dispose() here risks accessing
        // freed GPU resources if any node references persist.
    }

    /// <summary>
    /// Updates the camera position used for LOD calculations.
    /// Call this before ProcessRenderQueue() each frame.
    /// </summary>
    public void SetCameraPosition(Vector3 position)
    {
        _cameraPosition = position;
    }

    /// <summary>
    /// Process the render queue, advancing chunk rendering within the time budget.
    /// Call this from _Process() each frame.
    /// </summary>
    public void ProcessRenderQueue()
    {
        if (!_progressiveRenderingEnabled) return;

        var frameStart = Time.GetTicksUsec();
        var budget = _renderBudgetUsec;

        // Start new renders from pending queue
        while (_pendingRender.Count > 0 && _activeRenders.Count < _maxConcurrentRenders)
        {
            var state = _pendingRender.Dequeue();

            // Skip if chunk was unloaded while pending
            if (!_renderingChunks.ContainsKey(state.Coord))
                continue;

            StartChunkRender(state);
            _activeRenders.Add(state);
        }

        // Process active renders, prioritizing earlier phases
        // Sort in-place by phase to ensure ground/roads render before buildings (avoids LINQ allocation)
        _activeRenders.Sort((a, b) => a.CurrentPhase.CompareTo(b.CurrentPhase));

        foreach (var state in _activeRenders)
        {
            var elapsed = (long)(Time.GetTicksUsec() - frameStart);
            if (elapsed >= budget)
                break;

            var remaining = budget - elapsed;
            AdvanceRenderPhase(state, remaining);
        }

        // Remove completed renders
        for (int i = _activeRenders.Count - 1; i >= 0; i--)
        {
            var state = _activeRenders[i];
            if (state.CurrentPhase == ChunkRenderPhase.Complete)
            {
                state.Cleanup();
                _renderingChunks.Remove(state.Coord);
                _activeRenders.RemoveAt(i);
            }
        }
    }

    private void OnChunkLoaded(ChunkLoadedEvent e)
    {
        if (_chunkNodes.ContainsKey(e.Coord) || _renderingChunks.ContainsKey(e.Coord))
        {
            Log.Warn(LogCategory.Chunk, $"Chunk {e.Coord} already has a node or is rendering, skipping");
            return;
        }

        if (_progressiveRenderingEnabled)
        {
            // Queue for progressive rendering
            var state = new ChunkRenderState(e.Coord, e.Data);
            _renderingChunks[e.Coord] = state;
            _pendingRender.Enqueue(state);
        }
        else
        {
            // Immediate rendering (original behavior)
            var node = CreateChunkNodeImmediate(e.Data);
            _chunkNodes[e.Coord] = node;
            // Track pending add and defer safely to avoid race with unload
            _pendingAdd.Add(node);
            var coord = e.Coord;
            Callable.From(() => CompleteAddChild(node, coord, isProgressive: false)).CallDeferred();
        }
    }

    private Node3D CreateChunkNodeImmediate(ChunkData data)
    {
        var chunkNode = _nodePool.GetNode();
        chunkNode.Name = $"Chunk_{data.Coord.X}_{data.Coord.Z}";

        var (worldX, worldZ) = data.Coord.ToWorldPosition(data.Size);
        chunkNode.Position = new Vector3(worldX, 0, worldZ);

        var context = new ChunkRenderContext
        {
            ChunkWorldX = worldX,
            ChunkWorldZ = worldZ,
            ChunkSize = data.Size,
            CameraPosition = _cameraPosition,
            NodePool = _nodePool,
            MaterialFactory = _materialFactory,
            GroundMaterial = _groundMaterial,
            BuildingMaterials = _buildingMaterials,
            WindowUnlitMaterial = _windowUnlitMaterial,
            WindowLitMaterial = _windowLitMaterial,
            LedgeMaterial = _ledgeMaterial,
            WetRoadMaterial = _wetRoadMaterial,
            WetSidewalkMaterial = _wetSidewalkMaterial,
            RoadMarkingMaterial = _roadMarkingMaterial,
            LampPoleMaterial = _lampPoleMaterial,
            LampRingMaterial = _lampRingMaterial,
            FacadeDetailMaterial = _facadeDetailMaterial
        };

        // Render all phases immediately using ISubRenderer interface
        foreach (var renderer in _phasedRenderers.Values)
        {
            if (renderer is ISubRenderer subRenderer)
            {
                subRenderer.Render(chunkNode, data, context);
            }
        }

        if (_debugShowBoundaries)
            AddChunkDebugLines(chunkNode, data.Size);

        return chunkNode;
    }

    private void OnChunkUnloaded(ChunkUnloadedEvent e)
    {
        // Handle chunks that finished rendering
        if (_chunkNodes.TryGetValue(e.Coord, out var node))
        {
            _chunkNodes.Remove(e.Coord);
            // Only return to pool if not pending - CompleteAddChild will handle it
            if (!_pendingAdd.Contains(node))
            {
                _nodePool.ReturnNode(node);
            }
        }

        // Handle chunks still being rendered
        if (_renderingChunks.TryGetValue(e.Coord, out var state))
        {
            // Remove from active renders
            _activeRenders.Remove(state);

            // Clean up partial render - only return to pool if not pending
            if (state.ChunkNode != null && !_pendingAdd.Contains(state.ChunkNode))
            {
                _nodePool.ReturnNode(state.ChunkNode);
            }

            state.Cleanup();
            _renderingChunks.Remove(e.Coord);
        }
    }

    /// <summary>
    /// Completes a deferred AddChild operation, handling the case where the chunk
    /// was unloaded before the deferred call executed.
    /// </summary>
    private void CompleteAddChild(Node3D node, ChunkCoord coord, bool isProgressive)
    {
        _pendingAdd.Remove(node);

        if (!GodotObject.IsInstanceValid(node))
            return;

        // Check if chunk is still valid
        bool stillValid = isProgressive
            ? _renderingChunks.ContainsKey(coord)
            : _chunkNodes.ContainsKey(coord);

        if (stillValid)
        {
            _parent.AddChild(node);
        }
        else
        {
            // Chunk was unloaded while pending - return node to pool
            _nodePool.ReturnNode(node);
        }
    }

    private void StartChunkRender(ChunkRenderState state)
    {
        var chunkNode = _nodePool.GetNode();
        chunkNode.Name = $"Chunk_{state.Data.Coord.X}_{state.Data.Coord.Z}";

        var (worldX, worldZ) = state.Data.Coord.ToWorldPosition(state.Data.Size);
        chunkNode.Position = new Vector3(worldX, 0, worldZ);

        state.ChunkNode = chunkNode;
        state.Context = new ChunkRenderContext
        {
            ChunkWorldX = worldX,
            ChunkWorldZ = worldZ,
            ChunkSize = state.Data.Size,
            CameraPosition = _cameraPosition,
            NodePool = _nodePool,
            MaterialFactory = _materialFactory,
            GroundMaterial = _groundMaterial,
            BuildingMaterials = _buildingMaterials,
            WindowUnlitMaterial = _windowUnlitMaterial,
            WindowLitMaterial = _windowLitMaterial,
            LedgeMaterial = _ledgeMaterial,
            WetRoadMaterial = _wetRoadMaterial,
            WetSidewalkMaterial = _wetSidewalkMaterial,
            RoadMarkingMaterial = _roadMarkingMaterial,
            LampPoleMaterial = _lampPoleMaterial,
            LampRingMaterial = _lampRingMaterial,
            FacadeDetailMaterial = _facadeDetailMaterial
        };

        // Add node to scene - track pending add to avoid race with unload
        _pendingAdd.Add(chunkNode);
        var coord = state.Coord;
        Callable.From(() => CompleteAddChild(chunkNode, coord, isProgressive: true)).CallDeferred();

        // Start with first phase
        state.CurrentPhase = ChunkRenderPhase.Ground;
    }

    private void AdvanceRenderPhase(ChunkRenderState state, long budgetUsec)
    {
        if (state.CurrentPhase == ChunkRenderPhase.Pending ||
            state.CurrentPhase == ChunkRenderPhase.Complete)
            return;

        if (!_phasedRenderers.TryGetValue(state.CurrentPhase, out var renderer))
        {
            Log.Warn(LogCategory.Chunk, $"No renderer for phase {state.CurrentPhase}");
            MoveToNextPhase(state);
            return;
        }

        // Render within budget
        var phaseComplete = renderer.RenderIncremental(state, budgetUsec);

        if (phaseComplete)
        {
            MoveToNextPhase(state);
        }
    }

    private void MoveToNextPhase(ChunkRenderState state)
    {
        var currentIndex = System.Array.IndexOf(PhaseOrder, state.CurrentPhase);
        if (currentIndex >= 0 && currentIndex < PhaseOrder.Length - 1)
        {
            state.CurrentPhase = PhaseOrder[currentIndex + 1];
        }
        else
        {
            // All phases complete
            state.CurrentPhase = ChunkRenderPhase.Complete;

            // Move node reference to completed chunks
            if (state.ChunkNode != null)
            {
                _chunkNodes[state.Coord] = state.ChunkNode;
                if (_debugShowBoundaries)
                    AddChunkDebugLines(state.ChunkNode, state.Data.Size);
            }
        }
    }

    private static void AddChunkDebugLines(Node3D chunkNode, float size)
    {
        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0f, 1f, 0.8f), // Magenta
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);

        // Draw chunk boundary at y=0.1 (slightly above ground)
        float y = 0.1f;
        mesh.SurfaceAddVertex(new Vector3(0, y, 0));
        mesh.SurfaceAddVertex(new Vector3(size, y, 0));

        mesh.SurfaceAddVertex(new Vector3(size, y, 0));
        mesh.SurfaceAddVertex(new Vector3(size, y, size));

        mesh.SurfaceAddVertex(new Vector3(size, y, size));
        mesh.SurfaceAddVertex(new Vector3(0, y, size));

        mesh.SurfaceAddVertex(new Vector3(0, y, size));
        mesh.SurfaceAddVertex(new Vector3(0, y, 0));

        mesh.SurfaceEnd();

        var meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            Name = "ChunkDebugLines"
        };
        chunkNode.AddChild(meshInstance);
    }
}
