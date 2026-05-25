using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.CityAliveness;
using Nightwalk.Core.CityAliveness.Events;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Godot.CityAliveness;

/// <summary>
/// Renders animated car trail effects on alive roads.
/// </summary>
public sealed partial class CarTrailRenderer : Node3D
{
    private readonly Dictionary<RoadId, MeshInstance3D> _trailMeshes = new();
    private readonly List<RoadId> _removeBuffer = new();
    private readonly ChunkManager _chunkManager;
    private readonly IEventBus _eventBus;
    private readonly GameDataService _data;
    private Shader? _shader;

    private CarTrailsSettings Config => _data.Aliveness.CarTrails;
    private RoadConfig RoadConfig => _data.Worldgen.Roads;

    private const string ShaderPath = "res://shaders/car_trails.gdshader";

    public CarTrailRenderer(
        ChunkManager chunkManager,
        IEventBus eventBus,
        GameDataService data)
    {
        _chunkManager = chunkManager;
        _eventBus = eventBus;
        _data = data;
    }

    public override void _Ready()
    {
        Name = "CarTrailRenderer";

        _shader = GD.Load<Shader>(ShaderPath);
        _eventBus.Subscribe<RoadAlivenessChangedEvent>(OnRoadAlivenessChanged);
    }

    public override void _ExitTree()
    {
        _eventBus.Unsubscribe<RoadAlivenessChangedEvent>(OnRoadAlivenessChanged);
    }

    private void OnRoadAlivenessChanged(RoadAlivenessChangedEvent e)
    {
        if (!Config.Enabled)
            return;

        if (_trailMeshes.ContainsKey(e.RoadId))
            return;

        var chunk = _chunkManager.GetChunk(e.RoadId.ChunkCoord);
        if (chunk == null)
            return;

        Log.Debug(LogCategory.Sim, $"Creating trail mesh for {e.RoadId}");
        CreateTrailMesh(e.RoadId, e.Direction, e.Position, e.HalfWidth, chunk);
    }

    private void CreateTrailMesh(RoadId roadId, RoadDirection direction, float roadPosition, float halfWidth, ChunkData chunk)
    {
        var chunkSize = _chunkManager.ChunkSize;
        var (chunkMinX, chunkMinZ) = chunk.Coord.ToWorldPosition(chunkSize);

        // Create material for this road
        var material = new ShaderMaterial { Shader = _shader };
        ConfigureMaterial(material, direction, halfWidth, roadPosition);

        // Create mesh based on road direction
        ArrayMesh mesh;
        Vector3 position;

        if (direction == RoadDirection.EastWest)
        {
            // EW road: runs along X axis, positioned at Z = roadPosition
            // Mesh spans the full chunk width
            mesh = CreateRoadQuad(chunkSize, halfWidth * 2f);
            position = new Vector3(chunkMinX + chunkSize / 2f, GetTrailHeight(), roadPosition);
        }
        else
        {
            // NS road: runs along Z axis, positioned at X = roadPosition
            mesh = CreateRoadQuad(halfWidth * 2f, chunkSize);
            position = new Vector3(roadPosition, GetTrailHeight(), chunkMinZ + chunkSize / 2f);
        }

        var meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            Position = position,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };

        ConfigureVisibility(meshInstance);

