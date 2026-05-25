using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Core.World.Neon;
using Nightwalk.Godot.Rendering;
using Nightwalk.Godot.Rendering.Constants;
using Nightwalk.Godot.Rendering.Geometry;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Represents a box collider to be created.
/// </summary>
public readonly struct BoxCollider
{
    public readonly float CenterX, CenterY, CenterZ;
    public readonly float Width, Height, Depth;

    public BoxCollider(float cx, float cy, float cz, float w, float h, float d)
    {
        CenterX = cx; CenterY = cy; CenterZ = cz;
        Width = w; Height = h; Depth = d;
    }
}

/// <summary>
/// Floor lighting pattern for a building floor.
/// </summary>
public enum FloorPattern
{
    Normal,
    MostlyDark,
    FullyLit,
    Checkerboard
}

/// <summary>
/// Renders buildings including facades, windows, ledges, and rooftops.
/// Supports incremental rendering to spread work across multiple frames.
/// </summary>
public sealed class BuildingRenderer : ISubRenderer, IPhasedSubRenderer
{
    public ChunkRenderPhase Phase => ChunkRenderPhase.Buildings;

    // Hash purpose constants for deterministic generation (avoids Random allocations)
    private const int PurposeMaterial = 0;
    public const int PurposeFloorPattern = 5;
    public const int PurposeWindowLit = 6;

    // Public rooftop constants - shared with RooftopDetector
    public const int PurposeRooftopChance = 1;
    public const int PurposeRooftopWidth = 2;
    public const int PurposeRooftopDepth = 3;
    public const int PurposeRooftopHeight = 4;

    private readonly FacadeConfig _facadeConfig;
    private readonly WindowConfig _windowConfig;
    private readonly FacadeDetailGenerator? _detailGenerator;
    private readonly OcclusionCullingConfig? _occlusionConfig;

    // Reusable buffer for blocking signs to avoid per-building allocations
    private readonly List<NeonLightData> _blockingSignsBuffer = new(16);

    /// <summary>
    /// Represents rooftop feature dimensions calculated deterministically from building seed.
    /// </summary>
    public readonly struct RooftopFeatureDimensions
    {
        public readonly float Width;
        public readonly float Height;
        public readonly float Depth;
        public readonly bool HasFeature;

        public RooftopFeatureDimensions(float width, float height, float depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
            HasFeature = true;
        }

        public static readonly RooftopFeatureDimensions None = default;
    }

    /// <summary>
    /// Represents the center position on a building's rooftop, accounting for
    /// stepped sections and rooftop features.
    /// </summary>
    public readonly struct RooftopCenterPosition
    {
        public readonly float X;
        public readonly float BaseY;
        public readonly float FeatureHeight;
        public readonly float Z;
        public readonly float BaseWidth;
        public readonly float BaseDepth;

        /// <summary>
        /// Total Y position (BaseY + FeatureHeight).
        /// </summary>
        public float Y => BaseY + FeatureHeight;

        public RooftopCenterPosition(float x, float baseY, float featureHeight, float z, float baseWidth, float baseDepth)
        {
            X = x;
            BaseY = baseY;
            FeatureHeight = featureHeight;
            Z = z;
            BaseWidth = baseWidth;
            BaseDepth = baseDepth;
        }
    }

    public BuildingRenderer(FacadeConfig facadeConfig, WindowConfig windowConfig, FacadeDetailConfig? detailConfig = null, OcclusionCullingConfig? occlusionConfig = null)
    {
        _facadeConfig = facadeConfig;
        _windowConfig = windowConfig;
        _occlusionConfig = occlusionConfig;

        if (detailConfig?.Enabled == true)
        {
            _detailGenerator = new FacadeDetailGenerator(
                detailConfig,
                facadeConfig.FloorHeight);
        }
    }

    public void Render(Node3D parent, ChunkData data, ChunkRenderContext context)
    {
        if (data.Buildings.Count == 0) return;

        // Reuse incremental implementation with unlimited budget.
        // Should complete in a single iteration, but cap iterations to catch regressions.
        var state = new ChunkRenderState(data.Coord, data)
        {
            ChunkNode = parent,
            Context = context
        };

        const int maxIterations = 100;
        int iterations = 0;
        while (!RenderIncremental(state, long.MaxValue))
        {
            if (++iterations >= maxIterations)
            {
                GD.PushError($"BuildingRenderer.Render exceeded {maxIterations} iterations - likely infinite loop bug");
                state.Cleanup();
                break;
            }
        }
    }

