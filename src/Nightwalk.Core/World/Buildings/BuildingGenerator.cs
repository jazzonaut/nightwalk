using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Core.World.Clusters;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.World.Buildings;

/// <summary>
/// Generates buildings within city blocks defined by roads.
/// Simplified approach: each city block = one plot with 1-2 buildings or empty (park).
/// </summary>
public sealed class BuildingGenerator
{
    private readonly BuildingConfig _config;
    private readonly FacadeConfig _facadeConfig;
    private readonly float _roadMajorSpacing;
    private readonly float _majorHalfWidth;
    private readonly float _minorHalfWidth;
    private readonly float _sidewalkWidth;
    private readonly ClusterInfluenceProvider? _clusterProvider;
    private readonly float _defaultWindowLitPercentage;
    private readonly float _defaultNeonDensityMultiplier;

    // Grid computation parameters
    private readonly float _windowSpacing;
    private readonly float _floorHeight;
    private readonly float _groundFloorHeight;
    private const float WindowEdgeMargin = 2f;

    public BuildingGenerator(
        BuildingConfig buildingConfig,
        RoadConfig roadConfig,
        FacadeConfig facadeConfig,
        float windowLitPercentage,
        float neonDensityMultiplier,
        ClusterInfluenceProvider? clusterProvider = null)
    {
        _config = buildingConfig;
        _facadeConfig = facadeConfig;
        _roadMajorSpacing = roadConfig.MajorSpacing;
        _majorHalfWidth = roadConfig.MajorWidth / 2f;
        _minorHalfWidth = roadConfig.MinorWidth / 2f;
        _sidewalkWidth = roadConfig.SidewalkWidth;
        _clusterProvider = clusterProvider;
        _defaultWindowLitPercentage = windowLitPercentage;
        _defaultNeonDensityMultiplier = neonDensityMultiplier;

        _windowSpacing = facadeConfig.WindowSpacing;
        _floorHeight = facadeConfig.FloorHeight;
        _groundFloorHeight = facadeConfig.GroundFloorHeight;
    }

    /// <summary>
    /// Generates buildings and plots for a chunk based on road layout.
    /// </summary>
    public (List<BuildingData> Buildings, List<PlotData> Plots, List<int> EmptyPlotIndices) GenerateBuildings(
        float chunkMinX, float chunkMinZ, float chunkSize, int chunkSeed, List<RoadSegment> roads)
    {
        var buildings = new List<BuildingData>();
        var plots = new List<PlotData>();
        var emptyPlotIndices = new List<int>();
        var rng = new DeterministicRng(chunkSeed);

        // Find city blocks within this chunk
        var blocks = FindCityBlocks(chunkMinX, chunkMinZ, chunkSize, roads);

        foreach (var block in blocks)
        {
            // Each block = one plot, possibly with 1-2 buildings
            GeneratePlotAndBuildings(buildings, plots, emptyPlotIndices, block, rng);
        }

        return (buildings, plots, emptyPlotIndices);
    }

