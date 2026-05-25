using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Core.World.Buildings;

namespace Nightwalk.Core.World.Neon;

/// <summary>
/// Generates neon light placements on buildings.
/// </summary>
public sealed class NeonPlacementGenerator
{
    private readonly NeonConfig _config;
    private readonly FacadeConfig _facadeConfig;

    public NeonPlacementGenerator(NeonConfig config, FacadeConfig facadeConfig)
    {
        _config = config;
        _facadeConfig = facadeConfig;
    }

    /// <summary>
    /// Gets the section dimensions at a given height for a building.
    /// Returns the section's width, depth, center offsets, and Y bounds.
    /// For non-stepped buildings, returns the base building dimensions.
    /// </summary>
    private static (float Width, float Depth, float OffsetX, float OffsetZ, float StartY, float EndY) GetSectionAtHeight(
        BuildingData building, float y)
    {
        if (building.Sections == null || building.Sections.Count == 0)
        {
            return (building.Width, building.Depth, 0f, 0f, 0f, building.Height);
        }

        // Find the section that contains this height
        foreach (var section in building.Sections)
        {
            if (y >= section.StartY && y <= section.EndY)
            {
                return (section.Width, section.Depth, section.OffsetX, section.OffsetZ, section.StartY, section.EndY);
            }
        }

        // If above all sections, use the topmost section
        var topSection = building.Sections[^1];
        return (topSection.Width, topSection.Depth, topSection.OffsetX, topSection.OffsetZ, topSection.StartY, topSection.EndY);
    }

    /// <summary>
    /// Calculates facade position for a sign, accounting for stepped building sections.
    /// Returns null if the sign cannot be placed on this facade at this height.
    /// </summary>
    /// <param name="building">The building to place on.</param>
    /// <param name="facade">Facade index: 0=front(+Z), 1=back(-Z), 2=left(-X), 3=right(+X).</param>
    /// <param name="y">Height of the sign center.</param>
    /// <param name="signWidth">Width of the sign.</param>
    /// <param name="horizontalOffset">Offset from facade center in world units (not normalized).</param>
    /// <param name="standoff">Distance from facade surface.</param>
    private static (float X, float Z, float RotationY)? GetFacadePosition(
        BuildingData building, int facade, float y, float signWidth, float horizontalOffset, float standoff)
    {
        var (sectionWidth, sectionDepth, sectionOffsetX, sectionOffsetZ, _, _) = GetSectionAtHeight(building, y);

        // Section center in world coordinates
        float sectionCenterX = building.X + sectionOffsetX;
        float sectionCenterZ = building.Z + sectionOffsetZ;

        // Check if sign width fits on this facade
        float facadeWidth = facade < 2 ? sectionWidth : sectionDepth;
        if (signWidth > facadeWidth * 0.9f)
        {
            return null; // Sign too wide for this section's facade
        }

        float x, z, rotationY;
        float halfWidth = sectionWidth / 2f;
        float halfDepth = sectionDepth / 2f;

        switch (facade)
        {
            case 0: // Front (+Z)
                x = sectionCenterX + horizontalOffset;
                z = sectionCenterZ + halfDepth + standoff;
                rotationY = 0;
                break;
            case 1: // Back (-Z)
                x = sectionCenterX + horizontalOffset;
                z = sectionCenterZ - halfDepth - standoff;
                rotationY = MathF.PI;
                break;
            case 2: // Left (-X)
                x = sectionCenterX - halfWidth - standoff;
                z = sectionCenterZ + horizontalOffset;
                rotationY = -MathF.PI / 2f;
                break;
            default: // Right (+X)
                x = sectionCenterX + halfWidth + standoff;
                z = sectionCenterZ + horizontalOffset;
                rotationY = MathF.PI / 2f;
                break;
        }

        return (x, z, rotationY);
    }