    public bool RenderIncremental(ChunkRenderState state, long budgetUsec)
    {
        if (state.ChunkNode == null || state.Context == null) return true;
        if (state.Data.Buildings.Count == 0) return true;

        var context = state.Context;
        var startTime = Time.GetTicksUsec();

        // Initialize MeshBuilders on first call
        if (!state.BuildingMBsInitialized)
        {
            InitializeMeshBuilders(state, context);
            state.BuildingMBsInitialized = true;
        }

        // Process buildings incrementally
        while (state.BuildingIndex < state.Data.Buildings.Count)
        {
            var building = state.Data.Buildings[state.BuildingIndex];
            var localX = building.X - context.ChunkWorldX;
            var localZ = building.Z - context.ChunkWorldZ;

            var materialIndex = StableHash.ToInt(building.Seed, PurposeMaterial, context.BuildingMaterials.Length);
            GetBlockingSignsForBuilding(state.Data.Neons, building, _blockingSignsBuffer);

            CollectBuildingCollisionBoxes(state.CollisionBoxes!, building, localX, localZ);
            AddBuildingGeometry(state.BuildingBodyMBs![materialIndex], state.LedgeMB!, state.WindowLitMB!, state.WindowUnlitMB!, building, localX, localZ, _blockingSignsBuffer);
            _detailGenerator?.AddFacadeDetails(state.DetailsMB!, building, localX, localZ);

            state.BuildingIndex++;

            // Check time budget every few buildings
            if (state.BuildingIndex % 3 == 0)
            {
                var elapsed = (long)(Time.GetTicksUsec() - startTime);
                if (elapsed >= budgetUsec)
                    return false; // More work needed
            }
        }

        // All buildings processed - finalize in incremental steps to avoid frame spikes
        // Step 0: Create collision shapes incrementally
        // Step 1: Commit meshes and create MeshInstance3D
        // Step 2: Create occluders (complete)
        while (state.BuildingFinalizeStep < 3)
        {
            var elapsed = (long)(Time.GetTicksUsec() - startTime);
            if (elapsed >= budgetUsec)
                return false; // Defer remaining steps to next frame

            switch (state.BuildingFinalizeStep)
            {
                case 0:
                    // Incremental collision creation - may take multiple frames
                    if (!FinalizeCollisionsIncremental(state, budgetUsec - elapsed))
                        return false; // More collision shapes to add
                    break;
                case 1:
                    // Incremental mesh commits - may take multiple frames
                    if (!FinalizeMeshesIncremental(state, context, budgetUsec - elapsed))
                        return false; // More surfaces to commit
                    break;
                case 2:
                    if (_occlusionConfig?.Enabled == true && state.CollisionBoxes!.Count > 0)
                        CreateOccluders(state.ChunkNode!, state.CollisionBoxes!);
                    state.BuildingMeshFinalized = true;
                    break;
            }
            state.BuildingFinalizeStep++;
        }

        return true; // Phase complete
    }

    private void InitializeMeshBuilders(ChunkRenderState state, ChunkRenderContext context)
    {
        state.BuildingBodyMBs = new MeshBuilder[context.BuildingMaterials.Length];
        for (int i = 0; i < state.BuildingBodyMBs.Length; i++)
        {
            state.BuildingBodyMBs[i] = new MeshBuilder();
        }

        state.LedgeMB = new MeshBuilder();
        state.WindowLitMB = new MeshBuilder();
        state.WindowUnlitMB = new MeshBuilder();
        state.DetailsMB = new MeshBuilder();

        // Pre-size for ~2 boxes per building average
        state.CollisionBoxes = new List<BoxCollider>(state.Data.Buildings.Count * 2);
    }

    /// <summary>
    /// Creates collision shapes incrementally to spread work across frames.
    /// Returns true when all collision shapes have been added.
    /// </summary>
    private static bool FinalizeCollisionsIncremental(ChunkRenderState state, long budgetUsec)
    {
        var collisionBoxes = state.CollisionBoxes!;
        if (collisionBoxes.Count == 0) return true;

        var startTime = Time.GetTicksUsec();

        // Create and add StaticBody3D to scene tree on first call
        // Adding it first means each shape addition triggers incremental physics updates
        // rather than one big update when the body enters the tree with all shapes
        if (state.CollisionBody == null)
        {
            state.CollisionBody = new StaticBody3D { Name = "BuildingCollisions" };
            state.ChunkNode!.AddChild(state.CollisionBody);
        }

        // Process collision boxes incrementally
        const int CheckBudgetInterval = 5; // Check budget every N shapes
        while (state.CollisionBoxIndex < collisionBoxes.Count)
        {
            var box = collisionBoxes[state.CollisionBoxIndex];
            var boxShape = new BoxShape3D { Size = new Vector3(box.Width, box.Height, box.Depth) };
            var collisionShape = new CollisionShape3D
            {
                Shape = boxShape,
                Position = new Vector3(box.CenterX, box.CenterY, box.CenterZ)
            };
            state.CollisionBody.AddChild(collisionShape);
            state.CollisionBoxIndex++;

            // Check time budget periodically
            if (state.CollisionBoxIndex % CheckBudgetInterval == 0)
            {
                var elapsed = (long)(Time.GetTicksUsec() - startTime);
                if (elapsed >= budgetUsec)
                    return false; // More work needed next frame
            }
        }

        return true;
    }

