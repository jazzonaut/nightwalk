using Godot;
using Nightwalk.Godot.Rendering.Materials;
using Nightwalk.Godot.Rendering.Pooling;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Shared context passed to all sub-renderers during chunk rendering.
/// Contains materials, utilities, and chunk position information.
/// </summary>
public sealed class ChunkRenderContext
{
    /// <summary>
    /// World X coordinate of the chunk's origin.
    /// </summary>
    public required float ChunkWorldX { get; init; }

    /// <summary>
    /// World Z coordinate of the chunk's origin.
    /// </summary>
    public required float ChunkWorldZ { get; init; }

    /// <summary>
    /// Size of the chunk in world units.
    /// </summary>
    public required float ChunkSize { get; init; }

    /// <summary>
    /// Camera position at render time, used for LOD calculations.
    /// </summary>
    public Vector3 CameraPosition { get; init; }

    /// <summary>
    /// Object pool for reusing nodes and mesh instances.
    /// </summary>
    public required NodePool NodePool { get; init; }

    /// <summary>
    /// Factory for creating and caching materials.
    /// </summary>
    public required MaterialFactory MaterialFactory { get; init; }

    /// <summary>
    /// Pre-created ground material.
    /// </summary>
    public required StandardMaterial3D GroundMaterial { get; init; }

    /// <summary>
    /// Pre-created building material variants (StandardMaterial3D or ShaderMaterial for weathering).
    /// </summary>
    public required Material[] BuildingMaterials { get; init; }

    /// <summary>
    /// Pre-created unlit window material.
    /// </summary>
    public required StandardMaterial3D WindowUnlitMaterial { get; init; }

    /// <summary>
    /// Pre-created lit window shader material.
    /// </summary>
    public required ShaderMaterial WindowLitMaterial { get; init; }

    /// <summary>
    /// Pre-created ledge material.
    /// </summary>
    public required StandardMaterial3D LedgeMaterial { get; init; }

    /// <summary>
    /// Pre-created wet road shader material.
    /// </summary>
    public required ShaderMaterial WetRoadMaterial { get; init; }

    /// <summary>
    /// Pre-created wet sidewalk shader material.
    /// </summary>
    public required ShaderMaterial WetSidewalkMaterial { get; init; }

    /// <summary>
    /// Pre-created road marking material.
    /// </summary>
    public required StandardMaterial3D RoadMarkingMaterial { get; init; }

    /// <summary>
    /// Pre-created lamp pole material.
    /// </summary>
    public required StandardMaterial3D LampPoleMaterial { get; init; }

    /// <summary>
    /// Pre-created lamp ring material.
    /// </summary>
    public required StandardMaterial3D LampRingMaterial { get; init; }

    /// <summary>
    /// Pre-created facade detail material (for AC units, pipes, etc.).
    /// Null if facade details are disabled.
    /// </summary>
    public StandardMaterial3D? FacadeDetailMaterial { get; init; }
}