    private List<CityBlock> FindCityBlocks(float chunkMinX, float chunkMinZ, float chunkSize, List<RoadSegment> roads)
    {
        var blocks = new List<CityBlock>();
        var chunkMaxX = chunkMinX + chunkSize;
        var chunkMaxZ = chunkMinZ + chunkSize;

        // Get road positions that affect this chunk
        var ewRoads = new List<float>();
        var nsRoads = new List<float>();

        foreach (var road in roads)
        {
            if (road.Direction == RoadDirection.EastWest)
                ewRoads.Add(road.Position);
            else
                nsRoads.Add(road.Position);
        }

        ewRoads.Sort();
        nsRoads.Sort();

        // Track if first/last positions are chunk boundaries (not real roads)
        bool nsHasMinBoundary = nsRoads.Count == 0 || nsRoads[0] > chunkMinX;
        bool nsHasMaxBoundary = nsRoads.Count == 0 || nsRoads[^1] < chunkMaxX;
        bool ewHasMinBoundary = ewRoads.Count == 0 || ewRoads[0] > chunkMinZ;
        bool ewHasMaxBoundary = ewRoads.Count == 0 || ewRoads[^1] < chunkMaxZ;

        // Add chunk boundaries as implicit road positions
        if (nsHasMinBoundary) nsRoads.Insert(0, chunkMinX);
        if (nsHasMaxBoundary) nsRoads.Add(chunkMaxX);
        if (ewHasMinBoundary) ewRoads.Insert(0, chunkMinZ);
        if (ewHasMaxBoundary) ewRoads.Add(chunkMaxZ);

        // Precompute road half-widths (chunk boundaries get 0 width)
        var nsHalfWidths = new float[nsRoads.Count];
        var ewHalfWidths = new float[ewRoads.Count];
        for (int i = 0; i < nsRoads.Count; i++)
        {
            bool isBoundary = (i == 0 && nsHasMinBoundary) || (i == nsRoads.Count - 1 && nsHasMaxBoundary);
            nsHalfWidths[i] = isBoundary ? 0 : GetRoadHalfWidth(nsRoads[i]);
        }
        for (int i = 0; i < ewRoads.Count; i++)
        {
            bool isBoundary = (i == 0 && ewHasMinBoundary) || (i == ewRoads.Count - 1 && ewHasMaxBoundary);
            ewHalfWidths[i] = isBoundary ? 0 : GetRoadHalfWidth(ewRoads[i]);
        }

        // Create blocks between roads - plots extend from road edge to road edge
        for (int i = 0; i < nsRoads.Count - 1; i++)
        {
            for (int j = 0; j < ewRoads.Count - 1; j++)
            {
                // Track which edges border real roads (not chunk boundaries)
                bool hasRoadMinX = !(i == 0 && nsHasMinBoundary);
                bool hasRoadMaxX = !(i + 1 == nsRoads.Count - 1 && nsHasMaxBoundary);
                bool hasRoadMinZ = !(j == 0 && ewHasMinBoundary);
                bool hasRoadMaxZ = !(j + 1 == ewRoads.Count - 1 && ewHasMaxBoundary);

                // Block extends from road edge to road edge (shader handles curb effect)
                var blockMinX = nsRoads[i] + nsHalfWidths[i];
                var blockMaxX = nsRoads[i + 1] - nsHalfWidths[i + 1];
                var blockMinZ = ewRoads[j] + ewHalfWidths[j];
                var blockMaxZ = ewRoads[j + 1] - ewHalfWidths[j + 1];

                // Only create block if it has positive dimensions
                if (blockMaxX > blockMinX && blockMaxZ > blockMinZ)
                {
                    blocks.Add(new CityBlock(blockMinX, blockMinZ, blockMaxX, blockMaxZ,
                        hasRoadMinX, hasRoadMaxX, hasRoadMinZ, hasRoadMaxZ));
                }
            }
        }

        return blocks;
    }

    private float GetRoadHalfWidth(float roadPosition)
    {
        // Check if this is a major road position
        var nearestMajor = MathF.Round(roadPosition / _roadMajorSpacing) * _roadMajorSpacing;
        if (MathF.Abs(roadPosition - nearestMajor) < 0.1f)
            return _majorHalfWidth;
        return _minorHalfWidth;
    }