    /// <summary>
    /// Commits mesh surfaces incrementally to spread GPU work across frames.
    /// Each surface is built from a MeshBuilder's pre-accumulated arrays in one
    /// ArrayMesh.AddSurfaceFromArrays call — no per-vertex marshalling.
    /// Returns true when all surfaces have been committed and the mesh is added to the scene.
    /// </summary>
    private static bool FinalizeMeshesIncremental(ChunkRenderState state, ChunkRenderContext context, long budgetUsec)
    {
        var mbBodies = state.BuildingBodyMBs!;
        var startTime = Time.GetTicksUsec();

        // Create ArrayMesh on first call
        if (state.BuildingArrayMesh == null)
        {
            state.BuildingArrayMesh = new ArrayMesh();
        }

        var arrayMesh = state.BuildingArrayMesh;

        // Total surfaces: body materials + ledge + windowLit + windowUnlit + details
        int totalSurfaces = mbBodies.Length + 4;

        while (state.MeshSurfaceIndex < totalSurfaces)
        {
            int idx = state.MeshSurfaceIndex;

            if (idx < mbBodies.Length)
            {
                CommitMeshBuilderSurface(arrayMesh, mbBodies[idx], context.BuildingMaterials[idx]);
            }
            else if (idx == mbBodies.Length)
            {
                CommitMeshBuilderSurface(arrayMesh, state.LedgeMB!, context.LedgeMaterial);
            }
            else if (idx == mbBodies.Length + 1)
            {
                CommitMeshBuilderSurface(arrayMesh, state.WindowLitMB!, context.WindowLitMaterial);
            }
            else if (idx == mbBodies.Length + 2)
            {
                CommitMeshBuilderSurface(arrayMesh, state.WindowUnlitMB!, context.WindowUnlitMaterial);
            }
            else if (idx == mbBodies.Length + 3)
            {
                if (state.DetailsMB != null && context.FacadeDetailMaterial != null)
                {
                    CommitMeshBuilderSurface(arrayMesh, state.DetailsMB, context.FacadeDetailMaterial);
                }
            }

            state.MeshSurfaceIndex++;

            // Check budget after each surface commit (each is a GPU operation)
            var elapsed = (long)(Time.GetTicksUsec() - startTime);
            if (elapsed >= budgetUsec && state.MeshSurfaceIndex < totalSurfaces)
                return false; // More work needed next frame
        }

        // All surfaces committed - add mesh to scene or dispose if empty
        if (arrayMesh.GetSurfaceCount() > 0)
        {
            var meshInstance = context.NodePool.GetMeshInstance("Buildings");
            meshInstance.Mesh = arrayMesh;
            state.ChunkNode!.AddChild(meshInstance);
            // Clear reference - MeshInstance3D now owns the mesh
            state.BuildingArrayMesh = null;
        }
        else
        {
            // No surfaces committed - dispose the empty mesh
            arrayMesh.Dispose();
            state.BuildingArrayMesh = null;
        }

        return true;
    }

    /// <summary>
    /// Submits a MeshBuilder's accumulated geometry as one surface of the ArrayMesh.
    /// Skipped if the MeshBuilder is empty (no geometry was added).
    /// </summary>
    private static void CommitMeshBuilderSurface(ArrayMesh arrayMesh, MeshBuilder mb, Material material)
    {
        var arrays = mb.ToSurfaceArrays();
        if (arrays == null) return;

        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        arrayMesh.SurfaceSetMaterial(arrayMesh.GetSurfaceCount() - 1, material);
    }

    /// <summary>
    /// Creates a single OccluderInstance3D with ArrayOccluder3D containing all building boxes.
    /// Using one combined occluder per chunk is much more efficient than individual nodes.
    /// </summary>
    private void CreateOccluders(Node3D parent, List<BoxCollider> collisionBoxes)
    {
        if (_occlusionConfig == null) return;

        var margin = _occlusionConfig.OccluderMargin;
        var minHeight = _occlusionConfig.MinBuildingHeight;
        var minFootprint = _occlusionConfig.MinFootprintArea;
        var includeRooftop = _occlusionConfig.IncludeRooftopFeatures;

        // Pre-count qualifying boxes to size arrays correctly
        int qualifyingCount = 0;
        foreach (var box in collisionBoxes)
        {
            if (box.Height < minHeight) continue;
            if (box.Width * box.Depth < minFootprint) continue;
            if (!includeRooftop && box.Height < minHeight * 0.5f) continue;
            qualifyingCount++;
        }

        if (qualifyingCount == 0) return;

        // Each box has 8 vertices and 12 triangles (36 indices)
        var vertices = new Vector3[qualifyingCount * 8];
        var indices = new int[qualifyingCount * 36];
        int vertexOffset = 0;
        int indexOffset = 0;

        foreach (var box in collisionBoxes)
        {
            if (box.Height < minHeight) continue;
            if (box.Width * box.Depth < minFootprint) continue;
            if (!includeRooftop && box.Height < minHeight * 0.5f) continue;

            // Shrink the occluder by margin to avoid artifacts at building edges
            var hw = Math.Max(0.05f, (box.Width - margin * 2) / 2f);
            var hh = Math.Max(0.05f, (box.Height - margin) / 2f);
            var hd = Math.Max(0.05f, (box.Depth - margin * 2) / 2f);
            var cx = box.CenterX;
            var cy = box.CenterY;
            var cz = box.CenterZ;

            // 8 vertices of the box
            int v = vertexOffset;
            vertices[v + 0] = new Vector3(cx - hw, cy - hh, cz - hd);
            vertices[v + 1] = new Vector3(cx + hw, cy - hh, cz - hd);
            vertices[v + 2] = new Vector3(cx + hw, cy + hh, cz - hd);
            vertices[v + 3] = new Vector3(cx - hw, cy + hh, cz - hd);
            vertices[v + 4] = new Vector3(cx - hw, cy - hh, cz + hd);
            vertices[v + 5] = new Vector3(cx + hw, cy - hh, cz + hd);
            vertices[v + 6] = new Vector3(cx + hw, cy + hh, cz + hd);
            vertices[v + 7] = new Vector3(cx - hw, cy + hh, cz + hd);

            // 12 triangles (36 indices) - front, back, left, right, top, bottom
            int i = indexOffset;
            // Front face (-Z)
            indices[i++] = v + 0; indices[i++] = v + 2; indices[i++] = v + 1;
            indices[i++] = v + 0; indices[i++] = v + 3; indices[i++] = v + 2;
            // Back face (+Z)
            indices[i++] = v + 4; indices[i++] = v + 5; indices[i++] = v + 6;
            indices[i++] = v + 4; indices[i++] = v + 6; indices[i++] = v + 7;
            // Left face (-X)
            indices[i++] = v + 0; indices[i++] = v + 4; indices[i++] = v + 7;
            indices[i++] = v + 0; indices[i++] = v + 7; indices[i++] = v + 3;
            // Right face (+X)
            indices[i++] = v + 1; indices[i++] = v + 2; indices[i++] = v + 6;
            indices[i++] = v + 1; indices[i++] = v + 6; indices[i++] = v + 5;
            // Top face (+Y)
            indices[i++] = v + 3; indices[i++] = v + 7; indices[i++] = v + 6;
            indices[i++] = v + 3; indices[i++] = v + 6; indices[i++] = v + 2;
            // Bottom face (-Y)
            indices[i++] = v + 0; indices[i++] = v + 1; indices[i++] = v + 5;
            indices[i++] = v + 0; indices[i++] = v + 5; indices[i++] = v + 4;

            vertexOffset += 8;
            indexOffset = i;
        }

        // Create single combined occluder for the entire chunk
        var arrayOccluder = new ArrayOccluder3D();
        arrayOccluder.SetArrays(vertices, indices);

        var occluderInstance = new OccluderInstance3D
        {
            Name = "BuildingOccluder",
            Occluder = arrayOccluder
        };

        parent.AddChild(occluderInstance);
    }

