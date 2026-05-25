using Nightwalk.Core.Buildings;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Godot.Rendering.Constants;
using Nightwalk.Godot.Rendering.SubRenderers;

namespace Nightwalk.Godot.Buildings;

/// <summary>
/// Detects when a player is standing on a building rooftop.
/// </summary>
public sealed class RooftopDetector
{
    private readonly ChunkManager _chunkManager;
    private readonly GameDataService _data;

    private FacadeConfig FacadeConfig => _data.Worldgen.Facades;
    private float HeightTolerance => _data.Gameplay.Buildings.RooftopHeightTolerance;
    private float MinRooftopHeight => _data.Gameplay.Buildings.MinRooftopHeight;

    /// <summary>
    /// Creates a rooftop detector.
    /// </summary>
    /// <param name="chunkManager">Chunk manager to access building data.</param>
    /// <param name="data">Game data service for configuration.</param>
    public RooftopDetector(ChunkManager chunkManager, GameDataService data)
    {
        _chunkManager = chunkManager;
        _data = data;
    }

    /// <summary>
    /// Checks if a position is on a building rooftop.
    /// Returns the building ID and data if found, null otherwise.
    /// </summary>
    public (BuildingId Id, BuildingData Building)? GetRooftopAt(float worldX, float worldY, float worldZ)
    {
        // Quick exit: player too low to be on any rooftop
        if (worldY < MinRooftopHeight)
            return null;

        var chunk = _chunkManager.GetChunkAtPosition(worldX, worldZ);
        if (chunk == null)
            return null;

        // Max possible height including rooftop features (constant)
        const float maxFeatureHeight = RenderingConstants.RooftopFeatureMinHeight + RenderingConstants.RooftopFeatureHeightRange;

        // Check each building in the chunk
        for (int i = 0; i < chunk.Buildings.Count; i++)
        {
            var building = chunk.Buildings[i];

            // XZ bounds check first (most selective - player in only one footprint)
            float halfWidth = building.Width / 2f;
            float halfDepth = building.Depth / 2f;

            if (worldX < building.X - halfWidth || worldX > building.X + halfWidth)
                continue;
            if (worldZ < building.Z - halfDepth || worldZ > building.Z + halfDepth)
                continue;

            // Y range check (many buildings can have overlapping height ranges)
            if (worldY < building.Height - HeightTolerance - 1f || worldY > building.Height + maxFeatureHeight + HeightTolerance)
                continue;

            // Check rooftop feature first (if it exists)
            if (IsOnRooftopFeature(building, worldX, worldY, worldZ))
            {
                return (new BuildingId(chunk.Coord, i), building);
            }

            // Get the effective rooftop height
            // For stepped buildings, check each section from top to bottom
            float rooftopHeight = GetRooftopHeightAt(building, worldX, worldZ);

            // Check if player Y is at rooftop level (with tolerance)
            if (worldY >= rooftopHeight - 0.5f && worldY <= rooftopHeight + HeightTolerance)
            {
                return (new BuildingId(chunk.Coord, i), building);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if player is standing on a rooftop feature (AC unit, water tank, etc.).
    /// Uses same seed-based logic as BuildingRenderer.
    /// </summary>
    private bool IsOnRooftopFeature(BuildingData building, float worldX, float worldY, float worldZ)
    {
        var rooftop = BuildingRenderer.GetRooftopCenterPosition(building, FacadeConfig.RooftopFeatureChance);

        // No feature on this building
        if (rooftop.FeatureHeight == 0f)
            return false;

        // Get feature dimensions for XZ bounds check
        var feature = BuildingRenderer.GetRooftopFeatureDimensions(
            building.Seed, rooftop.BaseWidth, rooftop.BaseDepth, FacadeConfig.RooftopFeatureChance);

        // Check XZ bounds
        float halfW = feature.Width / 2f;
        float halfD = feature.Depth / 2f;

        if (worldX < rooftop.X - halfW || worldX > rooftop.X + halfW)
            return false;
        if (worldZ < rooftop.Z - halfD || worldZ > rooftop.Z + halfD)
            return false;

        // Check Y (player standing on top of feature)
        return worldY >= rooftop.Y - 0.5f && worldY <= rooftop.Y + HeightTolerance;
    }

    /// <summary>
    /// Gets the rooftop height at a specific XZ position within a building.
    /// Handles stepped buildings with multiple sections.
    /// </summary>
    private float GetRooftopHeightAt(BuildingData building, float worldX, float worldZ)
    {
        // Simple building - just return total height
        if (!building.IsStepped || building.Sections == null)
            return building.Height;

        // Stepped building - find which section the position is in
        // Check from top section down
        for (int i = building.Sections.Count - 1; i >= 0; i--)
        {
            var section = building.Sections[i];
            float sectionX = building.X + section.OffsetX;
            float sectionZ = building.Z + section.OffsetZ;
            float sectionHalfW = section.Width / 2f;
            float sectionHalfD = section.Depth / 2f;

            // Check if position is within this section's bounds
            if (worldX >= sectionX - sectionHalfW && worldX <= sectionX + sectionHalfW &&
                worldZ >= sectionZ - sectionHalfD && worldZ <= sectionZ + sectionHalfD)
            {
                return section.EndY;
            }
        }

        // Fallback to building height
        return building.Height;
    }
}
