using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Godot.Rendering.Geometry;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Generates facade detail geometry (AC units, pipes).
/// Called by BuildingRenderer during building geometry generation.
/// </summary>
public sealed class FacadeDetailGenerator
{
    // Hash purpose constants for deterministic placement
    private const int PurposeAcUnit = 100;
    private const int PurposeAcUnitFace = 101;
    private const int PurposeAcUnitFloor = 102;
    private const int PurposeAcUnitHPos = 103;
    private const int PurposePipe = 110;
    private const int PurposePipeFace = 111;
    private const int PurposePipeRadius = 112;
    private readonly FacadeDetailConfig _config;
    private readonly float _floorHeight;

    public FacadeDetailGenerator(FacadeDetailConfig config, float floorHeight)
    {
        _config = config;
        _floorHeight = floorHeight;
    }

    /// <summary>
    /// Adds facade detail geometry (AC units, pipes) to the provided MeshBuilder.
    /// </summary>
    public void AddFacadeDetails(
        MeshBuilder mbDetails,
        BuildingData building,
        float localX,
        float localZ)
    {
        if (!_config.Enabled) return;

        if (_config.AcUnitsEnabled)
        {
            AddAcUnits(mbDetails, building, localX, localZ);
        }

        if (_config.PipesEnabled)
        {
            AddPipes(mbDetails, building, localX, localZ);
        }
    }

    private void AddAcUnits(MeshBuilder mb, BuildingData building, float localX, float localZ)
    {
        // For stepped buildings, only place AC units on the base section
        float maxHeight;
        float acWidth, acDepth;
        if (building.IsStepped && building.Sections != null)
        {
            var baseSection = building.Sections[0];
            maxHeight = baseSection.EndY;
            acWidth = baseSection.Width;
            acDepth = baseSection.Depth;
        }
        else
        {
            maxHeight = building.Height;
            acWidth = building.Width;
            acDepth = building.Depth;
        }

        var numPotentialUnits = (int)(maxHeight / 15f);
        if (numPotentialUnits < 1) return;

        for (int i = 0; i < numPotentialUnits; i++)
        {
            var roll = StableHash.ToFloat01(building.Seed, i, PurposeAcUnit);
            if (roll > _config.AcUnitChance) continue;

            // Determine which face and floor
            var faceIndex = StableHash.ToInt(building.Seed, i, PurposeAcUnitFace, 4);
            var floor = _config.AcUnitMinFloor + StableHash.ToInt(building.Seed, i, PurposeAcUnitFloor, numPotentialUnits);
            var y = floor * _floorHeight + _floorHeight * 0.3f;

            if (y >= maxHeight - 3f) continue;

            // Horizontal position along the facade
            var hPos = StableHash.ToFloat01(building.Seed, i, PurposeAcUnitHPos);

            var (acX, acZ) = GetFacePosition(
                localX, localZ, acWidth, acDepth,
                faceIndex, _config.AcUnitDepth / 2f, hPos);

            // Swap width and depth for side faces so AC unit protrudes correctly from wall
            var isSideFace = faceIndex >= 2;
            var boxWidth = isSideFace ? _config.AcUnitDepth : _config.AcUnitWidth;
            var boxDepth = isSideFace ? _config.AcUnitWidth : _config.AcUnitDepth;

            GeometryBuilder.AddBox(mb, acX, y, acZ,
                boxWidth, _config.AcUnitHeight, boxDepth);
        }
    }

    private void AddPipes(MeshBuilder mb, BuildingData building, float localX, float localZ)
    {
        // For stepped buildings, only use the first (base) section dimensions
        float sectionHeight;
        float pipeWidth, pipeDepth;
        if (building.IsStepped && building.Sections != null)
        {
            var baseSection = building.Sections[0];
            sectionHeight = baseSection.EndY;
            pipeWidth = baseSection.Width;
            pipeDepth = baseSection.Depth;
        }
        else
        {
            sectionHeight = building.Height;
            pipeWidth = building.Width;
            pipeDepth = building.Depth;
        }

        // Only add pipes to shorter buildings (under max floors)
        var numFloors = (int)(sectionHeight / _floorHeight);
        if (numFloors > _config.PipeMaxFloors) return;

        // 1-3 pipes per building, placed at corners where there are no windows
        var numPipes = 1 + StableHash.ToInt(building.Seed, PurposePipe, 3);

        for (int i = 0; i < numPipes; i++)
        {
            var roll = StableHash.ToFloat01(building.Seed, i, PurposePipe);
            if (roll > _config.PipeChance) continue;

            var radiusRange = _config.PipeRadiusMax - _config.PipeRadiusMin;
            var pipeRadius = _config.PipeRadiusMin + StableHash.ToFloat01(building.Seed, i, PurposePipeRadius) * radiusRange;

            // Pipe goes from ground to roof (full height)
            var pipeHeight = sectionHeight;

            // Place pipes at building corners - pick one of 4 corners
            var cornerIndex = StableHash.ToInt(building.Seed, i, PurposePipeFace, 4);
            var (pipeX, pipeZ) = GetCornerPosition(localX, localZ, pipeWidth, pipeDepth, cornerIndex, pipeRadius);

            // Vertical pipe as tall thin box, centered at half height
            GeometryBuilder.AddBox(mb, pipeX, pipeHeight / 2f, pipeZ,
                pipeRadius * 2f, pipeHeight, pipeRadius * 2f);
        }
    }

    /// <summary>
    /// Gets position at a building corner (where windows never are).
    /// Pipe is placed on the outside of the wall, near the corner.
    /// </summary>
    private static (float x, float z) GetCornerPosition(float cx, float cz, float width, float depth, int cornerIndex, float pipeRadius)
    {
        var halfW = width / 2f;
        var halfD = depth / 2f;
        var cornerInset = 0.8f; // How far from actual corner along the wall

        return cornerIndex switch
        {
            // Front face (+Z), near left corner
            0 => (cx - halfW + cornerInset, cz + halfD + pipeRadius),
            // Front face (+Z), near right corner
            1 => (cx + halfW - cornerInset, cz + halfD + pipeRadius),
            // Right face (+X), near back corner
            2 => (cx + halfW + pipeRadius, cz - halfD + cornerInset),
            // Left face (-X), near back corner
            _ => (cx - halfW - pipeRadius, cz - halfD + cornerInset)
        };
    }

    /// <summary>
    /// Gets position on a building face.
    /// </summary>
    /// <param name="cx">Building center X</param>
    /// <param name="cz">Building center Z</param>
    /// <param name="width">Building width</param>
    /// <param name="depth">Building depth</param>
    /// <param name="faceIndex">0=Front(+Z), 1=Back(-Z), 2=Right(+X), 3=Left(-X)</param>
    /// <param name="offset">Distance from wall surface (positive = outward)</param>
    /// <param name="hPos">Horizontal position along face (0-1)</param>
    private static (float x, float z) GetFacePosition(
        float cx, float cz, float width, float depth,
        int faceIndex, float offset, float hPos)
    {
        // Convert hPos from 0-1 to actual position along face (with margins)
        var margin = 1.5f;

        return faceIndex switch
        {
            0 => (cx + (hPos - 0.5f) * (width - margin * 2), cz + depth / 2f + offset), // Front (+Z)
            1 => (cx + (hPos - 0.5f) * (width - margin * 2), cz - depth / 2f - offset), // Back (-Z)
            2 => (cx + width / 2f + offset, cz + (hPos - 0.5f) * (depth - margin * 2)), // Right (+X)
            _ => (cx - width / 2f - offset, cz + (hPos - 0.5f) * (depth - margin * 2))  // Left (-X)
        };
    }
}