    private void CollectBuildingCollisionBoxes(List<BoxCollider> boxes, BuildingData building, float localX, float localZ)
    {
        if (building.IsStepped && building.Sections != null)
        {
            // Add collision box for each section of stepped building
            foreach (var section in building.Sections)
            {
                boxes.Add(new BoxCollider(
                    localX + section.OffsetX,
                    section.StartY + (section.EndY - section.StartY) / 2f,
                    localZ + section.OffsetZ,
                    section.Width,
                    section.EndY - section.StartY,
                    section.Depth
                ));
            }

            // Add rooftop feature collision for stepped building
            var topSection = building.Sections[^1];
            var steppedRooftop = GetRooftopFeatureDimensions(building.Seed, topSection.Width, topSection.Depth, _facadeConfig.RooftopFeatureChance);
            if (steppedRooftop.HasFeature)
            {
                boxes.Add(new BoxCollider(
                    localX + topSection.OffsetX,
                    topSection.EndY + steppedRooftop.Height / 2f,
                    localZ + topSection.OffsetZ,
                    steppedRooftop.Width, steppedRooftop.Height, steppedRooftop.Depth
                ));
            }
        }
        else
        {
            // Simple building - single box collision
            boxes.Add(new BoxCollider(localX, building.Height / 2f, localZ,
                building.Width, building.Height, building.Depth));

            // Add rooftop feature collision for simple building
            var simpleRooftop = GetRooftopFeatureDimensions(building.Seed, building.Width, building.Depth, _facadeConfig.RooftopFeatureChance);
            if (simpleRooftop.HasFeature)
            {
                boxes.Add(new BoxCollider(localX, building.Height + simpleRooftop.Height / 2f, localZ,
                    simpleRooftop.Width, simpleRooftop.Height, simpleRooftop.Depth));
            }
        }
    }

