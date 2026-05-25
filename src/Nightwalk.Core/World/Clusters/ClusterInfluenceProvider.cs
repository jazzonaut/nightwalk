using System.Collections.Concurrent;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;

namespace Nightwalk.Core.World.Clusters;

/// <summary>
/// Base parameter values used for cluster variation.
/// </summary>
public readonly struct ClusterBaseParameters
{
    public float EmptyPlotChance { get; init; }
    public float TwoBuildingChance { get; init; }
    public float SteppedBuildingChance { get; init; }
    public float SteppedSetbackMin { get; init; }
    public float SteppedSetbackMax { get; init; }
    public float SteppedAsymmetricChance { get; init; }
    public float NeonDensityMultiplier { get; init; }
    public float WindowLitPercentage { get; init; }
}

/// <summary>
/// Main service for the city cluster system.
/// Generates clusters procedurally and provides influence queries.
/// </summary>
public sealed class ClusterInfluenceProvider
{
    private readonly ClusterConfig _config;
    private readonly ClusterBaseParameters _baseParams;
    private readonly int _worldSeed;
    private readonly float _superChunkSize;
    private readonly float _maxClusterRadius;
    private readonly int _spacingReach;
    private readonly float _minSpacingSq;

    // Cache of generated clusters per super-chunk
    private readonly ConcurrentDictionary<SuperChunkCoord, List<CityCluster>> _superChunkClusters = new();

    // Lock for coordinating generation to ensure deterministic ordering
    private readonly object _generationLock = new();

    // The guaranteed origin cluster (always present if enabled)
    private readonly CityCluster? _originCluster;

    public ClusterInfluenceProvider(ClusterConfig config, ClusterBaseParameters baseParams, int worldSeed)
    {
        _config = config;
        _baseParams = baseParams;
        _worldSeed = worldSeed;
        _superChunkSize = config.SuperChunkSize;
        _maxClusterRadius = Math.Max(config.Origin.Radius, config.Generation.RadiusMax);
        _minSpacingSq = config.MinimumSpacing * config.MinimumSpacing;

        // How many super-chunks away could affect spacing validation?
        _spacingReach = (int)MathF.Ceiling(config.MinimumSpacing / _superChunkSize) + 1;

        // Create the origin cluster if enabled
        if (config.Origin.Enabled)
        {
            _originCluster = CreateOriginCluster();
        }
    }

    /// <summary>
    /// Pre-warms the cluster cache by generating chunks in expanding rings from origin.
    /// Call during initialization to reduce generation bursts when exploring distant areas.
    /// </summary>
    /// <param name="radius">Radius in super-chunk coordinates to pre-warm (e.g., 5 = 5 super-chunks in each direction)</param>
    /// <returns>Number of chunks generated</returns>
    public int PreWarm(int radius)
    {
        if (radius <= 0)
            return 0;

        int generated = 0;

        lock (_generationLock)
        {
            // Generate in distance-from-origin order to ensure proper dependency ordering
            var chunksToGenerate = new List<SuperChunkCoord>();

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    var coord = new SuperChunkCoord(x, z);
                    if (!_superChunkClusters.ContainsKey(coord))
                    {
                        chunksToGenerate.Add(coord);
                    }
                }
            }

            // Sort by global order (distance from origin, then lexicographic)
            chunksToGenerate.Sort(CompareChunkOrder);

