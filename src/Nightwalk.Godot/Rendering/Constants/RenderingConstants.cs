using Godot;

namespace Nightwalk.Godot.Rendering.Constants;

/// <summary>
/// Centralized rendering constants extracted from ChunkRenderer.
/// Eliminates magic numbers and provides semantic meaning.
/// </summary>
public static class RenderingConstants
{
    #region Ground/Terrain

    /// <summary>Offset to prevent Z-fighting between ground and road surfaces.</summary>
    public const float GroundZFightingOffset = 0.002f;

    /// <summary>Height of the ground collision box (extends below Y=0).</summary>
    public const float GroundCollisionBoxHeight = 0.5f;

    /// <summary>Y offset for ground collision box center (negative = below surface).</summary>
    public const float GroundCollisionYOffset = 0.25f;

    #endregion

    #region Roads

    /// <summary>Height offset for road markings above road surface to prevent Z-fighting.</summary>
    public const float RoadMarkingHeightOffset = 0.015f;

    /// <summary>Minimum length for a road segment to be rendered.</summary>
    public const float MinRoadSegmentLength = 0.1f;

    /// <summary>Margin subtracted from road width for crosswalk stripe calculation.</summary>
    public const float CrosswalkRoadWidthMargin = 0.5f;

    /// <summary>Offset for positioning crosswalks from intersection center.</summary>
    public const float CrosswalkPositionOffset = 0.25f;

    /// <summary>Multiplier for center line width (makes lines more visible).</summary>
    public const float CenterLineWidthMultiplier = 1.5f;

    /// <summary>Grid spacing divisor for intersection seed calculation.</summary>
    public const float IntersectionGridSpacing = 48f;

    /// <summary>Multiplier for Z coordinate in intersection seed calculation.</summary>
    public const int IntersectionSeedZMultiplier = 10000;

    #endregion

    #region Buildings

    /// <summary>Depth of parapet walls on terrace ledges.</summary>
    public const float ParapetDepth = 0.3f;

    /// <summary>Margin from section/building top for ledge placement.</summary>
    public const float SectionEndMargin = 1f;

    /// <summary>Margin from building edge for window placement.</summary>
    public const int WindowEdgeMargin = 2;

    /// <summary>Minimum scale factor for rooftop features (as fraction of building dimension).</summary>
    public const float RooftopFeatureMinScale = 0.5f;

    /// <summary>Additional random scale range for rooftop features.</summary>
    public const float RooftopFeatureScaleRange = 0.5f;

    /// <summary>Minimum height for rooftop features.</summary>
    public const float RooftopFeatureMinHeight = 2f;

    /// <summary>Additional random height range for rooftop features.</summary>
    public const float RooftopFeatureHeightRange = 7f;

    #endregion

    #region Windows

    /// <summary>Mask for extracting building seed lower bits.</summary>
    public const int SeedMask = 0xFFFF;

    /// <summary>Normalization factor for building seed to 0-1 range.</summary>
    public const float SeedNormalizationFactor = 65535f;

    /// <summary>Mask for window index in UV encoding.</summary>
    public const int WindowIndexMask = 0xFF;

    /// <summary>Normalization factor for window index in UV2.y.</summary>
    public const float WindowIndexNormalizationFactor = 255f;

    /// <summary>XOR value for floor seed variation.</summary>
    public const int FloorSeedXorValue = 12345;

    /// <summary>Chance for lit window on mostly-dark floors.</summary>
    public const float MostlyDarkLitChance = 0.1f;

    /// <summary>Offset factor for window Y position within floor.</summary>
    public const float FloorHeightOffsetFactor = 0.5f;

    /// <summary>Minimum roughness for unlit window material.</summary>
    public const float UnlitWindowMinRoughness = 0.85f;

    /// <summary>Depth offset for highlight windows to prevent Z-fighting.</summary>
    public const float WindowHighlightDepthOffset = 0.02f;

    #endregion

    #region Neon Signs

    /// <summary>Depth for small neon shapes (bars, strips).</summary>
    public const float NeonDepthSmall = 0.1f;

    /// <summary>Depth for medium neon shapes (signs, outlines).</summary>
    public const float NeonDepthMedium = 0.15f;

    /// <summary>Depth for large neon shapes (large signs).</summary>
    public const float NeonDepthLarge = 0.25f;

    /// <summary>Thickness of neon outline frame bars.</summary>
    public const float NeonOutlineThickness = 0.15f;

    /// <summary>Width threshold for determining kanji column count.</summary>
    public const float KanjiColumnWidthThreshold = 2.5f;

    /// <summary>Frame width for billboard signs.</summary>
    public const float BillboardFrameWidth = 0.04f;

    /// <summary>Scroll speed for animated billboard content.</summary>
    public const float BillboardScrollSpeed = 0.15f;

    /// <summary>Seed offset multiplier between kanji sign layers.</summary>
    public const int KanjiLayerSeedOffset = 1000;

    /// <summary>Seed offset multiplier between billboard layers.</summary>
    public const int BillboardLayerSeedOffset = 500;