    private void AddBuildingGeometry(MeshBuilder mbBody, MeshBuilder mbLedge, MeshBuilder mbWindowLit, MeshBuilder mbWindowUnlit, BuildingData building, float localX, float localZ, List<NeonLightData> buildingNeons)
    {
        var inset = _facadeConfig.FacadeInset;
        // Convert building seed to float 0-1 for shader weathering variation
        var buildingSeedFloat = (building.Seed & RenderingConstants.SeedMask) / RenderingConstants.SeedNormalizationFactor;

        if (building.IsStepped && building.Sections != null)
        {
            for (int i = 0; i < building.Sections.Count; i++)
            {
                var section = building.Sections[i];
                var sectionX = localX + section.OffsetX;
                var sectionZ = localZ + section.OffsetZ;
                var sectionHeight = section.EndY - section.StartY;
                var sectionCenterY = section.StartY + sectionHeight / 2f;

                // World coordinates for this section
                var worldSectionX = building.X + section.OffsetX;
                var worldSectionZ = building.Z + section.OffsetZ;

                GeometryBuilder.AddBoxWithSeed(mbBody, sectionX, sectionCenterY, sectionZ,
                    section.Width - inset * 2, sectionHeight, section.Depth - inset * 2, buildingSeedFloat);

                // Use pre-computed grid for floor/ledge layout
                var sectionGrid = section.Grid;
                for (int floor = 0; floor <= sectionGrid.NumFloors; floor++)
                {
                    var ledgeY = section.StartY + sectionGrid.GroundFloorHeight + floor * sectionGrid.FloorHeight;
                    if (ledgeY < section.EndY - RenderingConstants.SectionEndMargin)
                    {
                        AddLedgeGeometry(mbLedge, sectionX, sectionZ, section.Width, section.Depth, ledgeY);
                    }
                }

                AddWindowGeometry(mbWindowLit, mbWindowUnlit, building.Seed, sectionX, sectionZ,
                    section.Width, section.Depth, section.StartY, section.EndY,
                    sectionGrid, worldSectionX, worldSectionZ, buildingNeons, building.WindowLitPercentage);

                if (i < building.Sections.Count - 1)
                {
                    var nextSection = building.Sections[i + 1];
                    AddTerraceLedge(mbLedge, sectionX, sectionZ, section, nextSection);
                }
            }

            // Rooftop feature geometry for stepped building (uses same material as building body)
            var topSection = building.Sections[^1];
            var steppedRooftopGeom = GetRooftopFeatureDimensions(building.Seed, topSection.Width, topSection.Depth, _facadeConfig.RooftopFeatureChance);
            if (steppedRooftopGeom.HasFeature)
            {
                GeometryBuilder.AddBoxWithSeed(mbBody, localX + topSection.OffsetX, topSection.EndY + steppedRooftopGeom.Height / 2f,
                    localZ + topSection.OffsetZ, steppedRooftopGeom.Width, steppedRooftopGeom.Height, steppedRooftopGeom.Depth, buildingSeedFloat);
            }
        }
        else
        {
            GeometryBuilder.AddBoxWithSeed(mbBody, localX, building.Height / 2f, localZ, building.Width - inset * 2, building.Height, building.Depth - inset * 2, buildingSeedFloat);

            // Use pre-computed grid for floor/ledge layout
            var grid = building.Grid;
            for (int floor = 0; floor <= grid.NumFloors; floor++)
            {
                var ledgeY = grid.GroundFloorHeight + floor * grid.FloorHeight;
                if (ledgeY < building.Height - RenderingConstants.SectionEndMargin)
                {
                    AddLedgeGeometry(mbLedge, localX, localZ, building.Width, building.Depth, ledgeY);
                }
            }

            AddWindowGeometry(mbWindowLit, mbWindowUnlit, building.Seed, localX, localZ,
                building.Width, building.Depth, 0, building.Height,
                grid, building.X, building.Z, buildingNeons, building.WindowLitPercentage);

            // Rooftop feature geometry for simple building (uses same material as building body)
            var simpleRooftopGeom = GetRooftopFeatureDimensions(building.Seed, building.Width, building.Depth, _facadeConfig.RooftopFeatureChance);
            if (simpleRooftopGeom.HasFeature)
            {
                GeometryBuilder.AddBoxWithSeed(mbBody, localX, building.Height + simpleRooftopGeom.Height / 2f, localZ,
                    simpleRooftopGeom.Width, simpleRooftopGeom.Height, simpleRooftopGeom.Depth, buildingSeedFloat);
            }
        }
    }