    private void GeneratePlotAndBuildings(List<BuildingData> buildings, List<PlotData> plots, List<int> emptyPlotIndices, CityBlock block, DeterministicRng rng)
    {
        // Create plot data for pavement rendering (extends to road edge)
        var plot = new PlotData
        {
            MinX = block.MinX,
            MinZ = block.MinZ,
            MaxX = block.MaxX,
            MaxZ = block.MaxZ,
            HasRoadMinX = block.HasRoadMinX,
            HasRoadMaxX = block.HasRoadMaxX,
            HasRoadMinZ = block.HasRoadMinZ,
            HasRoadMaxZ = block.HasRoadMaxZ
        };
        var plotIndex = plots.Count;
        plots.Add(plot);

        // Get cluster parameters for this plot location, or use config fallbacks
        float plotCenterX = (block.MinX + block.MaxX) / 2f;
        float plotCenterZ = (block.MinZ + block.MaxZ) / 2f;
        var clusterParams = _clusterProvider?.GetParametersAt(plotCenterX, plotCenterZ);

        // Determine empty plot and two-building chances (cluster params or config fallback)
        float emptyPlotChance = clusterParams?.EmptyPlotChance ?? _config.EmptyPlotChance;
        float twoBuildingChance = clusterParams?.TwoBuildingChance ?? _config.TwoBuildingChance;

        // Random chance for empty plot (park)
        if (rng.NextDouble() < emptyPlotChance)
        {
            emptyPlotIndices.Add(plotIndex);
            return; // No building - just pavement (park)
        }

        // Building area is inset from plot edges by sidewalk width (road edges) or edge margin (non-road edges)
        float buildMinX = block.MinX + (block.HasRoadMinX ? _sidewalkWidth : _config.EdgeMargin);
        float buildMaxX = block.MaxX - (block.HasRoadMaxX ? _sidewalkWidth : _config.EdgeMargin);
        float buildMinZ = block.MinZ + (block.HasRoadMinZ ? _sidewalkWidth : _config.EdgeMargin);
        float buildMaxZ = block.MaxZ - (block.HasRoadMaxZ ? _sidewalkWidth : _config.EdgeMargin);

        // Skip if building area is too small
        float buildWidth = buildMaxX - buildMinX;
        float buildDepth = buildMaxZ - buildMinZ;
        if (buildWidth < 4f || buildDepth < 4f)
            return;

        // Decide: 1 or 2 buildings?
        // Only split if building area is large enough (min 16 units on the split axis)
        bool twoBuildings = rng.NextDouble() < twoBuildingChance &&
                           MathF.Max(buildWidth, buildDepth) >= 16f;

        if (twoBuildings)
        {
            // Gap between two buildings on same plot
            const float buildingGap = 4f;
            float halfGap = buildingGap / 2f;

            // Split building area in half along the longer axis, with gap between
            if (buildWidth >= buildDepth)
            {
                // Split along X axis
                float midX = (buildMinX + buildMaxX) / 2f;
                CreateBuilding(buildings, buildMinX, buildMinZ, midX - halfGap, buildMaxZ, rng);
                CreateBuilding(buildings, midX + halfGap, buildMinZ, buildMaxX, buildMaxZ, rng);
            }
            else
            {
                // Split along Z axis
                float midZ = (buildMinZ + buildMaxZ) / 2f;
                CreateBuilding(buildings, buildMinX, buildMinZ, buildMaxX, midZ - halfGap, rng);
                CreateBuilding(buildings, buildMinX, midZ + halfGap, buildMaxX, buildMaxZ, rng);
            }
        }
        else
        {
            // Single building fills the building area
            CreateBuilding(buildings, buildMinX, buildMinZ, buildMaxX, buildMaxZ, rng);
        }
    }

    private void CreateBuilding(List<BuildingData> buildings, float minX, float minZ, float maxX, float maxZ, DeterministicRng rng)
    {
        float width = maxX - minX;
        float depth = maxZ - minZ;

        // Skip if dimensions are too small
        if (width < 1f || depth < 1f)
            return;

        float centerX = (minX + maxX) / 2f;
        float centerZ = (minZ + maxZ) / 2f;

        // Calculate distance from world center
        var distFromCenter = MathF.Sqrt(centerX * centerX + centerZ * centerZ);

        // Calculate height multiplier from clusters or config fallback
        float heightMultiplier;
        if (_clusterProvider != null)
        {
            heightMultiplier = _clusterProvider.GetHeightMultiplierAt(centerX, centerZ);
        }
        else
        {
            // Fallback: use config downtown radius/multiplier
            heightMultiplier = 1.0f;
            if (distFromCenter < _config.DowntownRadius)
            {
                var downtownFactor = 1.0f - (distFromCenter / _config.DowntownRadius);
                heightMultiplier = 1.0f + downtownFactor * (_config.DowntownHeightMultiplier - 1.0f);
            }
        }

        var baseHeight = _config.MinHeight + (float)rng.NextDouble() * (_config.MaxHeight - _config.MinHeight);
        var finalHeight = baseHeight * heightMultiplier;

        // Get cluster parameters or use config fallbacks
        var clusterParams = _clusterProvider?.GetParametersAt(centerX, centerZ);

        // Determine style/aesthetic values (cluster params or config fallback)
        float steppedBuildingChance = clusterParams?.SteppedBuildingChance ?? _config.SteppedBuildingChance;
        float neonDensityMultiplier = clusterParams?.NeonDensityMultiplier ?? _defaultNeonDensityMultiplier;
        float windowLitPercentage = clusterParams?.WindowLitPercentage ?? _defaultWindowLitPercentage;

        var building = new BuildingData
        {
            X = centerX,
            Z = centerZ,
            Width = width,
            Depth = depth,
            Height = finalHeight,
            Seed = rng.NextInt(),
            DistanceFromCenter = distFromCenter,
            Grid = FacadeGrid.Compute(width, depth, finalHeight,
                _windowSpacing, _groundFloorHeight, _floorHeight, WindowEdgeMargin),
            // Set aesthetic parameters
            NeonDensityMultiplier = neonDensityMultiplier,
            WindowLitPercentage = windowLitPercentage
        };

        // Generate stepped profile for tall buildings
        if (finalHeight >= _config.SteppedMinHeight &&
            rng.NextDouble() < steppedBuildingChance)
        {
            building.Sections = GenerateSteppedSections(building, rng, clusterParams);
        }

        buildings.Add(building);
    }