    /// <summary>
    /// Generates neon lights for a building.
    /// </summary>
    public List<NeonLightData> GenerateNeons(BuildingData building, int chunkSeed)
    {
        var neons = new List<NeonLightData>();
        // Use stable hash for deterministic per-building seed
        var buildingSeed = StableHash.Combine(chunkSeed, (int)(building.X * 1000), (int)(building.Z * 1000));
        var rng = new DeterministicRng(buildingSeed);

        // Skip if no color palette
        if (_config.ColorPalette == null || _config.ColorPalette.Count == 0)
            return neons;

        // Determine how many neons based on building size and density
        // Apply cluster neon density multiplier for regional variation
        var facadeArea = (building.Width + building.Depth) * 2 * building.Height;
        var effectiveDensity = _config.PlacementDensity * building.NeonDensityMultiplier;
        var baseNeonCount = (int)(facadeArea * effectiveDensity / 500f);
        var neonCount = Math.Max(0, baseNeonCount + rng.NextInt(-1, 2));

        for (int i = 0; i < neonCount; i++)
        {
            var neon = GenerateNeonForBuilding(building, rng);
            if (neon != null)
            {
                neons.Add(neon);
            }
        }

        // Large sign on upper facade (for tall buildings)
        if (building.Height > 40 && rng.NextDouble() < _config.LargeSignChance)
        {
            var largeSign = GenerateLargeSign(building, rng);
            if (largeSign != null)
                neons.Add(largeSign);
        }

        // Corner strips
        if (building.Height > 30 && rng.NextDouble() < _config.CornerStripChance)
        {
            neons.AddRange(GenerateCornerStrips(building, rng));
        }

        // Full-height strips
        if (building.Height > 50 && rng.NextDouble() < _config.FullHeightStripChance)
        {
            var strip = GenerateFullHeightStrip(building, rng);
            if (strip != null)
                neons.Add(strip);
        }

        // Kanji signs
        if (building.Height > _config.KanjiSignMinHeight && rng.NextDouble() < _config.KanjiSignChance)
        {
            var kanjiSign = GenerateKanjiSign(building, rng);
            if (kanjiSign != null)
                neons.Add(kanjiSign);
        }

        // Billboards
        if (building.Height > _config.BillboardMinHeight && rng.NextDouble() < _config.BillboardChance)
        {
            var billboard = GenerateBillboard(building, rng);
            if (billboard != null)
                neons.Add(billboard);
        }

        return neons;
    }

    private NeonLightData? GenerateLargeSign(BuildingData building, DeterministicRng rng)
    {
        var facade = rng.NextInt(4);
        var color = _config.ColorPalette[rng.NextInt(_config.ColorPalette.Count)];
        var intensity = _config.IntensityMax * NeonPlacementConstants.LargeSignIntensityMultiplier;
        var facadeInset = _facadeConfig.FacadeInset;

        // Use base grid to estimate Y position first (large signs go on upper floors)
        var baseGrid = building.Grid;
        var baseNumFloors = baseGrid.NumFloors;

        // Position high on building (upper 40%) - estimate floor first
        var minFloorEst = (int)(baseNumFloors * _config.LargeSignMinFloor);
        var maxFloorEst = Math.Max(minFloorEst, baseNumFloors - 2);
        if (maxFloorEst <= minFloorEst) return null;

        var signFloorEst = minFloorEst + rng.NextInt(maxFloorEst - minFloorEst + 1);
        var estimatedY = baseGrid.GroundFloorHeight + signFloorEst * baseGrid.FloorHeight;
        estimatedY = Math.Clamp(estimatedY, baseGrid.GroundFloorHeight, building.Height - 2f);

        // Get section at this height for accurate dimensions
        var (sectionWidth, sectionDepth, _, _, sectionStartY, sectionEndY) = GetSectionAtHeight(building, estimatedY);
        var grid = building.GetGridAtHeight(estimatedY);

        var windowSpacing = grid.WindowSpacing;
        var floorHeight = grid.FloorHeight;
        var groundFloorHeight = grid.GroundFloorHeight;
        var facadeWidth = facade < 2 ? sectionWidth : sectionDepth;
        var numFloors = grid.NumFloors;
        var numWindowColumns = grid.GetColumnsForFacade(facade);

        // Skip if section is too narrow
        if (numWindowColumns < 2) return null;

        // Large signs span 3-6 window columns
        var minColumns = (int)Math.Ceiling(_config.LargeSignMinWidth / windowSpacing);
        var maxColumns = Math.Min((int)(_config.LargeSignMaxWidth / windowSpacing), numWindowColumns);
        if (maxColumns < minColumns) return null; // Facade too narrow

        var signColumns = minColumns + rng.NextInt(Math.Max(1, maxColumns - minColumns + 1));
        var width = signColumns * windowSpacing - NeonPlacementConstants.LargeSignWidthMargin;

        // Large signs span 1-2 floors
        var signFloors = 1 + rng.NextInt(2);
        var height = signFloors * floorHeight - NeonPlacementConstants.LargeSignHeightMargin;
        height = Math.Clamp(height, _config.LargeSignMinHeight, _config.LargeSignMaxHeight);

        // Position within section bounds
        var minFloor = (int)(numFloors * _config.LargeSignMinFloor);
        var maxFloor = Math.Max(minFloor, numFloors - signFloors);
        if (maxFloor <= minFloor) return null;

        var signFloor = minFloor + rng.NextInt(maxFloor - minFloor + 1);
        // Y is section-relative, add sectionStartY to get world coordinates
        var y = sectionStartY + groundFloorHeight + signFloor * floorHeight + (signFloors * floorHeight) / 2f;

        // Clamp Y to section bounds
        y = Math.Min(y, sectionEndY - height / 2f - 0.5f);

        // Align to window column center (world units)
        var maxStartColumn = Math.Max(0, numWindowColumns - signColumns);
        var startColumn = rng.NextInt(maxStartColumn + 1);
        var centerColumn = startColumn + (signColumns - 1) / 2f;
        var horizontalOffset = (centerColumn - (numWindowColumns - 1) / 2f) * windowSpacing;

        // Clamp to keep sign on facade
        var maxOffset = facadeWidth / 2f - width / 2f - 0.5f;
        if (maxOffset < 0) return null; // Sign too wide for facade
        horizontalOffset = Math.Clamp(horizontalOffset, -maxOffset, maxOffset);

        var position = GetFacadePosition(building, facade, y, width, horizontalOffset,
            facadeInset + NeonPlacementConstants.LargeSignExtraStandoff);

        if (position == null)
        {
            return null; // Sign doesn't fit on this section's facade
        }

        var (x, z, rotationY) = position.Value;

        return new NeonLightData
        {
            X = x,
            Y = y,
            Z = z,
            Width = width,
            Height = height,
            Shape = NeonShape.LargeSign,
            Color = color,
            Intensity = intensity,
            RotationY = rotationY
        };
    }