    private void AddWindowGeometry(MeshBuilder mbLit, MeshBuilder mbUnlit, int buildingSeed, float localX, float localZ,
        float width, float depth, float baseY, float maxY, in FacadeGrid grid,
        float worldX, float worldZ, List<NeonLightData> blockingSigns, float? customLitPercentage = null)
    {
        var windowWidth = _facadeConfig.WindowWidth;
        var windowHeight = _facadeConfig.WindowHeight;

        // Use pre-computed grid values
        var windowsX = grid.ColumnsX;
        var windowsZ = grid.ColumnsZ;
        var startOffsetX = grid.StartOffsetX;
        var startOffsetZ = grid.StartOffsetZ;
        var windowSpacing = grid.WindowSpacing;
        var floorHeight = grid.FloorHeight;
        var groundFloorHeight = grid.GroundFloorHeight;

        var numFloors = grid.NumFloors;
        var startFloorNum = (int)((baseY + groundFloorHeight) / floorHeight);

        var uvX = (buildingSeed & RenderingConstants.SeedMask) / RenderingConstants.SeedNormalizationFactor;
        var totalFloors = startFloorNum + numFloors;

        // Fast path: no blocking signs means no coverage checks needed
        bool hasBlockingSigns = blockingSigns.Count > 0;

        for (int floor = 0; floor < numFloors; floor++)
        {
            var absoluteFloor = startFloorNum + floor;
            var windowY = baseY + groundFloorHeight + floor * floorHeight + floorHeight * RenderingConstants.FloorHeightOffsetFactor;
            if (windowY + windowHeight / 2 > maxY - RenderingConstants.SectionEndMargin) continue;

            var uvY = absoluteFloor / (float)Math.Max(totalFloors, 1);
            var floorPattern = GetFloorPattern(buildingSeed, absoluteFloor, _windowConfig.Variation);
            int windowIndex = 0;

            // Front and back facades
            for (int i = 0; i < windowsX; i++)
            {
                var wx = localX - width / 2f + startOffsetX + i * windowSpacing;
                var worldWx = worldX - width / 2f + startOffsetX + i * windowSpacing;

                // Front window (+Z)
                var frontZ = localZ + depth / 2f;
                var worldFrontZ = worldZ + depth / 2f;
                var frontCovered = hasBlockingSigns && IsWindowCoveredBySign(worldWx, windowY, worldFrontZ, windowWidth, windowHeight, FaceDirection.Front, blockingSigns);

                if (!frontCovered && IsWindowLit(buildingSeed, absoluteFloor, windowIndex, _windowConfig, floorPattern, customLitPercentage))
                {
                    var uv2X = StableHash.ToFloat01(buildingSeed, absoluteFloor, windowIndex, 0);
                    var uv2Y = (windowIndex & RenderingConstants.WindowIndexMask) / RenderingConstants.WindowIndexNormalizationFactor;
                    GeometryBuilder.AddWindowQuadWithUV(mbLit, wx, windowY, frontZ, windowWidth, windowHeight, uvX, uvY, uv2X, uv2Y, FaceDirection.Front);
                }
                else
                {
                    GeometryBuilder.AddWindowQuad(mbUnlit, wx, windowY, frontZ, windowWidth, windowHeight, FaceDirection.Front);
                }
                windowIndex++;

                // Back window (-Z)
                var backZ = localZ - depth / 2f;
                var worldBackZ = worldZ - depth / 2f;
                var backCovered = hasBlockingSigns && IsWindowCoveredBySign(worldWx, windowY, worldBackZ, windowWidth, windowHeight, FaceDirection.Back, blockingSigns);

                if (!backCovered && IsWindowLit(buildingSeed, absoluteFloor, windowIndex, _windowConfig, floorPattern, customLitPercentage))
                {
                    var uv2X = StableHash.ToFloat01(buildingSeed, absoluteFloor, windowIndex, 1);
                    var uv2Y = (windowIndex & RenderingConstants.WindowIndexMask) / RenderingConstants.WindowIndexNormalizationFactor;
                    GeometryBuilder.AddWindowQuadWithUV(mbLit, wx, windowY, backZ, windowWidth, windowHeight, uvX, uvY, uv2X, uv2Y, FaceDirection.Back);
                }
                else
                {
                    GeometryBuilder.AddWindowQuad(mbUnlit, wx, windowY, backZ, windowWidth, windowHeight, FaceDirection.Back);
                }
                windowIndex++;
            }

            // Left and right facades
            for (int i = 0; i < windowsZ; i++)
            {
                var wz = localZ - depth / 2f + startOffsetZ + i * windowSpacing;
                var worldWz = worldZ - depth / 2f + startOffsetZ + i * windowSpacing;

                // Right window (+X)
                var rightX = localX + width / 2f;
                var worldRightX = worldX + width / 2f;
                var rightCovered = hasBlockingSigns && IsWindowCoveredBySign(worldRightX, windowY, worldWz, windowWidth, windowHeight, FaceDirection.Right, blockingSigns);

                if (!rightCovered && IsWindowLit(buildingSeed, absoluteFloor, windowIndex, _windowConfig, floorPattern, customLitPercentage))
                {
                    var uv2X = StableHash.ToFloat01(buildingSeed, absoluteFloor, windowIndex, 2);
                    var uv2Y = (windowIndex & RenderingConstants.WindowIndexMask) / RenderingConstants.WindowIndexNormalizationFactor;
                    GeometryBuilder.AddWindowQuadWithUV(mbLit, rightX, windowY, wz, windowWidth, windowHeight, uvX, uvY, uv2X, uv2Y, FaceDirection.Right);
                }
                else
                {
                    GeometryBuilder.AddWindowQuad(mbUnlit, rightX, windowY, wz, windowWidth, windowHeight, FaceDirection.Right);
                }
                windowIndex++;

                // Left window (-X)
                var leftX = localX - width / 2f;
                var worldLeftX = worldX - width / 2f;
                var leftCovered = hasBlockingSigns && IsWindowCoveredBySign(worldLeftX, windowY, worldWz, windowWidth, windowHeight, FaceDirection.Left, blockingSigns);

                if (!leftCovered && IsWindowLit(buildingSeed, absoluteFloor, windowIndex, _windowConfig, floorPattern, customLitPercentage))
                {
                    var uv2X = StableHash.ToFloat01(buildingSeed, absoluteFloor, windowIndex, 3);
                    var uv2Y = (windowIndex & RenderingConstants.WindowIndexMask) / RenderingConstants.WindowIndexNormalizationFactor;
                    GeometryBuilder.AddWindowQuadWithUV(mbLit, leftX, windowY, wz, windowWidth, windowHeight, uvX, uvY, uv2X, uv2Y, FaceDirection.Left);
                }
                else
                {
                    GeometryBuilder.AddWindowQuad(mbUnlit, leftX, windowY, wz, windowWidth, windowHeight, FaceDirection.Left);
                }
                windowIndex++;
            }
        }
    }