    /// <summary>Roughness for neon emissive materials.</summary>
    public const float NeonMaterialRoughness = 0.1f;

    #endregion

    #region Corner Strips

    /// <summary>Depth of corner strip neon meshes.</summary>
    public const float CornerStripDepth = 0.08f;

    /// <summary>Length multiplier for corner strip arms.</summary>
    public const float CornerStripLengthMultiplier = 3f;

    /// <summary>Position offset multiplier for corner strip placement.</summary>
    public const float CornerStripPositionMultiplier = 1.5f;

    #endregion

    #region Street Lamps

    /// <summary>Factor of sidewalk width for lamp placement from road edge.</summary>
    public const float LampSidewalkPlacementFactor = 0.3f;

    /// <summary>Multiplier for X coordinate in corner seed calculation.</summary>
    public const int CornerSeedXMultiplier = 100;

    /// <summary>Multiplier for Z coordinate in corner seed calculation.</summary>
    public const int CornerSeedZMultiplier = 10000;

    /// <summary>Top radius of tapered lamp pole.</summary>
    public const float LampPoleTopRadius = 0.08f;

    /// <summary>Bottom radius of tapered lamp pole.</summary>
    public const float LampPoleBottomRadius = 0.12f;

    /// <summary>Height offset for lamp meshes (clearance above ground).</summary>
    public const float LampHeightOffset = 0.5f;

    /// <summary>Y offset for top ring above pole.</summary>
    public const float LampTopRingYOffset = 0.05f;

    /// <summary>Inner radius of lamp top ring torus.</summary>
    public const float LampTopRingInnerRadius = 0.25f;

    /// <summary>Outer radius of lamp top ring torus.</summary>
    public const float LampTopRingOuterRadius = 0.6f;

    /// <summary>Number of rings in top ring torus mesh.</summary>
    public const int LampTopRingRings = 16;

    /// <summary>Number of segments in top ring torus mesh.</summary>
    public const int LampTopRingSegments = 32;

    /// <summary>Inner radius of accent ring torus.</summary>
    public const float LampAccentRingInnerRadius = 0.14f;

    /// <summary>Outer radius of accent ring torus.</summary>
    public const float LampAccentRingOuterRadius = 0.18f;

    /// <summary>Number of rings in accent ring torus mesh.</summary>
    public const int LampAccentRingRings = 12;

    /// <summary>Number of segments in accent ring torus mesh.</summary>
    public const int LampAccentRingSegments = 24;

    /// <summary>Height factors for accent ring placement along pole.</summary>
    public static readonly float[] LampAccentHeightFactors = { 0.25f, 0.5f, 0.75f };

    #endregion

    #region Lamp Materials

    /// <summary>Albedo color for lamp pole material (dark metallic gray).</summary>
    public static readonly Color LampPoleAlbedoColor = new(0.15f, 0.15f, 0.18f);

    /// <summary>Roughness for lamp pole material.</summary>
    public const float LampPoleMaterialRoughness = 0.7f;

    /// <summary>Metallic value for lamp pole material.</summary>
    public const float LampPoleMaterialMetallic = 0.6f;

    /// <summary>Emission energy multiplier for lamp ring material.</summary>
    public const float LampRingEmissionMultiplier = 2.0f;

    /// <summary>Roughness for lamp ring material.</summary>
    public const float LampRingMaterialRoughness = 0.2f;

    /// <summary>Metallic value for lamp ring material.</summary>
    public const float LampRingMaterialMetallic = 0.0f;

    /// <summary>Default frame color for billboard signs (dark blue-gray).</summary>
    public static readonly Color BillboardFrameColor = new(0.15f, 0.15f, 0.2f);

    #endregion

    #region Noise Textures

    /// <summary>Size (width/height) for procedural noise textures.</summary>
    public const int NoiseTextureSize = 256;

    /// <summary>Frequency for puddle noise generation.</summary>
    public const float PuddleNoiseFrequency = 0.02f;

    /// <summary>Number of fractal octaves for puddle noise.</summary>
    public const int PuddleNoiseOctaves = 4;

    /// <summary>Fractal lacunarity for puddle noise.</summary>
    public const float PuddleNoiseLacunarity = 2.0f;

    /// <summary>Fractal gain for puddle noise.</summary>
    public const float PuddleNoiseGain = 0.5f;

    /// <summary>Frequency for ripple normal noise generation.</summary>
    public const float RippleNoiseFrequency = 0.05f;

    /// <summary>Number of fractal octaves for ripple noise.</summary>
    public const int RippleNoiseOctaves = 2;

    /// <summary>Bump strength for ripple normal map generation.</summary>
    public const float RippleBumpStrength = 8.0f;

    #endregion

    #region Object Pooling

    /// <summary>Maximum number of Node3D objects to keep in pool.</summary>
    public const int NodePoolCapacity = 32;

    /// <summary>Maximum number of MeshInstance3D objects to keep in pool.</summary>
    public const int MeshInstancePoolCapacity = 128;

    #endregion
}