    private List<NeonLightData> GenerateCornerStrips(BuildingData building, DeterministicRng rng)
    {
        var strips = new List<NeonLightData>();
        var color = _config.ColorPalette[rng.NextInt(_config.ColorPalette.Count)];
        var intensity = (_config.IntensityMin +
            (float)rng.NextDouble() * (_config.IntensityMax - _config.IntensityMin))
            * NeonPlacementConstants.CornerStripIntensityMultiplier;

        // Pick 1-2 corners
        var numCorners = rng.NextInt(1, 3);
        var corners = Enumerable.Range(0, 4).OrderBy(_ => rng.NextInt()).Take(numCorners).ToList();

        // Inset from corner to ensure proper attachment to building walls
        var insetAmount = NeonPlacementConstants.CornerStripInset;

        foreach (var corner in corners)
        {
            float x, z;
            switch (corner)
            {
                case 0: // +X +Z
                    x = building.X + building.Width / 2f - insetAmount;
                    z = building.Z + building.Depth / 2f - insetAmount;
                    break;
                case 1: // +X -Z
                    x = building.X + building.Width / 2f - insetAmount;
                    z = building.Z - building.Depth / 2f + insetAmount;
                    break;
                case 2: // -X +Z
                    x = building.X - building.Width / 2f + insetAmount;
                    z = building.Z + building.Depth / 2f - insetAmount;
                    break;
                default: // -X -Z
                    x = building.X - building.Width / 2f + insetAmount;
                    z = building.Z - building.Depth / 2f + insetAmount;
                    break;
            }

            // Determine actual corner height for stepped buildings
            var cornerHeight = building.Height;
            if (building.Sections != null && building.Sections.Count > 0)
            {
                // Check which section actually covers this corner
                foreach (var section in building.Sections.AsEnumerable().Reverse())
                {
                    var sectionHalfW = section.Width / 2f;
                    var sectionHalfD = section.Depth / 2f;
                    var sectionCenterX = building.X + section.OffsetX;
                    var sectionCenterZ = building.Z + section.OffsetZ;

                    // Check if corner is within this section's footprint
                    if (Math.Abs(x - sectionCenterX) <= sectionHalfW + 0.1f &&
                        Math.Abs(z - sectionCenterZ) <= sectionHalfD + 0.1f)
                    {
                        cornerHeight = section.EndY;
                        break;
                    }
                }
            }

            // Strip runs from near ground to near top of actual corner height
            var stripHeight = cornerHeight * (0.7f + (float)rng.NextDouble() * 0.25f);
            var startY = 3f + (float)rng.NextDouble() * 5f;

            // Ensure strip doesn't exceed corner height
            if (startY + stripHeight > cornerHeight - 1f)
            {
                stripHeight = cornerHeight - startY - 1f;
            }

            // Skip if strip would be too short
            if (stripHeight < 5f) continue;

            // Randomly select corner style (0=L, 1=T, 2=Cross, 3=Bracket)
            var cornerStyle = rng.NextInt(4);

            strips.Add(new NeonLightData
            {
                X = x,
                Y = startY + stripHeight / 2f,
                Z = z,
                Width = _config.CornerStripWidth,
                Height = stripHeight,
                Shape = NeonShape.CornerStrip,
                Color = color,
                Intensity = intensity,
                RotationY = 0, // Corner strips are rendered specially
                CornerStyle = cornerStyle
            });
        }

        return strips;
    }