    private void AddLedgeGeometry(MeshBuilder mb, float cx, float cz, float buildingWidth, float buildingDepth, float y)
    {
        var ledgeHeight = _facadeConfig.LedgeHeight;
        var overhang = _facadeConfig.LedgeDepth;

        GeometryBuilder.AddBox(mb, cx, y, cz + buildingDepth / 2f + overhang / 2f, buildingWidth + overhang * 2, ledgeHeight, overhang);
        GeometryBuilder.AddBox(mb, cx, y, cz - buildingDepth / 2f - overhang / 2f, buildingWidth + overhang * 2, ledgeHeight, overhang);
        GeometryBuilder.AddBox(mb, cx - buildingWidth / 2f - overhang / 2f, y, cz, overhang, ledgeHeight, buildingDepth);
        GeometryBuilder.AddBox(mb, cx + buildingWidth / 2f + overhang / 2f, y, cz, overhang, ledgeHeight, buildingDepth);
    }

    private void AddTerraceLedge(MeshBuilder mb, float cx, float cz, BuildingSection lowerSection, BuildingSection upperSection)
    {
        var terraceY = lowerSection.EndY;
        var ledgeHeight = _facadeConfig.LedgeHeight * 2;

        var lowerHalfW = lowerSection.Width / 2f;
        var lowerHalfD = lowerSection.Depth / 2f;
        var upperHalfW = upperSection.Width / 2f;
        var upperHalfD = upperSection.Depth / 2f;

        if (lowerHalfD > upperHalfD || upperSection.OffsetZ < 0)
        {
            var parapetZ = cz + lowerHalfD;
            GeometryBuilder.AddBox(mb, cx, terraceY + ledgeHeight / 2f, parapetZ, lowerSection.Width, ledgeHeight, RenderingConstants.ParapetDepth);
        }
        if (lowerHalfD > upperHalfD || upperSection.OffsetZ > 0)
        {
            var parapetZ = cz - lowerHalfD;
            GeometryBuilder.AddBox(mb, cx, terraceY + ledgeHeight / 2f, parapetZ, lowerSection.Width, ledgeHeight, RenderingConstants.ParapetDepth);
        }
        if (lowerHalfW > upperHalfW || upperSection.OffsetX > 0)
        {
            var parapetX = cx - lowerHalfW;
            GeometryBuilder.AddBox(mb, parapetX, terraceY + ledgeHeight / 2f, cz, RenderingConstants.ParapetDepth, ledgeHeight, lowerSection.Depth);
        }
        if (lowerHalfW > upperHalfW || upperSection.OffsetX < 0)
        {
            var parapetX = cx + lowerHalfW;
            GeometryBuilder.AddBox(mb, parapetX, terraceY + ledgeHeight / 2f, cz, RenderingConstants.ParapetDepth, ledgeHeight, lowerSection.Depth);
        }
    }

    /// <summary>
    /// Checks if a window would be lit. Used by BuildingRenderer and other renderers (e.g., silhouettes).
    /// </summary>
    /// <param name="buildingSeed">Building seed from BuildingData.Seed</param>
    /// <param name="absoluteFloor">Absolute floor number (including ground floor offset)</param>
    /// <param name="windowIndex">Window index on this floor</param>
    /// <param name="windowConfig">Window configuration</param>
    /// <param name="precomputedPattern">Optional precomputed floor pattern</param>
    /// <param name="customLitPercentage">Optional custom lit percentage (from cluster data). If null, uses windowConfig.LitPercentage</param>
    public static bool IsWindowLit(int buildingSeed, int absoluteFloor, int windowIndex, WindowConfig windowConfig, FloorPattern? precomputedPattern = null, float? customLitPercentage = null)
    {
        var pattern = precomputedPattern ?? GetFloorPattern(buildingSeed, absoluteFloor, windowConfig.Variation);
        var litPercentage = customLitPercentage ?? windowConfig.LitPercentage;

        return pattern switch
        {
            FloorPattern.MostlyDark => StableHash.ToFloat01(buildingSeed, absoluteFloor, windowIndex, PurposeWindowLit) < RenderingConstants.MostlyDarkLitChance,
            FloorPattern.FullyLit => true,
            FloorPattern.Checkerboard => (absoluteFloor + windowIndex) % 2 == 0,
            _ => StableHash.ToFloat01(buildingSeed, absoluteFloor, windowIndex, PurposeWindowLit) < litPercentage
        };
    }

    /// <summary>
    /// Gets the floor pattern for a given building floor.
    /// </summary>
    public static FloorPattern GetFloorPattern(int buildingSeed, int absoluteFloor, WindowVariationConfig variation)
    {
        var roll = StableHash.ToFloat01(buildingSeed, absoluteFloor, PurposeFloorPattern);

        var darkThreshold = variation.DarkFloorChance;
        var litThreshold = darkThreshold + variation.FullyLitFloorChance;
        var checkerThreshold = litThreshold + variation.CheckerboardFloorChance;

        if (roll < darkThreshold)
            return FloorPattern.MostlyDark;
        if (roll < litThreshold)
            return FloorPattern.FullyLit;
        if (roll < checkerThreshold)
            return FloorPattern.Checkerboard;

        return FloorPattern.Normal;
    }