    private List<BuildingSection> GenerateSteppedSections(BuildingData building, DeterministicRng rng, ClusterParameterSet? clusterParams)
    {
        var sections = new List<BuildingSection>();

        // Determine number of sections
        var numSections = rng.NextInt(_config.SteppedMinSections, _config.SteppedMaxSections + 1);

        // Calculate section heights - vary the distribution
        var totalHeight = building.Height;
        var heights = new float[numSections];
        var remainingHeight = totalHeight;

        for (int i = 0; i < numSections - 1; i++)
        {
            var minH = _config.SteppedSectionMinHeight;
            var maxH = remainingHeight - (numSections - i - 1) * minH;
            heights[i] = minH + (float)rng.NextDouble() * (maxH - minH) * 0.6f;
            remainingHeight -= heights[i];
        }
        heights[numSections - 1] = remainingHeight;

        // Get setback parameters from cluster or config fallback
        float setbackMin = clusterParams?.SetbackRatioMin ?? _config.SteppedSetbackMin;
        float setbackMax = clusterParams?.SetbackRatioMax ?? _config.SteppedSetbackMax;
        float asymmetricChance = clusterParams?.AsymmetricChance ?? _config.SteppedAsymmetricChance;

        // Build sections from bottom to top
        var currentY = 0f;
        var currentWidth = building.Width;
        var currentDepth = building.Depth;
        var isAsymmetric = rng.NextDouble() < asymmetricChance;
        var asymmetricDirection = rng.NextInt(4); // 0=+X, 1=-X, 2=+Z, 3=-Z
        var cumulativeOffsetX = 0f;
        var cumulativeOffsetZ = 0f;

        for (int i = 0; i < numSections; i++)
        {
            float sectionOffsetX = cumulativeOffsetX;
            float sectionOffsetZ = cumulativeOffsetZ;

            if (i > 0)
            {
                // Calculate setback for this section
                var setbackRatio = setbackMin +
                    (float)rng.NextDouble() * (setbackMax - setbackMin);

                var widthReduction = currentWidth * setbackRatio;
                var depthReduction = currentDepth * setbackRatio;

                currentWidth -= widthReduction;
                currentDepth -= depthReduction;

                // Ensure minimum size
                currentWidth = MathF.Max(currentWidth, 4f);
                currentDepth = MathF.Max(currentDepth, 4f);

                // Apply asymmetric offset if enabled
                if (isAsymmetric)
                {
                    var offsetAmount = widthReduction * 0.3f;
                    switch (asymmetricDirection)
                    {
                        case 0: cumulativeOffsetX += offsetAmount; break;
                        case 1: cumulativeOffsetX -= offsetAmount; break;
                        case 2: cumulativeOffsetZ += offsetAmount; break;
                        case 3: cumulativeOffsetZ -= offsetAmount; break;
                    }
                    sectionOffsetX = cumulativeOffsetX;
                    sectionOffsetZ = cumulativeOffsetZ;
                }
            }

            // Compute grid for this section
            // First section uses ground floor height, upper sections use 0
            var sectionGroundFloor = i == 0 ? _groundFloorHeight : 0f;
            var sectionHeight = heights[i];

            sections.Add(new BuildingSection
            {
                StartY = currentY,
                EndY = currentY + sectionHeight,
                Width = currentWidth,
                Depth = currentDepth,
                OffsetX = sectionOffsetX,
                OffsetZ = sectionOffsetZ,
                Grid = FacadeGrid.Compute(currentWidth, currentDepth, sectionHeight,
                    _windowSpacing, sectionGroundFloor, _floorHeight, WindowEdgeMargin)
            });

            currentY += heights[i];
        }

        return sections;
    }

    private readonly struct CityBlock
    {
        public readonly float MinX, MinZ, MaxX, MaxZ;
        // Track which edges border roads (vs chunk boundaries)
        public readonly bool HasRoadMinX, HasRoadMaxX, HasRoadMinZ, HasRoadMaxZ;

        public CityBlock(float minX, float minZ, float maxX, float maxZ,
            bool hasRoadMinX, bool hasRoadMaxX, bool hasRoadMinZ, bool hasRoadMaxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            HasRoadMinX = hasRoadMinX;
            HasRoadMaxX = hasRoadMaxX;
            HasRoadMinZ = hasRoadMinZ;
            HasRoadMaxZ = hasRoadMaxZ;
        }
    }
}