    private NeonLightData? GenerateFullHeightStrip(BuildingData building, DeterministicRng rng)
    {
        var facade = rng.NextInt(4);
        var edgeSide = rng.NextInt(2); // 0 = left edge, 1 = right edge of facade
        var color = _config.ColorPalette[rng.NextInt(_config.ColorPalette.Count)];
        var intensity = _config.IntensityMax * NeonPlacementConstants.FullHeightStripIntensityMultiplier;
        var facadeInset = _facadeConfig.FacadeInset;

        // Calculate maximum strip height, accounting for stepped buildings
        var maxStripHeight = building.Height;
        if (building.Sections != null && building.Sections.Count > 0)
        {
            // Use base section height for full-height strips
            maxStripHeight = building.Sections[0].EndY;
        }

        // Clamp strip height to maximum ratio of available height with minimum gaps
        var stripHeight = Math.Min(
            maxStripHeight - NeonPlacementConstants.FullHeightStripTopGap,
            maxStripHeight * NeonPlacementConstants.FullHeightStripMaxHeightRatio);

        float x, z, rotationY;

        var edgeOffset = NeonPlacementConstants.FullHeightStripEdgeOffset;
        switch (facade)
        {
            case 0: // Front facade (+Z)
                x = building.X + (edgeSide == 0 ? -building.Width / 2f + edgeOffset : building.Width / 2f - edgeOffset);
                z = building.Z + building.Depth / 2f + facadeInset;
                rotationY = 0;
                break;
            case 1: // Back facade (-Z)
                x = building.X + (edgeSide == 0 ? -building.Width / 2f + edgeOffset : building.Width / 2f - edgeOffset);
                z = building.Z - building.Depth / 2f - facadeInset;
                rotationY = MathF.PI;
                break;
            case 2: // Left facade (-X)
                x = building.X - building.Width / 2f - facadeInset;
                z = building.Z + (edgeSide == 0 ? -building.Depth / 2f + edgeOffset : building.Depth / 2f - edgeOffset);
                rotationY = -MathF.PI / 2f;
                break;
            default: // Right facade (+X)
                x = building.X + building.Width / 2f + facadeInset;
                z = building.Z + (edgeSide == 0 ? -building.Depth / 2f + edgeOffset : building.Depth / 2f - edgeOffset);
                rotationY = MathF.PI / 2f;
                break;
        }

        return new NeonLightData
        {
            X = x,
            Y = NeonPlacementConstants.FullHeightStripYStart + stripHeight / 2f,
            Z = z,
            Width = _config.FullHeightStripWidth,
            Height = stripHeight,
            Shape = NeonShape.FullHeightStrip,
            Color = color,
            Intensity = intensity,
            RotationY = rotationY
        };
    }