    /// <summary>
    /// Calculates rooftop feature dimensions deterministically from building seed.
    /// Returns dimensions only if the building has a rooftop feature based on rooftopFeatureChance.
    /// </summary>
    public static RooftopFeatureDimensions GetRooftopFeatureDimensions(int buildingSeed, float baseWidth, float baseDepth, float rooftopFeatureChance)
    {
        if (StableHash.ToFloat01(buildingSeed, PurposeRooftopChance) >= rooftopFeatureChance)
            return RooftopFeatureDimensions.None;

        var featureWidth = baseWidth * (RenderingConstants.RooftopFeatureMinScale +
            StableHash.ToFloat01(buildingSeed, PurposeRooftopWidth) * RenderingConstants.RooftopFeatureScaleRange);
        var featureDepth = baseDepth * (RenderingConstants.RooftopFeatureMinScale +
            StableHash.ToFloat01(buildingSeed, PurposeRooftopDepth) * RenderingConstants.RooftopFeatureScaleRange);
        var featureHeight = RenderingConstants.RooftopFeatureMinHeight +
            StableHash.ToFloat01(buildingSeed, PurposeRooftopHeight) * RenderingConstants.RooftopFeatureHeightRange;

        return new RooftopFeatureDimensions(featureWidth, featureHeight, featureDepth);
    }

    /// <summary>
    /// Calculates the center position on top of a building's rooftop, accounting for
    /// stepped sections and rooftop features.
    /// </summary>
    /// <param name="building">The building data.</param>
    /// <param name="rooftopFeatureChance">Chance of rooftop features (from config).</param>
    /// <returns>The rooftop center position.</returns>
    public static RooftopCenterPosition GetRooftopCenterPosition(BuildingData building, float rooftopFeatureChance)
    {
        float baseY = building.Height;
        float centerX = building.X;
        float centerZ = building.Z;
        float baseWidth = building.Width;
        float baseDepth = building.Depth;

        // For stepped buildings, use the top section
        if (building.IsStepped && building.Sections is { Count: > 0 })
        {
            var topSection = building.Sections[^1];
            baseY = topSection.EndY;
            centerX = building.X + topSection.OffsetX;
            centerZ = building.Z + topSection.OffsetZ;
            baseWidth = topSection.Width;
            baseDepth = topSection.Depth;
        }

        // Check for rooftop feature height
        var rooftopFeature = GetRooftopFeatureDimensions(building.Seed, baseWidth, baseDepth, rooftopFeatureChance);
        float featureHeight = rooftopFeature.HasFeature ? rooftopFeature.Height : 0f;

        return new RooftopCenterPosition(centerX, baseY, featureHeight, centerZ, baseWidth, baseDepth);
    }

    /// <summary>
    /// Checks if a window is covered by any large neon sign.
    /// Assumes buildingNeons is pre-filtered to only contain blocking sign types.
    /// </summary>
    private static bool IsWindowCoveredBySign(
        float windowX, float windowY, float windowZ,
        float windowWidth, float windowHeight,
        FaceDirection face,
        List<NeonLightData> buildingNeons)
    {
        // Pre-compute window bounds once
        float windowMinY = windowY - windowHeight / 2f;
        float windowMaxY = windowY + windowHeight / 2f;
        float windowHalfWidth = windowWidth / 2f;

        for (int i = 0; i < buildingNeons.Count; i++)
        {
            var neon = buildingNeons[i];

            // Quick Y bounds check first (most likely to fail, cheapest check)
            float signMinY = neon.Y - neon.Height / 2f;
            float signMaxY = neon.Y + neon.Height / 2f;
            if (windowMaxY < signMinY || windowMinY > signMaxY) continue;

            // Check if the sign is on the same facade based on rotation
            bool sameFacade = face switch
            {
                FaceDirection.Front => neon.RotationY > -0.1f && neon.RotationY < 0.1f,
                FaceDirection.Back => neon.RotationY > 3.04f || neon.RotationY < -3.04f,
                FaceDirection.Left => neon.RotationY < -1.47f && neon.RotationY > -1.67f,
                FaceDirection.Right => neon.RotationY > 1.47f && neon.RotationY < 1.67f,
                _ => false
            };
            if (!sameFacade) continue;

            // Check horizontal overlap based on facade orientation
            float signHalfWidth = neon.Width / 2f;
            float combinedHalfWidth = signHalfWidth + windowHalfWidth;

            bool horizontalOverlap = (face == FaceDirection.Front || face == FaceDirection.Back)
                ? Math.Abs(windowX - neon.X) < combinedHalfWidth
                : Math.Abs(windowZ - neon.Z) < combinedHalfWidth;

            if (horizontalOverlap) return true;
        }

        return false;
    }

    /// <summary>
    /// Filters neons to only window-blocking sign types belonging to a specific building.
    /// Uses a reusable buffer to avoid per-building allocations.
    /// </summary>
    private static void GetBlockingSignsForBuilding(List<NeonLightData> allNeons, BuildingData building, List<NeonLightData> result)
    {
        result.Clear();
        var halfW = building.Width / 2f + 1f;
        var halfD = building.Depth / 2f + 1f;
        var maxY = building.Height + 2f;

        for (int i = 0; i < allNeons.Count; i++)
        {
            var neon = allNeons[i];

            // Only include sign types that block windows
            if (neon.Shape != NeonShape.Billboard &&
                neon.Shape != NeonShape.KanjiSign &&
                neon.Shape != NeonShape.LargeSign)
                continue;

            // Check if neon is within building footprint
            if (Math.Abs(neon.X - building.X) <= halfW &&
                Math.Abs(neon.Z - building.Z) <= halfD &&
                neon.Y <= maxY)
            {
                result.Add(neon);
            }
        }
    }
}
