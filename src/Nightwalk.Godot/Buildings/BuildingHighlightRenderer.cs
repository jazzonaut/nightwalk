using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Buildings;
using Nightwalk.Core.Buildings.Events;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Collectibles;
using Nightwalk.Core.Collectibles.Events;
using Nightwalk.Core.Config;
using Nightwalk.Core.Events;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Godot.Rendering.Constants;
using Nightwalk.Godot.Rendering.Geometry;
using Nightwalk.Godot.Rendering.SubRenderers;

namespace Nightwalk.Godot.Buildings;

/// <summary>
/// Renders highlight overlays for climbed buildings.
/// Silhouettes appear on climb, beams/particles/windows appear on collectible pickup.
/// </summary>
public sealed partial class BuildingHighlightRenderer : Node3D
{
    // Collectible highlights (windows, beams, particles)
    private readonly Dictionary<BuildingId, (MeshInstance3D? windows, MeshInstance3D? beam, GpuParticles3D? particles)> _collectibleHighlights = new();
    // Silhouette highlights (triggered by climbing)
    private readonly Dictionary<BuildingId, MeshInstance3D> _silhouetteHighlights = new();
    private readonly List<BuildingId> _removeBuffer = new(); // Reusable buffer to avoid allocations
    private readonly ChunkManager _chunkManager;
    private readonly IEventBus _eventBus;
    private readonly GameDataService _data;
    private StandardMaterial3D? _windowMaterial;
    private ShaderMaterial? _beamMaterial;
    private ShaderMaterial? _particleMaterial;
    private ParticleProcessMaterial? _particleProcessMaterial;
    private QuadMesh? _particleQuadMesh;
    private ShaderMaterial? _silhouetteMaterial;
    private ImageTexture? _silhouetteAtlas;

    private FacadeConfig FacadeConfig => _data.Worldgen.Facades;
    private BuildingsConfig BuildingsConfig => _data.Gameplay.Buildings;
    private SilhouetteSettings SilhouetteConfig => _data.Aliveness.Silhouettes;
    private WindowConfig WindowConfig => _data.Worldgen.Windows;

    private const string BeamShaderPath = "res://shaders/beam_highlight.gdshader";
    private const string ParticleShaderPath = "res://shaders/highlight_particles.gdshader";
    private const string SilhouetteShaderPath = "res://shaders/window_silhouette.gdshader";
    private const float SilhouetteDepthOffset = 0.03f; // In front of window highlights

    public BuildingHighlightRenderer(
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
        Name = "BuildingHighlights";

        // Create materials (will be configured/updated via UpdateMaterials)
        _windowMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Back
        };

        var beamShader = GD.Load<Shader>(BeamShaderPath);
        _beamMaterial = new ShaderMaterial { Shader = beamShader };
        _beamMaterial.SetShaderParameter("fade_start", 0.1f);

        // Create particle material and mesh
        var particleShader = GD.Load<Shader>(ParticleShaderPath);
        _particleMaterial = new ShaderMaterial { Shader = particleShader };

        _particleQuadMesh = new QuadMesh();
        _particleQuadMesh.Material = _particleMaterial;

        _particleProcessMaterial = new ParticleProcessMaterial();

        // Create silhouette material and generate atlas
        var silhouetteShader = GD.Load<Shader>(SilhouetteShaderPath);
        _silhouetteMaterial = new ShaderMaterial { Shader = silhouetteShader };
        _silhouetteAtlas = SilhouetteAtlasGenerator.GenerateAtlas();
        _silhouetteMaterial.SetShaderParameter("silhouette_atlas", _silhouetteAtlas);

        UpdateMaterials();