    private NeonLightData? GenerateKanjiSign(BuildingData building, DeterministicRng rng)
    {
        var facade = rng.NextInt(4);
        var color = _config.ColorPalette[rng.NextInt(_config.ColorPalette.Count)];
        var intensity = _config.IntensityMax * NeonPlacementConstants.KanjiSignIntensityMultiplier;
        var facadeInset = _facadeConfig.FacadeInset;

        // Kanji signs are vertical and narrow (like Japanese shop signs)
        var width = 1.5f + (float)rng.NextDouble() * 1.5f;
        var height = 4f + (float)rng.NextDouble() * 8f;

        // Use base grid to estimate Y position first (kanji signs go on lower-mid floors)
        var baseGrid = building.Grid;
        var baseFloorHeight = baseGrid.FloorHeight;
        var baseGroundFloorHeight = baseGrid.GroundFloorHeight;

        // Position on lower-mid portion of building (street level visibility)
        var minY = baseGroundFloorHeight + baseFloorHeight * 2; // Start above 2nd floor
        var maxY = building.Height - height / 2f - 1f;

        // Skip if building is too short for a kanji sign
        if (maxY <= minY) return null;

        var estimatedY = minY + (float)rng.NextDouble() * Math.Min(maxY - minY, 20f);

        // Get section at this height for accurate dimensions
        var (sectionWidth, sectionDepth, _, _, sectionStartY, sectionEndY) = GetSectionAtHeight(building, estimatedY);
        var grid = building.GetGridAtHeight(estimatedY);

        var windowSpacing = grid.WindowSpacing;
        var floorHeight = grid.FloorHeight;
        var groundFloorHeight = grid.GroundFloorHeight;
        var facadeWidth = facade < 2 ? sectionWidth : sectionDepth;
        var numWindowColumns = grid.GetColumnsForFacade(facade);

        // Skip if section is too narrow
        if (numWindowColumns < 1) return null;

        // Recalculate Y within section bounds
        var y = Math.Min(estimatedY, sectionEndY - height / 2f - 0.5f);

        // Align to window column (world units) - place between or on windows
        var columnIndex = rng.NextInt(Math.Max(1, numWindowColumns));
        var horizontalOffset = (columnIndex - (numWindowColumns - 1) / 2f) * windowSpacing;

        // Clamp to keep sign on facade
        var maxOffset = facadeWidth / 2f - width / 2f - 0.5f;
        if (maxOffset < 0) return null; // Sign too wide for facade
        horizontalOffset = Math.Clamp(horizontalOffset, -maxOffset, maxOffset);

        var position = GetFacadePosition(building, facade, y, width, horizontalOffset,
            facadeInset + NeonPlacementConstants.KanjiSignExtraStandoff);

        if (position == null)
        {
            return null; // Sign doesn't fit on this section's facade
        }

        var (x, z, rotationY) = position.Value;

        // Broken state (20% chance)
        var isBroken = rng.NextDouble() < 0.20;
        var brokenIntensity = isBroken ? 0.4f + (float)rng.NextDouble() * 0.5f : 0f;

        // Parallax layers (2-3 for visual depth)
        var parallaxLayers = 2 + rng.NextInt(2); // 2 or 3
        var parallaxDepth = 0.1f + (float)rng.NextDouble() * 0.1f;

        return new NeonLightData
        {
            X = x,
            Y = y,
            Z = z,
            Width = width,
            Height = height,
            Shape = NeonShape.KanjiSign,
            Color = color,
            Intensity = intensity,
            RotationY = rotationY,
            Seed = rng.NextInt(10000),
            IsBroken = isBroken,
            BrokenIntensity = brokenIntensity,
            ParallaxLayers = parallaxLayers,
            ParallaxDepth = parallaxDepth
        };
    }