        AddChild(meshInstance);
        _trailMeshes[roadId] = meshInstance;
    }

    private void ConfigureMaterial(ShaderMaterial material, RoadDirection direction, float halfWidth, float roadCenter)
    {
        material.SetShaderParameter("road_half_width", halfWidth);
        material.SetShaderParameter("road_center", roadCenter);
        material.SetShaderParameter("is_east_west", direction == RoadDirection.EastWest);
        material.SetShaderParameter("lanes_per_direction", Config.LanesPerDirection);
        material.SetShaderParameter("scroll_speed", Config.ScrollSpeed);
        material.SetShaderParameter("scroll_speed_variation", Config.ScrollSpeedVariation);
        material.SetShaderParameter("streak_color", Color.FromHtml(Config.StreakColor));
        material.SetShaderParameter("streak_color_oncoming", Color.FromHtml(Config.StreakColorOncoming));
        material.SetShaderParameter("emission_strength", Config.EmissionStrength);
        material.SetShaderParameter("trail_width", Config.TrailWidth);
        material.SetShaderParameter("fuzziness", Config.Fuzziness);

        // Enhanced visual effects
        material.SetShaderParameter("motion_blur_strength", Config.MotionBlurStrength);
        material.SetShaderParameter("bloom_boost", Config.BloomBoost);
    }

    private float GetTrailHeight()
    {
        return RoadConfig.RoadHeight + Config.HeightOffset;
    }

    private ArrayMesh CreateRoadQuad(float width, float depth)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float hw = width / 2f;
        float hd = depth / 2f;

        // Quad vertices (facing up)
        // Note: position is local to mesh, world position set via MeshInstance3D.Position
        var a = new Vector3(-hw, 0, -hd);
        var b = new Vector3(hw, 0, -hd);
        var c = new Vector3(hw, 0, hd);
        var d = new Vector3(-hw, 0, hd);

        st.SetNormal(Vector3.Up);

        // Triangle 1: a-b-c
        st.AddVertex(a);
        st.AddVertex(b);
        st.AddVertex(c);

        // Triangle 2: a-c-d
        st.AddVertex(a);
        st.AddVertex(c);
        st.AddVertex(d);

        return st.Commit();
    }

    /// <summary>
    /// Called when a chunk is unloaded to clean up trail meshes.
    /// </summary>
    public void OnChunkUnloaded(ChunkCoord coord)
    {
        _removeBuffer.Clear();

        foreach (var (roadId, meshInstance) in _trailMeshes)
        {
            if (roadId.ChunkCoord.Equals(coord))
            {
                meshInstance.QueueFree();
                _removeBuffer.Add(roadId);
            }
        }

        foreach (var roadId in _removeBuffer)
        {
            _trailMeshes.Remove(roadId);
        }
    }

    /// <summary>
    /// Called when a chunk is loaded to restore trail meshes for alive roads.
    /// </summary>
    public void OnChunkLoaded(ChunkCoord coord, ChunkData data, CityAlivenessService alivenessService)
    {
        if (!Config.Enabled)
            return;

        for (int i = 0; i < data.Roads.Count; i++)
        {
            var roadId = new RoadId(coord, i);
            if (alivenessService.IsAlive(roadId) && !_trailMeshes.ContainsKey(roadId))
            {
                var road = data.Roads[i];
                CreateTrailMesh(roadId, road.Direction, road.Position, road.HalfWidth, data);
            }
        }
    }

    /// <summary>
    /// Updates material and visibility properties from current config. Call after hot reload.
    /// </summary>
    public void UpdateMaterials()
    {
        foreach (var (roadId, meshInstance) in _trailMeshes)
        {
            var chunk = _chunkManager.GetChunk(roadId.ChunkCoord);
            if (chunk == null || roadId.RoadIndex >= chunk.Roads.Count)
                continue;

            var road = chunk.Roads[roadId.RoadIndex];
            if (meshInstance.MaterialOverride is ShaderMaterial material)
            {
                ConfigureMaterial(material, road.Direction, road.HalfWidth, road.Position);
            }

            // Update visibility LOD settings
            ConfigureVisibility(meshInstance);
        }
    }

    private void ConfigureVisibility(MeshInstance3D meshInstance)
    {
        if (Config.VisibilityRange > 0)
        {
            meshInstance.VisibilityRangeEnd = Config.VisibilityRange;
            meshInstance.VisibilityRangeEndMargin = Config.VisibilityMargin;
            meshInstance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        }
        else
        {
            meshInstance.VisibilityRangeEnd = 0;
            meshInstance.VisibilityRangeEndMargin = 0;
        }
    }

    /// <summary>
    /// Resets all trail meshes (for session reset).
    /// </summary>
    public void Reset()
    {
        foreach (var meshInstance in _trailMeshes.Values)
        {
            meshInstance.QueueFree();
        }
        _trailMeshes.Clear();
    }
}
