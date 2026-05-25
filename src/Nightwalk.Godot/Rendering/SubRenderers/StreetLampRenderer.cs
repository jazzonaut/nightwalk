using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.World.Roads;
using Nightwalk.Godot.Rendering;
using Nightwalk.Godot.Rendering.Constants;
using Nightwalk.Godot.Rendering.Materials;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Renders street lamps at intersection corners.
/// Uses MultiMeshInstance3D for efficient batching of lamp geometry.
/// </summary>
public sealed class StreetLampRenderer : ISubRenderer, IPhasedSubRenderer
{
    public ChunkRenderPhase Phase => ChunkRenderPhase.StreetLamps;
    private readonly StreetLightsConfig _config;
    private readonly RoadConfig _roadConfig;
    private readonly Color _lightColor;

    // Cached meshes (shared across all chunks)
    private CylinderMesh? _lampPoleMesh;
    private TorusMesh? _lampTopRingMesh;
    private TorusMesh? _lampAccentRingMesh;

    public StreetLampRenderer(StreetLightsConfig config, RoadConfig roadConfig)
    {
        _config = config;
        _roadConfig = roadConfig;
        _lightColor = Color.FromHtml(config.LightColor);
    }

    public void Render(Node3D parent, ChunkData data, ChunkRenderContext context)
    {
        if (!_config.Enabled || data.Roads.Count == 0) return;

        // Reuse incremental implementation with unlimited budget
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
                GD.PushError($"StreetLampRenderer.Render exceeded {maxIterations} iterations - likely infinite loop bug");
                break;
            }
        }
    }

    private List<Vector3> CollectLampPositions(ChunkData data, float chunkWorldX, float chunkWorldZ)
    {
        var lampPositions = new List<Vector3>();
        var maxLamps = _config.MaxPerChunk;

        // Separate roads by direction
        var ewRoads = new List<RoadSegment>(data.Roads.Count);
        var nsRoads = new List<RoadSegment>(data.Roads.Count);
        foreach (var road in data.Roads)
        {
            if (road.Direction == RoadDirection.EastWest)
                ewRoads.Add(road);
            else if (road.Direction == RoadDirection.NorthSouth)
                nsRoads.Add(road);
        }

        // Find lamp positions at intersection corners
        foreach (var ewRoad in ewRoads)
        {
            if (lampPositions.Count >= maxLamps) break;

            var localZ = ewRoad.Position - chunkWorldZ;

            foreach (var nsRoad in nsRoads)
            {
                if (lampPositions.Count >= maxLamps) break;

                var localX = nsRoad.Position - chunkWorldX;

                float xOffset = nsRoad.HalfWidth + _roadConfig.CurbWidth + _roadConfig.SidewalkWidth * RenderingConstants.LampSidewalkPlacementFactor;
                float zOffset = ewRoad.HalfWidth + _roadConfig.CurbWidth + _roadConfig.SidewalkWidth * RenderingConstants.LampSidewalkPlacementFactor;

                var cornerSeed = (int)(localX * RenderingConstants.CornerSeedXMultiplier + localZ * RenderingConstants.CornerSeedZMultiplier);
                bool useFirstDiagonal = (cornerSeed % 2) == 0;

                var corners = useFirstDiagonal
                    ? new[] { (localX - xOffset, localZ - zOffset), (localX + xOffset, localZ + zOffset) }
                    : new[] { (localX + xOffset, localZ - zOffset), (localX - xOffset, localZ + zOffset) };

                foreach (var (lampX, lampZ) in corners)
                {
                    if (lampPositions.Count >= maxLamps) break;
                    if (lampX < 0 || lampX > data.Size || lampZ < 0 || lampZ > data.Size)
                        continue;

                    lampPositions.Add(new Vector3(lampX, 0, lampZ));
                }
            }
        }

        return lampPositions;
    }

    private OmniLight3D CreateLight(Vector3 pos, float height)
    {
        var light = new OmniLight3D
        {
            Position = new Vector3(pos.X, height, pos.Z),
            LightColor = _lightColor,
            LightEnergy = _config.LightEnergy,
            OmniRange = _config.LightRange,
            OmniAttenuation = _config.OmniAttenuation,
            ShadowEnabled = _config.ShadowEnabled,
            DistanceFadeEnabled = true,
            DistanceFadeBegin = _config.LodLightDistance,
            DistanceFadeLength = _config.LodLightFadeLength
        };
        return light;
    }

    private static MultiMesh CreateMultiMesh(Mesh mesh, int count)
    {
        return new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = count
        };
    }

    private void EnsureLampMeshes(float height)
    {
        _lampPoleMesh ??= new CylinderMesh
        {
            TopRadius = RenderingConstants.LampPoleTopRadius,
            BottomRadius = RenderingConstants.LampPoleBottomRadius,
            Height = height - RenderingConstants.LampHeightOffset
        };

        _lampTopRingMesh ??= new TorusMesh
        {
            InnerRadius = RenderingConstants.LampTopRingInnerRadius,
            OuterRadius = RenderingConstants.LampTopRingOuterRadius,
            Rings = RenderingConstants.LampTopRingRings,
            RingSegments = RenderingConstants.LampTopRingSegments
        };

        _lampAccentRingMesh ??= new TorusMesh
        {
            InnerRadius = RenderingConstants.LampAccentRingInnerRadius,
            OuterRadius = RenderingConstants.LampAccentRingOuterRadius,
            Rings = RenderingConstants.LampAccentRingRings,
            RingSegments = RenderingConstants.LampAccentRingSegments
        };
    }

    public bool RenderIncremental(ChunkRenderState state, long budgetUsec)
    {
        if (state.ChunkNode == null || state.Context == null) return true;
        if (!_config.Enabled || state.Data.Roads.Count == 0) return true;

        var context = state.Context;
        var startTime = Time.GetTicksUsec();

        // Initialize on first call: collect positions, create MultiMeshes
        if (!state.StreetLampsInitialized)
        {
            state.StreetLampPositions = CollectLampPositions(state.Data, context.ChunkWorldX, context.ChunkWorldZ);
            if (state.StreetLampPositions.Count == 0)
            {
                state.StreetLampsInitialized = true;
                return true;
            }

            EnsureLampMeshes(_config.LightHeight);

            state.StreetLampsNode = new Node3D { Name = "StreetLamps" };
            int count = state.StreetLampPositions.Count;

            // Create MultiMeshes and set all transforms (cheap operation)
            var poleMultiMesh = CreateMultiMesh(_lampPoleMesh!, count);
            var topRingMultiMesh = CreateMultiMesh(_lampTopRingMesh!, count);
            var accentRingMultiMesh = CreateMultiMesh(_lampAccentRingMesh!, count * 3);

            float height = _config.LightHeight;
            float poleY = (height - RenderingConstants.LampHeightOffset) / 2f;
            float topRingY = height - RenderingConstants.LampHeightOffset + RenderingConstants.LampTopRingYOffset;

            for (int i = 0; i < count; i++)
            {
                var pos = state.StreetLampPositions[i];
                poleMultiMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(pos.X, poleY, pos.Z)));
                topRingMultiMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(pos.X, topRingY, pos.Z)));

                for (int j = 0; j < RenderingConstants.LampAccentHeightFactors.Length; j++)
                {
                    float accentY = height * RenderingConstants.LampAccentHeightFactors[j];
                    accentRingMultiMesh.SetInstanceTransform(i * 3 + j,
                        new Transform3D(Basis.Identity, new Vector3(pos.X, accentY, pos.Z)));
                }
            }

            // Add MultiMeshInstance3D nodes (geometry is ready immediately)
            state.StreetLampsNode.AddChild(new MultiMeshInstance3D { Multimesh = poleMultiMesh, MaterialOverride = context.LampPoleMaterial });
            state.StreetLampsNode.AddChild(new MultiMeshInstance3D { Multimesh = topRingMultiMesh, MaterialOverride = context.LampRingMaterial });
            state.StreetLampsNode.AddChild(new MultiMeshInstance3D { Multimesh = accentRingMultiMesh, MaterialOverride = context.LampRingMaterial });

            state.ChunkNode.AddChild(state.StreetLampsNode);
            state.StreetLampsInitialized = true;
        }

        // Create lights incrementally (this is the expensive part)
        float lightHeight = _config.LightHeight;
        while (state.StreetLampIndex < state.StreetLampPositions!.Count)
        {
            var pos = state.StreetLampPositions[state.StreetLampIndex];
            var light = CreateLight(pos, lightHeight);
            state.StreetLampsNode!.AddChild(light);
            state.StreetLampIndex++;

            // Check budget every few lights
            if (state.StreetLampIndex % 2 == 0)
            {
                var elapsed = (long)(Time.GetTicksUsec() - startTime);
                if (elapsed >= budgetUsec)
                    return false; // More work needed
            }
        }

        return true; // Phase complete
    }
}