    private NeonLightData? GenerateBillboard(BuildingData building, DeterministicRng rng)
    {
        var facade = rng.NextInt(4);
        var primaryColor = _config.ColorPalette[rng.NextInt(_config.ColorPalette.Count)];
        var secondaryColor = _config.ColorPalette[rng.NextInt(_config.ColorPalette.Count)];
        var intensity = _config.IntensityMax * NeonPlacementConstants.BillboardIntensityMultiplier;
        var facadeInset = _facadeConfig.FacadeInset;

        // Use base grid to estimate Y position first
        var baseGrid = building.Grid;
        var baseFloorHeight = baseGrid.FloorHeight;
        var baseGroundFloorHeight = baseGrid.GroundFloorHeight;
        var baseNumFloors = baseGrid.NumFloors;

        // Billboards span 1-2 floors in height
        var billboardFloors = 1 + rng.NextInt(2);

        // Calculate valid floor range ensuring billboard stays within building bounds
        var minFloor = 3; // Above 3rd floor
        var maxFloor = Math.Max(minFloor, baseNumFloors - billboardFloors);

        // Skip if building is too short for a billboard
        if (maxFloor <= minFloor) return null;

        // Position aligned to floor (20-70% range for better street visibility)
        var targetFloor = (int)(baseNumFloors * 0.2f + rng.NextDouble() * baseNumFloors * 0.5f);
        targetFloor = Math.Clamp(targetFloor, minFloor, maxFloor);
        var estimatedY = baseGroundFloorHeight + targetFloor * baseFloorHeight + (billboardFloors * baseFloorHeight) / 2f;
        estimatedY = Math.Clamp(estimatedY, baseGroundFloorHeight + minFloor * baseFloorHeight, building.Height - 2f);

        // Get section at this height for accurate dimensions
        var (sectionWidth, sectionDepth, _, _, sectionStartY, sectionEndY) = GetSectionAtHeight(building, estimatedY);
        var grid = building.GetGridAtHeight(estimatedY);

        var windowSpacing = grid.WindowSpacing;
        var floorHeight = grid.FloorHeight;
        var groundFloorHeight = grid.GroundFloorHeight;
        var numFloors = grid.NumFloors;
        var facadeWidth = facade < 2 ? sectionWidth : sectionDepth;
        var numWindowColumns = grid.GetColumnsForFacade(facade);

        // Skip if section is too narrow
        if (numWindowColumns < 2) return null;

        var height = billboardFloors * floorHeight - 0.5f;
        height = Math.Clamp(height, 2f, 6f);

        // Recalculate Y within section bounds
        var y = Math.Min(estimatedY, sectionEndY - height / 2f - 0.5f);
        if (y < sectionStartY + groundFloorHeight + minFloor * floorHeight) return null;

        // Billboards are large and wide - span 3-6 window columns
        var minColumns = Math.Max(2, (int)(4f / windowSpacing));
        var maxColumns = Math.Min((int)(facadeWidth * 0.75f / windowSpacing), numWindowColumns);
        if (maxColumns < minColumns) return null; // Facade too narrow for a billboard

        var billboardColumns = minColumns + rng.NextInt(Math.Max(1, maxColumns - minColumns + 1));
        var width = billboardColumns * windowSpacing - 0.5f;
        width = Math.Clamp(width, 4f, 16f);

        // Align to window column center (world units)
        var maxStartColumn = Math.Max(0, numWindowColumns - billboardColumns);
        var startColumn = rng.NextInt(maxStartColumn + 1);
        var centerColumn = startColumn + (billboardColumns - 1) / 2f;
        var horizontalOffset = (centerColumn - (numWindowColumns - 1) / 2f) * windowSpacing;

        // Clamp to keep billboard on facade
        var maxOffset = facadeWidth / 2f - width / 2f - 0.5f;
        if (maxOffset < 0) return null; // Billboard too wide for facade
        horizontalOffset = Math.Clamp(horizontalOffset, -maxOffset, maxOffset);

        var position = GetFacadePosition(building, facade, y, width, horizontalOffset,
            facadeInset + NeonPlacementConstants.BillboardExtraStandoff);

        if (position == null)
        {
            return null; // Billboard doesn't fit on this section's facade
        }

        var (x, z, rotationY) = position.Value;

        // Broken state (20% chance)
        var isBroken = rng.NextDouble() < 0.20;
        var brokenIntensity = isBroken ? 0.4f + (float)rng.NextDouble() * 0.5f : 0f;

        // Parallax layers (2-3 for visual depth)
        var parallaxLayers = 2 + rng.NextInt(2); // 2 or 3
        var parallaxDepth = 0.08f + (float)rng.NextDouble() * 0.08f;

        return new NeonLightData
        {
            X = x,
            Y = y,
            Z = z,
            Width = width,
            Height = height,
            Shape = NeonShape.Billboard,
            Color = primaryColor,
            SecondaryColor = secondaryColor,
            Intensity = intensity,
            RotationY = rotationY,
            Seed = rng.NextInt(10000),
            IsBroken = isBroken,
            BrokenIntensity = brokenIntensity,
            ParallaxLayers = parallaxLayers,
            ParallaxDepth = parallaxDepth
        };
    }