        _eventBus.Subscribe<BuildingClimbedEvent>(OnBuildingClimbed);
        _eventBus.Subscribe<CollectibleCollectedEvent>(OnCollectibleCollected);
    }

    /// <summary>
    /// Updates material properties from current config. Call after hot reload.
    /// </summary>
    public void UpdateMaterials()
    {
        var config = BuildingsConfig;

        if (_windowMaterial != null)
        {
            var windowColor = Color.FromHtml(config.WindowHighlightColor);
            _windowMaterial.AlbedoColor = windowColor;
            _windowMaterial.Emission = windowColor;
            _windowMaterial.EmissionEnergyMultiplier = config.WindowHighlightIntensity;
        }

        if (_beamMaterial != null)
        {
            _beamMaterial.SetShaderParameter("beam_color", Color.FromHtml(config.BeamColor));
            _beamMaterial.SetShaderParameter("intensity", config.BeamIntensity);
        }

        // Particle color (use beam color if not specified)
        var particleColor = string.IsNullOrEmpty(config.ParticleColor)
            ? Color.FromHtml(config.BeamColor)
            : Color.FromHtml(config.ParticleColor);

        if (_particleMaterial != null)
        {
            _particleMaterial.SetShaderParameter("particle_color", particleColor);
        }

        if (_silhouetteMaterial != null)
        {
            var silhouetteConfig = SilhouetteConfig;
            _silhouetteMaterial.SetShaderParameter("silhouette_opacity", silhouetteConfig.Opacity);
            _silhouetteMaterial.SetShaderParameter("blur_radius", silhouetteConfig.BlurRadius);
        }

        if (_particleProcessMaterial != null && _particleQuadMesh != null)
        {
            // Configure particle process material - tight swirl around beam
            _particleProcessMaterial.Direction = new Vector3(0, 1, 0);
            _particleProcessMaterial.Spread = 0f; // No spread - straight up
            _particleProcessMaterial.InitialVelocityMin = config.ParticleRiseSpeed * 0.95f;
            _particleProcessMaterial.InitialVelocityMax = config.ParticleRiseSpeed * 1.05f;
            _particleProcessMaterial.Gravity = Vector3.Zero;

            // Ring emission - particles spawn at fixed distance from center
            _particleProcessMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
            _particleProcessMaterial.EmissionRingRadius = config.ParticleSpawnRadius;
            _particleProcessMaterial.EmissionRingInnerRadius = config.ParticleSpawnRadius * 0.8f;
            _particleProcessMaterial.EmissionRingHeight = 0.1f;
            _particleProcessMaterial.EmissionRingAxis = new Vector3(0, 1, 0);

            // Uniform scale for mechanical look
            _particleProcessMaterial.ScaleMin = config.ParticleSize * 0.9f;
            _particleProcessMaterial.ScaleMax = config.ParticleSize * 1.1f;

            // No random spin - more artificial
            _particleProcessMaterial.AngularVelocityMin = 0f;
            _particleProcessMaterial.AngularVelocityMax = 0f;

            // Strong orbit for swirling motion around beam
            _particleProcessMaterial.OrbitVelocityMin = 0.4f;
            _particleProcessMaterial.OrbitVelocityMax = 0.5f;

            // Minimal hue variation for uniform color
            _particleProcessMaterial.HueVariationMin = -0.02f;
            _particleProcessMaterial.HueVariationMax = 0.02f;

            // Color ramp for fade in/out over lifetime
            var colorRamp = new Gradient();
            colorRamp.SetColor(0, new Color(1, 1, 1, 0));      // Fade in
            colorRamp.SetColor(1, new Color(1, 1, 1, 0));      // Fade out
            colorRamp.AddPoint(0.1f, new Color(1, 1, 1, 1));   // Full opacity
            colorRamp.AddPoint(0.8f, new Color(1, 1, 1, 1));   // Full opacity
            var gradientTexture = new GradientTexture1D { Gradient = colorRamp };
            _particleProcessMaterial.ColorRamp = gradientTexture;

            // Update quad mesh size
            _particleQuadMesh.Size = new Vector2(config.ParticleSize, config.ParticleSize);
        }
    }

    public override void _ExitTree()
    {
        _eventBus.Unsubscribe<BuildingClimbedEvent>(OnBuildingClimbed);
        _eventBus.Unsubscribe<CollectibleCollectedEvent>(OnCollectibleCollected);
        _windowMaterial?.Dispose();
        _beamMaterial?.Dispose();
        _particleMaterial?.Dispose();
        _particleProcessMaterial?.Dispose();
        _particleQuadMesh?.Dispose();
        _silhouetteMaterial?.Dispose();
        _silhouetteAtlas?.Dispose();
    }

    private void OnBuildingClimbed(BuildingClimbedEvent e)
    {
        if (!SilhouetteConfig.Enabled)
            return;

        if (_silhouetteHighlights.ContainsKey(e.BuildingId))
            return;

        var chunk = _chunkManager.GetChunk(e.BuildingId.ChunkCoord);
        if (chunk == null || e.BuildingId.BuildingIndex >= chunk.Buildings.Count)
            return;

        var building = chunk.Buildings[e.BuildingId.BuildingIndex];
        CreateSilhouetteHighlight(e.BuildingId, building);
    }

    private void OnCollectibleCollected(CollectibleCollectedEvent e)
    {
        // Only highlight buildings, not empty plots or other collectible locations
        if (e.CollectibleId.LocationType != CollectibleLocationType.BuildingRooftop)
            return;

        var buildingId = new BuildingId(e.CollectibleId.ChunkCoord, e.CollectibleId.Index);

        if (_collectibleHighlights.ContainsKey(buildingId))
            return;

        var chunk = _chunkManager.GetChunk(buildingId.ChunkCoord);
        if (chunk == null || buildingId.BuildingIndex >= chunk.Buildings.Count)
            return;

        var building = chunk.Buildings[buildingId.BuildingIndex];
        CreateCollectibleHighlight(buildingId, building);
    }

    private void CreateSilhouetteHighlight(BuildingId id, BuildingData building)
    {
        if (_silhouetteMaterial == null)
            return;

        var position = new Vector3(building.X, 0, building.Z);
        int buildingSeed = building.Seed;

        var stSilhouettes = new SurfaceTool();
        stSilhouettes.Begin(Mesh.PrimitiveType.Triangles);

        if (building.IsStepped && building.Sections != null)
        {
            foreach (var section in building.Sections)
            {
                AddSilhouettesForSection(stSilhouettes, buildingSeed,
                    section.OffsetX, section.OffsetZ,
                    section.Width, section.Depth,
                    section.StartY, section.EndY,
                    section.Grid);
            }
        }
        else
        {
            AddSilhouettesForSection(stSilhouettes, buildingSeed, 0, 0, building.Width, building.Depth, 0, building.Height, building.Grid);
        }

        stSilhouettes.GenerateNormals();
        var silhouetteMesh = stSilhouettes.Commit();

        if (silhouetteMesh.GetSurfaceCount() > 0)
        {
            var silhouetteInstance = new MeshInstance3D
            {
                Mesh = silhouetteMesh,
                MaterialOverride = _silhouetteMaterial,
                Position = position,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };

            // Apply LOD - fade out silhouettes at distance
            if (SilhouetteConfig.VisibilityRange > 0)
            {
                silhouetteInstance.VisibilityRangeEnd = SilhouetteConfig.VisibilityRange;
                silhouetteInstance.VisibilityRangeEndMargin = SilhouetteConfig.VisibilityMargin;
                silhouetteInstance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
            }

            AddChild(silhouetteInstance);
            _silhouetteHighlights[id] = silhouetteInstance;
        }
    }

    private void CreateCollectibleHighlight(BuildingId id, BuildingData building)
    {
        var position = new Vector3(building.X, 0, building.Z);
        MeshInstance3D? windowInstance = null;
        MeshInstance3D? beamInstance = null;
        GpuParticles3D? particleInstance = null;

        // Get rooftop center position accounting for stepped sections and rooftop features
        var rooftop = BuildingRenderer.GetRooftopCenterPosition(building, FacadeConfig.RooftopFeatureChance);
        float rooftopY = rooftop.Y;  // Includes feature height
        float rooftopX = rooftop.X - building.X;  // Convert to local offset
        float rooftopZ = rooftop.Z - building.Z;

        // Create window highlights mesh (if enabled)
        if (BuildingsConfig.HighlightWindows)
        {
            var stWindows = new SurfaceTool();
            stWindows.Begin(Mesh.PrimitiveType.Triangles);

            if (building.IsStepped && building.Sections != null)
            {
                foreach (var section in building.Sections)
                {
                    AddWindowsForSection(stWindows,
                        section.OffsetX, section.OffsetZ,
                        section.Width, section.Depth,
                        section.StartY, section.EndY,
                        section.Grid);
                }
            }
            else
            {
                AddWindowsForSection(stWindows, 0, 0, building.Width, building.Depth, 0, building.Height, building.Grid);
            }

            stWindows.GenerateNormals();
            var windowMesh = stWindows.Commit();

            if (windowMesh.GetSurfaceCount() > 0)
            {
                windowInstance = new MeshInstance3D
                {
                    Mesh = windowMesh,
                    MaterialOverride = _windowMaterial,
                    Position = position,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                };
                AddChild(windowInstance);
            }
        }

        // Create single rooftop beam (if enabled)
        if (BuildingsConfig.HighlightBeam)
        {
            var stBeam = new SurfaceTool();
            stBeam.Begin(Mesh.PrimitiveType.Triangles);
            AddRooftopBeam(stBeam, rooftopX, rooftopY, rooftopZ);
            stBeam.GenerateNormals();
            var beamMesh = stBeam.Commit();

            if (beamMesh.GetSurfaceCount() > 0)
            {
                beamInstance = new MeshInstance3D
                {
                    Mesh = beamMesh,
                    MaterialOverride = _beamMaterial,
                    Position = position,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                };
                AddChild(beamInstance);
            }
        }

        // Create rising particles (if enabled)
        if (BuildingsConfig.HighlightParticles && _particleProcessMaterial != null && _particleQuadMesh != null)
        {
            particleInstance = new GpuParticles3D
            {
                ProcessMaterial = _particleProcessMaterial,
                DrawPass1 = _particleQuadMesh,
                Amount = BuildingsConfig.ParticleCount,
                Lifetime = BuildingsConfig.ParticleLifetime,
                Preprocess = 1.0f, // Start with some particles already visible
                Position = position + new Vector3(rooftopX, rooftopY + BuildingsConfig.ParticleStartOffset, rooftopZ),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // AABB sized to actual particle bounds with margin for viewing angles
                // Horizontal: spawn radius + particle size buffer
                // Vertical: extends below emitter for camera viewing from below, up to max travel height
                VisibilityAabb = new Aabb(
                    new Vector3(
                        -BuildingsConfig.ParticleSpawnRadius - 5f,
                        -100f,  // Below emitter for viewing from lower elevations
                        -BuildingsConfig.ParticleSpawnRadius - 5f),
                    new Vector3(
                        (BuildingsConfig.ParticleSpawnRadius + 5f) * 2f,
                        BuildingsConfig.ParticleLifetime * BuildingsConfig.ParticleRiseSpeed + 110f,
                        (BuildingsConfig.ParticleSpawnRadius + 5f) * 2f)
                )
            };

            // Apply LOD - fade out particles at distance
            if (BuildingsConfig.ParticleVisibilityRange > 0)
            {
                particleInstance.VisibilityRangeEnd = BuildingsConfig.ParticleVisibilityRange;
                particleInstance.VisibilityRangeEndMargin = BuildingsConfig.ParticleVisibilityMargin;
                particleInstance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
            }

            AddChild(particleInstance);
        }

        if (windowInstance != null || beamInstance != null || particleInstance != null)
        {
            _collectibleHighlights[id] = (windowInstance, beamInstance, particleInstance);
        }
    }

    private void AddRooftopBeam(SurfaceTool st, float offsetX, float rooftopY, float offsetZ)
    {
        // Single beam from rooftop center, offset above roof to avoid roof obstruction
        var startY = rooftopY + BuildingsConfig.BeamStartOffset;
        AddBeamQuads(st, offsetX, startY, offsetZ, BuildingsConfig.BeamWidth, BuildingsConfig.BeamHeight);
    }

    private static void AddBeamQuads(SurfaceTool st, float cx, float baseY, float cz, float width, float height)
    {
        // Create two crossing quads for volumetric beam effect (billboard-like)
        var hw = width / 2f;

        // Quad 1: along X axis
        var a1 = new Vector3(cx - hw, baseY, cz);
        var b1 = new Vector3(cx + hw, baseY, cz);
        var c1 = new Vector3(cx + hw, baseY + height, cz);
        var d1 = new Vector3(cx - hw, baseY + height, cz);

        // Quad 2: along Z axis
        var a2 = new Vector3(cx, baseY, cz - hw);
        var b2 = new Vector3(cx, baseY, cz + hw);
        var c2 = new Vector3(cx, baseY + height, cz + hw);
        var d2 = new Vector3(cx, baseY + height, cz - hw);

        // Add both quads with UV for height fade (UV.y = 0 at bottom, 1 at top)
        AddBeamQuad(st, a1, b1, c1, d1);
        AddBeamQuad(st, a2, b2, c2, d2);
    }

    private static void AddBeamQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        // Triangle 1: a-b-c
        st.SetUV(new Vector2(0, 0)); st.AddVertex(a);
        st.SetUV(new Vector2(1, 0)); st.AddVertex(b);
        st.SetUV(new Vector2(1, 1)); st.AddVertex(c);

        // Triangle 2: a-c-d
        st.SetUV(new Vector2(0, 0)); st.AddVertex(a);
        st.SetUV(new Vector2(1, 1)); st.AddVertex(c);
        st.SetUV(new Vector2(0, 1)); st.AddVertex(d);
    }

    private void AddWindowsForSection(SurfaceTool st, float offsetX, float offsetZ,
        float width, float depth, float baseY, float maxY, in FacadeGrid grid)
    {
        var windowWidth = FacadeConfig.WindowWidth;
        var windowHeight = FacadeConfig.WindowHeight;

        // Use pre-computed grid values
        var windowsX = grid.ColumnsX;
        var windowsZ = grid.ColumnsZ;
        var startOffsetX = grid.StartOffsetX;
        var startOffsetZ = grid.StartOffsetZ;
        var floorHeight = grid.FloorHeight;
        var groundFloorHeight = grid.GroundFloorHeight;
        var floorsInSection = grid.NumFloors;

        if (windowsX <= 0 && windowsZ <= 0)
            return;

        for (int floor = 0; floor < floorsInSection; floor++)
        {
            var windowY = baseY + groundFloorHeight + floor * floorHeight + floorHeight * RenderingConstants.FloorHeightOffsetFactor;
            if (windowY + windowHeight / 2 > maxY - RenderingConstants.SectionEndMargin)
                continue;

            // Front and back facades (along X axis)
            for (int i = 0; i < windowsX; i++)
            {
                var wx = offsetX - width / 2f + startOffsetX + i * grid.WindowSpacing;

                // Front window (+Z)
                var frontZ = offsetZ + depth / 2f + RenderingConstants.WindowHighlightDepthOffset;
                GeometryBuilder.AddWindowQuad(st, wx, windowY, frontZ, windowWidth, windowHeight, FaceDirection.Front);

                // Back window (-Z)
                var backZ = offsetZ - depth / 2f - RenderingConstants.WindowHighlightDepthOffset;
                GeometryBuilder.AddWindowQuad(st, wx, windowY, backZ, windowWidth, windowHeight, FaceDirection.Back);
            }

            // Left and right facades (along Z axis)
            for (int i = 0; i < windowsZ; i++)
            {
                var wz = offsetZ - depth / 2f + startOffsetZ + i * grid.WindowSpacing;

                // Right window (+X)
                var rightX = offsetX + width / 2f + RenderingConstants.WindowHighlightDepthOffset;
                GeometryBuilder.AddWindowQuad(st, rightX, windowY, wz, windowWidth, windowHeight, FaceDirection.Right);

                // Left window (-X)
                var leftX = offsetX - width / 2f - RenderingConstants.WindowHighlightDepthOffset;
                GeometryBuilder.AddWindowQuad(st, leftX, windowY, wz, windowWidth, windowHeight, FaceDirection.Left);
            }
        }
    }

    private void AddSilhouettesForSection(SurfaceTool st, int buildingSeed, float offsetX, float offsetZ,
        float width, float depth, float baseY, float maxY, in FacadeGrid grid)
    {
        var windowWidth = FacadeConfig.WindowWidth;
        var windowHeight = FacadeConfig.WindowHeight;

        var windowsX = grid.ColumnsX;
        var windowsZ = grid.ColumnsZ;
        var startOffsetX = grid.StartOffsetX;
        var startOffsetZ = grid.StartOffsetZ;
        var floorHeight = grid.FloorHeight;
        var groundFloorHeight = grid.GroundFloorHeight;
        var floorsInSection = grid.NumFloors;

        if (windowsX <= 0 && windowsZ <= 0)
            return;

        var config = SilhouetteConfig;
        var windowCfg = WindowConfig;

        // Calculate starting floor number to get absolute floor (matching BuildingRenderer)
        var startFloorNum = (int)((baseY + groundFloorHeight) / floorHeight);

        for (int floor = 0; floor < floorsInSection; floor++)
        {
            var windowY = baseY + groundFloorHeight + floor * floorHeight + floorHeight * RenderingConstants.FloorHeightOffsetFactor;
            if (windowY + windowHeight / 2 > maxY - RenderingConstants.SectionEndMargin)
                continue;

            var absoluteFloor = startFloorNum + floor;
            int windowIndex = 0;  // Reset per floor to match BuildingRenderer

            // Front and back facades (along X axis)
            for (int i = 0; i < windowsX; i++)
            {
                var wx = offsetX - width / 2f + startOffsetX + i * grid.WindowSpacing;

                // Front window (+Z) - must match BuildingRenderer's windowIndex ordering
                var frontState = SilhouetteState.Generate(buildingSeed, absoluteFloor, windowIndex,
                    config.Density, windowCfg);

                if (frontState.HasSilhouette)
                {
                    var uv = frontState.EncodeUV();
                    var uv2 = frontState.EncodeUV2();
                    var frontZ = offsetZ + depth / 2f + SilhouetteDepthOffset;
                    AddSilhouetteQuad(st, wx, windowY, frontZ, windowWidth, windowHeight, FaceDirection.Front, uv, uv2, frontState.BlurMultiplier);
                }
                windowIndex++;

                // Back window (-Z)
                var backState = SilhouetteState.Generate(buildingSeed, absoluteFloor, windowIndex,
                    config.Density, windowCfg);

                if (backState.HasSilhouette)
                {
                    var uv = backState.EncodeUV();
                    var uv2 = backState.EncodeUV2();
                    var backZ = offsetZ - depth / 2f - SilhouetteDepthOffset;
                    AddSilhouetteQuad(st, wx, windowY, backZ, windowWidth, windowHeight, FaceDirection.Back, uv, uv2, backState.BlurMultiplier);
                }
                windowIndex++;
            }

            // Left and right facades (along Z axis)
            for (int i = 0; i < windowsZ; i++)
            {
                var wz = offsetZ - depth / 2f + startOffsetZ + i * grid.WindowSpacing;

                // Right window (+X) - must match BuildingRenderer's windowIndex ordering
                var rightState = SilhouetteState.Generate(buildingSeed, absoluteFloor, windowIndex,
                    config.Density, windowCfg);

                if (rightState.HasSilhouette)
                {
                    var uv = rightState.EncodeUV();
                    var uv2 = rightState.EncodeUV2();
                    var rightX = offsetX + width / 2f + SilhouetteDepthOffset;
                    AddSilhouetteQuad(st, rightX, windowY, wz, windowWidth, windowHeight, FaceDirection.Right, uv, uv2, rightState.BlurMultiplier);
                }
                windowIndex++;

                // Left window (-X)
                var leftState = SilhouetteState.Generate(buildingSeed, absoluteFloor, windowIndex,
                    config.Density, windowCfg);

                if (leftState.HasSilhouette)
                {
                    var uv = leftState.EncodeUV();
                    var uv2 = leftState.EncodeUV2();
                    var leftX = offsetX - width / 2f - SilhouetteDepthOffset;
                    AddSilhouetteQuad(st, leftX, windowY, wz, windowWidth, windowHeight, FaceDirection.Left, uv, uv2, leftState.BlurMultiplier);
                }
                windowIndex++;
            }
        }
    }

    private static void AddSilhouetteQuad(SurfaceTool st, float cx, float cy, float cz,
        float width, float height, FaceDirection direction, Vector2 uv, Vector2 uv2, float blurMultiplier)
    {
        var hw = width / 2f;
        var hh = height / 2f;

        Vector3 a, b, c, d;
        Vector3 normal;

        switch (direction)
        {
            case FaceDirection.Front:
                a = new Vector3(cx + hw, cy - hh, cz);
                b = new Vector3(cx - hw, cy - hh, cz);
                c = new Vector3(cx - hw, cy + hh, cz);
                d = new Vector3(cx + hw, cy + hh, cz);
                normal = new Vector3(0, 0, 1);
                break;
            case FaceDirection.Back:
                a = new Vector3(cx - hw, cy - hh, cz);
                b = new Vector3(cx + hw, cy - hh, cz);
                c = new Vector3(cx + hw, cy + hh, cz);
                d = new Vector3(cx - hw, cy + hh, cz);
                normal = new Vector3(0, 0, -1);
                break;
            case FaceDirection.Right:
                a = new Vector3(cx, cy - hh, cz - hw);
                b = new Vector3(cx, cy - hh, cz + hw);
                c = new Vector3(cx, cy + hh, cz + hw);
                d = new Vector3(cx, cy + hh, cz - hw);
                normal = new Vector3(1, 0, 0);
                break;
            case FaceDirection.Left:
            default:
                a = new Vector3(cx, cy - hh, cz + hw);
                b = new Vector3(cx, cy - hh, cz - hw);
                c = new Vector3(cx, cy + hh, cz - hw);
                d = new Vector3(cx, cy + hh, cz + hw);
                normal = new Vector3(-1, 0, 0);
                break;
        }

        // Local quad UVs (0,0 to 1,1) passed through COLOR.xy attribute
        // COLOR.z = blur multiplier (depth from window: low=sharp/close, high=fuzzy/far)
        // a = bottom-right, b = bottom-left, c = top-left, d = top-right
        var uvA = new Color(1, 0, blurMultiplier, 1);  // (1, 0)
        var uvB = new Color(0, 0, blurMultiplier, 1);  // (0, 0)
        var uvC = new Color(0, 1, blurMultiplier, 1);  // (0, 1)
        var uvD = new Color(1, 1, blurMultiplier, 1);  // (1, 1)

        // Triangle 1: a-b-c
        st.SetColor(uvA);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(a);

        st.SetColor(uvB);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(b);

        st.SetColor(uvC);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(c);

        // Triangle 2: a-c-d
        st.SetColor(uvA);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(a);

        st.SetColor(uvC);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(c);

        st.SetColor(uvD);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(d);
    }

    public void OnChunkUnloaded(ChunkCoord coord)
    {
        _removeBuffer.Clear();

        // Remove collectible highlights
        foreach (var (id, (windows, beams, particles)) in _collectibleHighlights)
        {
            if (id.ChunkCoord.Equals(coord))
            {
                windows?.QueueFree();
                beams?.QueueFree();
                particles?.QueueFree();
                _removeBuffer.Add(id);
            }
        }

        foreach (var id in _removeBuffer)
        {
            _collectibleHighlights.Remove(id);
        }

        // Remove silhouette highlights
        _removeBuffer.Clear();
        foreach (var (id, silhouette) in _silhouetteHighlights)
        {
            if (id.ChunkCoord.Equals(coord))
            {
                silhouette?.QueueFree();
                _removeBuffer.Add(id);
            }
        }

        foreach (var id in _removeBuffer)
        {
            _silhouetteHighlights.Remove(id);
        }
    }

    public void OnChunkLoaded(ChunkCoord coord, ChunkData data, CollectibleTracker collectibleTracker, BuildingTracker buildingTracker)
    {
        for (int i = 0; i < data.Buildings.Count; i++)
        {
            var collectibleId = CollectibleId.ForBuilding(coord, i);
            var buildingId = new BuildingId(coord, i);
            var building = data.Buildings[i];

            // Restore silhouettes for climbed buildings
            if (SilhouetteConfig.Enabled && buildingTracker.IsClimbed(buildingId) && !_silhouetteHighlights.ContainsKey(buildingId))
            {
                CreateSilhouetteHighlight(buildingId, building);
            }

            // Restore collectible highlights for collected buildings
            if (collectibleTracker.IsCollected(collectibleId) && !_collectibleHighlights.ContainsKey(buildingId))
            {
                CreateCollectibleHighlight(buildingId, building);
            }
        }
    }

    public void Reset()
    {
        foreach (var (windows, beams, particles) in _collectibleHighlights.Values)
        {
            windows?.QueueFree();
            beams?.QueueFree();
            particles?.QueueFree();
        }
        _collectibleHighlights.Clear();

        foreach (var silhouette in _silhouetteHighlights.Values)
        {
            silhouette?.QueueFree();
        }
        _silhouetteHighlights.Clear();
    }
}