            // Generate in order - each chunk's earlier dependencies are already generated
            foreach (var coord in chunksToGenerate)
            {
                if (!_superChunkClusters.ContainsKey(coord))
                {
                    var clusters = GenerateSingleChunk(coord);
                    _superChunkClusters[coord] = clusters;
                    generated++;
                }
            }
        }

        return generated;
    }

    /// <summary>
    /// Gets the number of currently cached super-chunks.
    /// </summary>
    public int CachedChunkCount => _superChunkClusters.Count;

    /// <summary>
    /// Gets the total height multiplier at a world position from all affecting clusters.
    /// Uses additive blending: result = 1.0 + sum of (influence * (multiplier - 1.0))
    /// </summary>
    public float GetHeightMultiplierAt(float worldX, float worldZ)
    {
        var clusters = GetAffectingClusters(worldX, worldZ);

        float additiveBonus = 0f;
        foreach (var cluster in clusters)
        {
            float influence = cluster.GetInfluenceAt(worldX, worldZ);
            if (influence > 0f)
            {
                // Additive: each cluster contributes its bonus scaled by influence
                additiveBonus += influence * (cluster.HeightMultiplier - 1f);
            }
        }

        return 1f + additiveBonus;
    }

    /// <summary>
    /// Gets the blended parameters at a world position from all affecting clusters.
    /// Parameters are weighted by cluster influence.
    /// Returns null if no clusters affect this position (caller should use config fallbacks).
    /// </summary>
    public ClusterParameterSet? GetParametersAt(float worldX, float worldZ)
    {
        var clusters = GetAffectingClusters(worldX, worldZ);

        var weighted = new List<(ClusterParameterSet Params, float Influence)>();
        foreach (var cluster in clusters)
        {
            float influence = cluster.GetInfluenceAt(worldX, worldZ);
            if (influence > 0f)
            {
                weighted.Add((cluster.Parameters, influence));
            }
        }

        if (weighted.Count == 0)
            return null;

        return ClusterParameterSet.BlendMultiple(weighted);
    }

    /// <summary>
    /// Gets all clusters that could affect a given world position.
    /// </summary>
    public List<CityCluster> GetAffectingClusters(float worldX, float worldZ)
    {
        var result = new List<CityCluster>();

        // Determine which super-chunks could contain clusters affecting this position
        var superChunksToCheck = GetRelevantSuperChunks(worldX, worldZ);

        foreach (var superChunkCoord in superChunksToCheck)
        {
            var clusters = GetOrGenerateClusters(superChunkCoord);
            foreach (var cluster in clusters)
            {
                if (cluster.ContainsPoint(worldX, worldZ))
                {
                    result.Add(cluster);
                }
            }
        }

        // Always include origin cluster check
        if (_originCluster != null && _originCluster.ContainsPoint(worldX, worldZ))
        {
            if (!result.Contains(_originCluster))
            {
                result.Add(_originCluster);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets or generates clusters for a super-chunk.
    /// Thread-safe and deterministic regardless of query order.
    /// Uses distance-from-origin ordering to ensure consistent results.
    /// </summary>
    private List<CityCluster> GetOrGenerateClusters(SuperChunkCoord coord)
    {
        // Fast path: already generated
        if (_superChunkClusters.TryGetValue(coord, out var existing))
            return existing;

        // Need to generate - use lock to ensure deterministic ordering
        lock (_generationLock)
        {
            // Double-check after acquiring lock
            if (_superChunkClusters.TryGetValue(coord, out existing))
                return existing;

            // Generate this chunk and all its earlier dependencies
            GenerateChunkWithDependencies(coord);

            return _superChunkClusters[coord];
        }
    }

    /// <summary>
    /// Generates a chunk after ensuring all "earlier" chunks in the transitive dependency closure are generated.
    /// Uses iterative approach to avoid deep recursion toward origin.
    /// The cascade is finite (terminates at origin) and results are cached for subsequent queries.
    /// First query to a distant area may generate many chunks, but this ensures strict spacing
    /// guarantees and determinism regardless of query order.
    /// </summary>
    private void GenerateChunkWithDependencies(SuperChunkCoord target)
    {
        // Already generated
        if (_superChunkClusters.ContainsKey(target))
            return;

        // Collect all chunks in the transitive dependency closure
        // This expands toward origin but is finite (origin has no earlier dependencies)
        var needed = new HashSet<SuperChunkCoord> { target };
        var frontier = new Queue<SuperChunkCoord>();
        frontier.Enqueue(target);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            for (int dx = -_spacingReach; dx <= _spacingReach; dx++)
            {
                for (int dz = -_spacingReach; dz <= _spacingReach; dz++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    var neighbor = new SuperChunkCoord(current.X + dx, current.Z + dz);

                    // Skip if already processed or cached
                    if (needed.Contains(neighbor) || _superChunkClusters.ContainsKey(neighbor))
                        continue;

                    // Skip if not earlier in global ordering
                    if (!IsEarlier(neighbor, current))
                        continue;

                    needed.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }
        }

        // Sort by global order and generate
        var sorted = new List<SuperChunkCoord>(needed);
        sorted.Sort(CompareChunkOrder);

        foreach (var coord in sorted)
        {
            if (!_superChunkClusters.ContainsKey(coord))
            {
                var clusters = GenerateSingleChunk(coord);
                _superChunkClusters[coord] = clusters;
            }
        }
    }

    /// <summary>
    /// Determines if chunk 'a' is "earlier" than chunk 'b' in the global ordering.
    /// Uses Chebyshev distance (max of |x|, |z|) so ordering matches square regions,
    /// enabling efficient pre-warming without spacing violations at boundaries.
    /// </summary>
    private static bool IsEarlier(SuperChunkCoord a, SuperChunkCoord b)
    {
        // Chebyshev distance from origin (forms square "rings")
        int distA = Math.Max(Math.Abs(a.X), Math.Abs(a.Z));
        int distB = Math.Max(Math.Abs(b.X), Math.Abs(b.Z));

        if (distA != distB)
            return distA < distB;

        // Same distance: use lexicographic order as tiebreaker
        if (a.X != b.X)
            return a.X < b.X;

        return a.Z < b.Z;
    }

    /// <summary>
    /// Comparison function for sorting chunks by the global ordering.
    /// Uses Chebyshev distance to match square regions.
    /// </summary>
    private static int CompareChunkOrder(SuperChunkCoord a, SuperChunkCoord b)
    {
        int distA = Math.Max(Math.Abs(a.X), Math.Abs(a.Z));
        int distB = Math.Max(Math.Abs(b.X), Math.Abs(b.Z));

        int cmpDist = distA.CompareTo(distB);
        if (cmpDist != 0)
            return cmpDist;

        int cmpX = a.X.CompareTo(b.X);
        if (cmpX != 0)
            return cmpX;

        return a.Z.CompareTo(b.Z);
    }

    /// <summary>
    /// Generates clusters for a single super-chunk, checking against earlier neighbors.
    /// </summary>
    private List<CityCluster> GenerateSingleChunk(SuperChunkCoord coord)
    {
        var clusters = new List<CityCluster>();
        var rng = new DeterministicRng(coord.GetSeed(_worldSeed));

        // Determine number of cluster candidates using Poisson-like distribution
        int numCandidates = SamplePoissonCount(rng, _config.AverageClustersPerSuperChunk);

        var (minX, minZ) = coord.ToWorldPosition(_superChunkSize);

        // Collect clusters from earlier neighbors for spacing validation
        var existingClusters = CollectEarlierNeighborClusters(coord);

        for (int i = 0; i < numCandidates; i++)
        {
            // Generate candidate position within super-chunk
            float candidateX = minX + (float)rng.NextDouble() * _superChunkSize;
            float candidateZ = minZ + (float)rng.NextDouble() * _superChunkSize;

            // Check minimum spacing against existing clusters (neighbors + already accepted in this chunk)
            if (!ValidateSpacing(candidateX, candidateZ, existingClusters, clusters))
                continue;

            // Generate cluster parameters
            var cluster = GenerateCluster(candidateX, candidateZ, rng);
            clusters.Add(cluster);
        }

        return clusters;
    }

    /// <summary>
    /// Collects clusters from "earlier" neighboring super-chunks for spacing validation.
    /// Only includes clusters from chunks that are earlier in the global ordering,
    /// ensuring deterministic results regardless of query order.
    /// </summary>
    private List<CityCluster> CollectEarlierNeighborClusters(SuperChunkCoord coord)
    {
        var result = new List<CityCluster>();

        // Add origin cluster if it exists (origin cluster is always "earliest")
        if (_originCluster != null)
        {
            result.Add(_originCluster);
        }

        // Add clusters only from earlier neighbors within spacing reach
        for (int dx = -_spacingReach; dx <= _spacingReach; dx++)
        {
            for (int dz = -_spacingReach; dz <= _spacingReach; dz++)
            {
                // Skip self
                if (dx == 0 && dz == 0)
                    continue;

                var neighborCoord = new SuperChunkCoord(coord.X + dx, coord.Z + dz);

                // Only include clusters from "earlier" chunks in the global ordering
                if (!IsEarlier(neighborCoord, coord))
                    continue;

                if (_superChunkClusters.TryGetValue(neighborCoord, out var neighborClusters))
                {
                    result.AddRange(neighborClusters);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Determines which super-chunks could have clusters affecting a position.
    /// Must check neighboring super-chunks since clusters can span boundaries.
    /// </summary>
    private List<SuperChunkCoord> GetRelevantSuperChunks(float worldX, float worldZ)
    {
        var result = new List<SuperChunkCoord>();

        // The position's super-chunk
        var centerCoord = SuperChunkCoord.FromWorldPosition(worldX, worldZ, _superChunkSize);

        // How many super-chunks away could a cluster center be and still reach this point?
        int reach = (int)MathF.Ceiling(_maxClusterRadius / _superChunkSize) + 1;

        for (int dx = -reach; dx <= reach; dx++)
        {
            for (int dz = -reach; dz <= reach; dz++)
            {
                result.Add(new SuperChunkCoord(centerCoord.X + dx, centerCoord.Z + dz));
            }
        }

        return result;
    }

    /// <summary>
    /// Validates that a candidate position meets minimum spacing requirements.
    /// </summary>
    private bool ValidateSpacing(float x, float z, List<CityCluster> neighborClusters, List<CityCluster> currentChunkClusters)
    {
        // Check against neighbor clusters
        foreach (var existing in neighborClusters)
        {
            if (existing.DistanceSquaredTo(x, z) < _minSpacingSq)
                return false;
        }

        // Check against clusters already accepted in this chunk
        foreach (var existing in currentChunkClusters)
        {
            if (existing.DistanceSquaredTo(x, z) < _minSpacingSq)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Generates a single cluster with randomized parameters.
    /// </summary>
    private CityCluster GenerateCluster(float centerX, float centerZ, DeterministicRng rng)
    {
        var gen = _config.Generation;

        // Random radius
        float radius = gen.RadiusMin + (float)rng.NextDouble() * (gen.RadiusMax - gen.RadiusMin);

        // Random height multiplier
        float heightMult = gen.HeightMultiplierMin +
            (float)rng.NextDouble() * (gen.HeightMultiplierMax - gen.HeightMultiplierMin);

        // Weighted random falloff selection
        var falloff = SelectWeightedFalloff(rng);

        // Generate parameter set
        var parameters = GenerateParameterSet(rng);

        return new CityCluster
        {
            CenterX = centerX,
            CenterZ = centerZ,
            Radius = radius,
            HeightMultiplier = heightMult,
            Falloff = falloff,
            Parameters = parameters,
            IsOriginCluster = false
        };
    }

    /// <summary>
    /// Creates the guaranteed origin cluster at (0,0).
    /// </summary>
    private CityCluster CreateOriginCluster()
    {
        var origin = _config.Origin;

        // Use world seed for deterministic origin parameters
        var rng = new DeterministicRng(_worldSeed);
        var parameters = GenerateParameterSet(rng);

        // Apply origin-specific multipliers from config
        parameters = new ClusterParameterSet
        {
            EmptyPlotChance = parameters.EmptyPlotChance * origin.EmptyPlotChanceMultiplier,
            TwoBuildingChance = parameters.TwoBuildingChance,
            SteppedBuildingChance = parameters.SteppedBuildingChance * origin.SteppedBuildingChanceMultiplier,
            SetbackRatioMin = parameters.SetbackRatioMin,
            SetbackRatioMax = parameters.SetbackRatioMax,
            AsymmetricChance = parameters.AsymmetricChance,
            NeonDensityMultiplier = parameters.NeonDensityMultiplier * origin.NeonDensityMultiplier,
            WindowLitPercentage = parameters.WindowLitPercentage * origin.WindowLitPercentageMultiplier
        };

        return new CityCluster
        {
            CenterX = 0f,
            CenterZ = 0f,
            Radius = origin.Radius,
            HeightMultiplier = origin.HeightMultiplier,
            Falloff = ClusterFalloffEvaluator.Parse(origin.Falloff),
            Parameters = parameters,
            IsOriginCluster = true
        };
    }

    /// <summary>
    /// Generates a randomized parameter set by applying a random multiplier to base values.
    /// </summary>
    private ClusterParameterSet GenerateParameterSet(DeterministicRng rng)
    {
        var gen = _config.Generation;
        float multiplier = gen.MultiplierMin + (float)rng.NextDouble() * (gen.MultiplierMax - gen.MultiplierMin);

        return new ClusterParameterSet
        {
            EmptyPlotChance = _baseParams.EmptyPlotChance * multiplier,
            TwoBuildingChance = _baseParams.TwoBuildingChance * multiplier,
            SteppedBuildingChance = _baseParams.SteppedBuildingChance * multiplier,
            SetbackRatioMin = _baseParams.SteppedSetbackMin,
            SetbackRatioMax = _baseParams.SteppedSetbackMax,
            AsymmetricChance = _baseParams.SteppedAsymmetricChance * multiplier,
            NeonDensityMultiplier = _baseParams.NeonDensityMultiplier * multiplier,
            WindowLitPercentage = _baseParams.WindowLitPercentage * multiplier
        };
    }

    /// <summary>
    /// Selects a falloff type using weighted random selection.
    /// </summary>
    private ClusterFalloff SelectWeightedFalloff(DeterministicRng rng)
    {
        var weights = _config.Generation.FalloffWeights;
        float total = weights.Linear + weights.Smoothstep + weights.Exponential + weights.InverseSquare;

        float roll = (float)rng.NextDouble() * total;

        if (roll < weights.Linear)
            return ClusterFalloff.Linear;

        roll -= weights.Linear;
        if (roll < weights.Smoothstep)
            return ClusterFalloff.Smoothstep;

        roll -= weights.Smoothstep;
        if (roll < weights.Exponential)
            return ClusterFalloff.Exponential;

        return ClusterFalloff.InverseSquare;
    }

    /// <summary>
    /// Samples a Poisson-distributed count given an average.
    /// Uses simple rejection method for small lambda.
    /// </summary>
    private static int SamplePoissonCount(DeterministicRng rng, float lambda)
    {
        // Knuth algorithm for Poisson random numbers
        double L = Math.Exp(-lambda);
        int k = 0;
        double p = 1.0;

        do
        {
            k++;
            p *= rng.NextDouble();
        } while (p > L);

        return k - 1;
    }
}