    private NeonLightData? GenerateNeonForBuilding(BuildingData building, DeterministicRng rng)
    {
        // Pick a facade (0=front, 1=back, 2=left, 3=right)
        var facade = rng.NextInt(4);

        // Pick shape (only basic shapes - special types have dedicated generators)
        var basicShapes = new[] {
            NeonShape.HorizontalBar,
            NeonShape.VerticalStrip,
            NeonShape.Outline,
            NeonShape.Sign
        };
        var shape = basicShapes[rng.NextInt(basicShapes.Length)];

        // Pick color from palette
        var color = _config.ColorPalette[rng.NextInt(_config.ColorPalette.Count)];

        // Pick intensity
        var intensity = _config.IntensityMin + (float)rng.NextDouble() * (_config.IntensityMax - _config.IntensityMin);

        // Get base grid for initial floor count estimate
        var baseGrid = building.Grid;
        var windowWidth = _facadeConfig.WindowWidth;
        var windowHeight = _facadeConfig.WindowHeight;

        // First pass: determine approximate Y position to find the right section
        float estimatedY;
        var baseNumFloors = baseGrid.NumFloors;
        switch (shape)
        {
            case NeonShape.HorizontalBar:
                var barFloorEst = 1 + rng.NextInt(Math.Max(1, baseNumFloors - 1));
                estimatedY = baseGrid.GroundFloorHeight + barFloorEst * baseGrid.FloorHeight;
                break;
            case NeonShape.VerticalStrip:
                var stripFloorsEst = 1 + rng.NextInt(Math.Min(4, baseNumFloors - 1));
                var stripStartFloorEst = 1 + rng.NextInt(Math.Max(1, baseNumFloors - stripFloorsEst));
                estimatedY = baseGrid.GroundFloorHeight + stripStartFloorEst * baseGrid.FloorHeight + (stripFloorsEst * baseGrid.FloorHeight) / 2f;
                break;
            case NeonShape.Outline:
                var outlineFloorsEst = 1 + rng.NextInt(Math.Min(4, baseNumFloors - 1));
                var outlineStartFloorEst = 1 + rng.NextInt(Math.Max(1, baseNumFloors - outlineFloorsEst));
                estimatedY = baseGrid.GroundFloorHeight + outlineStartFloorEst * baseGrid.FloorHeight + (outlineFloorsEst * baseGrid.FloorHeight) / 2f;
                break;
            default: // Sign
                var signFloorEst = 1 + rng.NextInt(Math.Max(1, baseNumFloors - 1));
                estimatedY = baseGrid.GroundFloorHeight + signFloorEst * baseGrid.FloorHeight;
                break;
        }
        estimatedY = Math.Clamp(estimatedY, baseGrid.GroundFloorHeight + baseGrid.FloorHeight, building.Height - 2f);

        // Get section at this height for accurate dimensions
        var (sectionWidth, sectionDepth, _, _, sectionStartY, sectionEndY) = GetSectionAtHeight(building, estimatedY);
        var grid = building.GetGridAtHeight(estimatedY);

        var windowSpacing = grid.WindowSpacing;
        var floorHeight = grid.FloorHeight;
        var groundFloorHeight = grid.GroundFloorHeight;
        var facadeWidth = facade < 2 ? sectionWidth : sectionDepth;
        var numFloors = grid.NumFloors;
        var numWindowColumns = grid.GetColumnsForFacade(facade);

        // Skip if section is too narrow
        if (numWindowColumns < 1) return null;

        // Determine size and position based on shape
        float width, height, y;
        float horizontalOffset; // In world units, not normalized

        // Max offset to keep signs away from facade edges
        var maxOffset = facadeWidth / 2f - 1f;

        switch (shape)
        {
            case NeonShape.HorizontalBar:
                // Place on ledges (between floors), spanning window columns
                var barWindowSpan = 1 + rng.NextInt(Math.Min(3, numWindowColumns)); // Span 1-3 windows
                width = barWindowSpan * windowSpacing - NeonPlacementConstants.HorizontalBarWidthMargin;
                height = NeonPlacementConstants.HorizontalBarMinHeight +
                    (float)rng.NextDouble() * NeonPlacementConstants.HorizontalBarHeightRange;
                // Snap to floor boundaries (ledge positions)
                var barFloor = 1 + rng.NextInt(Math.Max(1, numFloors - 1));
                // Y is section-relative, add sectionStartY for world coords
                y = sectionStartY + groundFloorHeight + barFloor * floorHeight;
                // Align to window column center
                var barStartColumn = rng.NextInt(Math.Max(1, numWindowColumns - barWindowSpan + 1));
                var barCenterColumn = barStartColumn + (barWindowSpan - 1) / 2f;
                horizontalOffset = (barCenterColumn - (numWindowColumns - 1) / 2f) * windowSpacing;
                break;

            case NeonShape.VerticalStrip:
                // Place in gaps between window columns
                width = NeonPlacementConstants.VerticalStripMinWidth +
                    (float)rng.NextDouble() * NeonPlacementConstants.VerticalStripWidthRange;
                var stripFloors = 1 + rng.NextInt(Math.Min(4, numFloors - 1)); // Span 1-4 floors
                height = stripFloors * floorHeight - NeonPlacementConstants.VerticalStripHeightMargin;
                // Y position: start from a floor and span upward
                var stripStartFloor = 1 + rng.NextInt(Math.Max(1, numFloors - stripFloors));
                // Y is section-relative, add sectionStartY for world coords
                y = sectionStartY + groundFloorHeight + stripStartFloor * floorHeight + height / 2f;
                // Place in gap between windows (use grid to find gap positions)
                var gapIndex = rng.NextInt(numWindowColumns + 1);
                // Gap positions are at edges of window grid
                horizontalOffset = (gapIndex - numWindowColumns / 2f) * windowSpacing;
                break;

            case NeonShape.Outline:
                // Frame spanning 1-4 windows wide x 1-4 floors tall
                var outlineColumnsWide = 1 + rng.NextInt(Math.Min(4, numWindowColumns));
                var outlineFloorsHigh = 1 + rng.NextInt(Math.Min(4, numFloors - 1));

                // Width spans window columns (window + gaps between) plus gap on each side
                width = outlineColumnsWide * windowSpacing - (windowSpacing - windowWidth)
                    + NeonPlacementConstants.OutlineFrameMargin
                    + NeonPlacementConstants.OutlineWindowGap * 2f;
                // Height spans floors plus gap on top and bottom
                height = outlineFloorsHigh * floorHeight - (floorHeight - windowHeight)
                    + NeonPlacementConstants.OutlineFrameMargin
                    + NeonPlacementConstants.OutlineWindowGap * 2f;

                // Pick starting window that fits
                var maxStartColumn = Math.Max(0, numWindowColumns - outlineColumnsWide);
                var outlineStartColumn = rng.NextInt(maxStartColumn + 1);
                var outlineCenterColumn = outlineStartColumn + (outlineColumnsWide - 1) / 2f;

                // Pick starting floor that fits
                var maxStartFloor = Math.Max(1, numFloors - outlineFloorsHigh);
                var outlineStartFloor = 1 + rng.NextInt(maxStartFloor);

                // Y position at center of the spanned floors (section-relative + sectionStartY)
                y = sectionStartY + groundFloorHeight + outlineStartFloor * floorHeight + (outlineFloorsHigh * floorHeight) / 2f;

                // Horizontal offset based on column center
                horizontalOffset = (outlineCenterColumn - (numWindowColumns - 1) / 2f) * windowSpacing;
                break;

            case NeonShape.Sign:
            default:
                // Small sign placed near a window (slightly above or to the side)
                width = windowWidth * (NeonPlacementConstants.SignWidthBaseFactor +
                    (float)rng.NextDouble() * NeonPlacementConstants.SignWidthVariationFactor);
                height = NeonPlacementConstants.SignMinHeight +
                    (float)rng.NextDouble() * NeonPlacementConstants.SignHeightRange;
                // Position at top of a window or between floors
                var signFloor = 1 + rng.NextInt(Math.Max(1, numFloors - 1));
                // Y is section-relative, add sectionStartY for world coords
                y = sectionStartY + groundFloorHeight + signFloor * floorHeight +
                    floorHeight * NeonPlacementConstants.SignFloorPositionFactor;
                // Align to window column
                var signColumn = rng.NextInt(Math.Max(1, numWindowColumns));
                horizontalOffset = (signColumn - (numWindowColumns - 1) / 2f) * windowSpacing;
                break;
        }

        // Clamp Y to section bounds
        y = Math.Max(sectionStartY + groundFloorHeight + floorHeight, Math.Min(y, sectionEndY - 1f));

        // Clamp horizontal offset to keep sign on facade
        if (maxOffset < 0) return null; // Sign too wide for facade
        horizontalOffset = Math.Clamp(horizontalOffset, -maxOffset, maxOffset);

        // Reduce intensity for certain shapes
        intensity *= shape switch
        {
            NeonShape.Sign => NeonPlacementConstants.SignIntensityMultiplier,
            NeonShape.Outline => NeonPlacementConstants.OutlineIntensityMultiplier,
            NeonShape.HorizontalBar => NeonPlacementConstants.HorizontalBarIntensityMultiplier,
            NeonShape.VerticalStrip => NeonPlacementConstants.VerticalStripIntensityMultiplier,
            _ => 1f
        };

        // Get section-aware position with shape-specific standoff
        var facadeInset = _facadeConfig.FacadeInset;
        var standoff = shape == NeonShape.HorizontalBar
            ? facadeInset + NeonPlacementConstants.HorizontalBarExtraStandoff
            : facadeInset;
        var position = GetFacadePosition(building, facade, y, width, horizontalOffset, standoff);

        if (position == null)
        {
            return null; // Neon doesn't fit on this section's facade
        }

        var (x, z, rotationY) = position.Value;

        return new NeonLightData
        {
            X = x,
            Y = y,
            Z = z,
            Width = width,
            Height = height,
            Shape = shape,
            Color = color,
            Intensity = intensity,
            RotationY = rotationY
        };
    }
}
