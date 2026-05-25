using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Godot.Rendering.SubRenderers;

namespace Nightwalk.Godot.Buildings;

/// <summary>
/// Animation types for window silhouettes.
/// </summary>
public enum SilhouetteAnimation
{
    Static = 0,
    Fade = 1,
    Walk = 2,
    Drift = 3
}

/// <summary>
/// Silhouette types in the atlas (4x4 = 16 slots).
/// Organized by blur level (depth from window).
/// Cyberpunk/noir aesthetic - tech and industrial objects.
/// </summary>
public enum SilhouetteType
{
    // Window coverings - futuristic, flush with edges (blur mult 0.25-0.4)
    VerticalBlinds = 0,
    MotorizedShade = 1,
    TechCurtains = 2,
    MeshScreen = 3,

    // Cats - iconic noir (on windowsill, blur mult 0.6)
    CatSitting = 4,
    CatStanding = 5,
    CatLying = 6,

    // Tech/Industrial objects (blur mult 0.7-0.8)
    Duct = 7,
    Pipes = 8,
    Antenna = 9,

    // More tech/industrial objects (blur mult 0.5-1.2)
    SecurityCamera = 10,
    CableBundle = 11,
    Generator = 12,
    WallVent = 13,
    LargeVent = 14
}

/// <summary>
/// Returns the blur multiplier for a silhouette type based on perceived depth from window.
/// Lower values = sharper (closer to window), higher values = fuzzier (further back).
/// </summary>
public static class SilhouetteTypeExtensions
{
    public static float GetBlurMultiplier(this SilhouetteType type) => type switch
    {
        // Window coverings - futuristic, flush with edges
        SilhouetteType.VerticalBlinds => 0.25f,
        SilhouetteType.MotorizedShade => 0.25f,
        SilhouetteType.TechCurtains => 0.4f,
        SilhouetteType.MeshScreen => 0.3f,

        // Cats on windowsill - iconic noir
        SilhouetteType.CatSitting => 0.6f,
        SilhouetteType.CatStanding => 0.6f,
        SilhouetteType.CatLying => 0.6f,

        // Tech/Industrial objects
        SilhouetteType.Duct => 0.7f,
        SilhouetteType.Pipes => 0.7f,
        SilhouetteType.Antenna => 0.7f,

        // More tech/industrial objects
        SilhouetteType.SecurityCamera => 0.8f,
        SilhouetteType.CableBundle => 0.9f,
        SilhouetteType.Generator => 1.2f,
        SilhouetteType.WallVent => 0.5f,
        SilhouetteType.LargeVent => 0.6f,

        _ => 1.0f
    };
}

/// <summary>
/// Per-window silhouette state, generated deterministically from window coordinates.
/// </summary>
public readonly struct SilhouetteState
{
    public readonly bool HasSilhouette;
    public readonly SilhouetteType Type;
    public readonly SilhouetteAnimation Animation;
    public readonly float AnimationSeed;
    public readonly float AnimationPhase;

    private SilhouetteState(bool hasSilhouette, SilhouetteType type, SilhouetteAnimation animation,
        float animationSeed, float animationPhase)
    {
        HasSilhouette = hasSilhouette;
        Type = type;
        Animation = animation;
        AnimationSeed = animationSeed;
        AnimationPhase = animationPhase;
    }

    /// <summary>
    /// Generates deterministic silhouette state for a window.
    /// Only creates silhouettes on windows that would be lit.
    /// </summary>
    /// <param name="buildingSeed">Building seed from BuildingData.Seed</param>
    /// <param name="absoluteFloor">Absolute floor number (including ground floor offset)</param>
    /// <param name="windowIndex">Window index on this floor</param>
    /// <param name="density">Probability of a lit window having a silhouette (0-1)</param>
    /// <param name="windowConfig">Window config for lit percentage</param>
    public static SilhouetteState Generate(
        int buildingSeed,
        int absoluteFloor,
        int windowIndex,
        float density,
        WindowConfig windowConfig)
    {
        // First check if this window would be lit (reuses BuildingRenderer logic exactly)
        if (!BuildingRenderer.IsWindowLit(buildingSeed, absoluteFloor, windowIndex, windowConfig))
        {
            return new SilhouetteState(false, 0, 0, 0, 0);
        }

        // Determine if this lit window has a silhouette (use different hash purpose to avoid correlation)
        float presenceRoll = StableHash.ToFloat01(buildingSeed, absoluteFloor, windowIndex, 100);
        if (presenceRoll >= density)
        {
            return new SilhouetteState(false, 0, 0, 0, 0);
        }

        // Select silhouette type (15 options)
        int typeIndex = StableHash.ToInt(buildingSeed, absoluteFloor + 100, windowIndex, 15);
        var type = (SilhouetteType)typeIndex;

        // Determine animation based on silhouette type
        // Cats can drift (subtle movement), all other types are static
        SilhouetteAnimation animation;
        if (type >= SilhouetteType.CatSitting && type <= SilhouetteType.CatLying)
        {
            // Cats randomly drift or stay static
            float animRoll = StableHash.ToFloat01(buildingSeed, absoluteFloor + 200, windowIndex);
            animation = animRoll < 0.5f ? SilhouetteAnimation.Drift : SilhouetteAnimation.Static;
        }
        else
        {
            // All tech/industrial objects are static
            animation = SilhouetteAnimation.Static;
        }

        // Generate animation timing parameters
        float animSeed = StableHash.ToFloat01(buildingSeed, absoluteFloor + 300, windowIndex);
        float animPhase = StableHash.ToFloat01(buildingSeed, absoluteFloor + 400, windowIndex);

        return new SilhouetteState(true, type, animation, animSeed, animPhase);
    }

    /// <summary>
    /// Encodes state into UV coordinates for shader consumption.
    /// UV.x = atlas index (0-14 encoded as 0.0-1.0)
    /// UV.y = animation type (0, 0.33, 0.67, 1.0)
    /// </summary>
    public Vector2 EncodeUV()
    {
        return new Vector2(
            (int)Type / 14.0f,
            (int)Animation / 3.0f
        );
    }

    /// <summary>
    /// Encodes animation parameters into UV2 coordinates.
    /// UV2.x = animation seed
    /// UV2.y = animation phase
    /// </summary>
    public Vector2 EncodeUV2()
    {
        return new Vector2(AnimationSeed, AnimationPhase);
    }

    /// <summary>
    /// Gets the blur multiplier for this silhouette based on its type (depth from window).
    /// </summary>
    public float BlurMultiplier => Type.GetBlurMultiplier();

    /// <summary>
    /// Creates a color value with quad UV and blur multiplier for shader.
    /// COLOR.xy = quad UV (local 0-1 coordinates within the window)
    /// COLOR.z = blur multiplier (depth-based blur scaling)
    /// </summary>
    public Color EncodeColor(float quadU, float quadV)
    {
        return new Color(quadU, quadV, BlurMultiplier, 1.0f);
    }
}

/// <summary>
/// Generates the silhouette atlas texture programmatically.
/// </summary>
public static class SilhouetteAtlasGenerator
{
    private const int AtlasSize = 512;
    private const int CellSize = 128;
    private const int GridSize = 4;

    /// <summary>
    /// Creates a 4x4 atlas texture with simple geometric silhouettes.
    /// </summary>
    public static ImageTexture GenerateAtlas()
    {
        var image = Image.CreateEmpty(AtlasSize, AtlasSize, false, Image.Format.Rgba8);

        // Clear to transparent
        image.Fill(new Color(0, 0, 0, 0));

        // Draw each silhouette in its cell (15 types, cell 15 left empty)
        for (int i = 0; i < 15; i++)
        {
            int cellX = i % GridSize;
            int cellY = i / GridSize;
            int baseX = cellX * CellSize;
            int baseY = cellY * CellSize;

            DrawSilhouette(image, baseX, baseY, (SilhouetteType)i);
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static void DrawSilhouette(Image image, int baseX, int baseY, SilhouetteType type)
    {
        // Center of cell
        int cx = baseX + CellSize / 2;

        switch (type)
        {
            // Window coverings (futuristic - flush with edges)
            case SilhouetteType.VerticalBlinds:
                DrawVerticalBlinds(image, baseX, baseY);
                break;
            case SilhouetteType.MotorizedShade:
                DrawMotorizedShade(image, baseX, baseY);
                break;
            case SilhouetteType.TechCurtains:
                DrawTechCurtains(image, baseX, baseY);
                break;
            case SilhouetteType.MeshScreen:
                DrawMeshScreen(image, baseX, baseY);
                break;

            // Cats (iconic noir - on windowsill)
            case SilhouetteType.CatSitting:
                DrawCatSitting(image, cx, baseY);
                break;
            case SilhouetteType.CatStanding:
                DrawCatStanding(image, cx, baseY);
                break;
            case SilhouetteType.CatLying:
                DrawCatLying(image, cx, baseY);
                break;

            // Tech/Industrial objects
            case SilhouetteType.Duct:
                DrawDuct(image, baseX, baseY);
                break;
            case SilhouetteType.Pipes:
                DrawPipes(image, baseX, baseY);
                break;
            case SilhouetteType.Antenna:
                DrawAntenna(image, cx, baseY);
                break;

            // More tech/industrial objects
            case SilhouetteType.SecurityCamera:
                DrawSecurityCamera(image, cx, baseY);
                break;
            case SilhouetteType.CableBundle:
                DrawCableBundle(image, baseX, baseY);
                break;
            case SilhouetteType.Generator:
                DrawGenerator(image, cx, baseY);
                break;
            case SilhouetteType.WallVent:
                DrawWallVent(image, cx, baseY);
                break;
            case SilhouetteType.LargeVent:
                DrawLargeVent(image, baseX, baseY);
                break;
        }
    }

    // ========================================
    // Window Coverings (futuristic - flush with edges)
    // ========================================

    private static void DrawVerticalBlinds(Image image, int baseX, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        // Sharp vertical slats - modern office look
        int slats = 12;
        int slatWidth = 6;
        int spacing = CellSize / slats;

        for (int i = 0; i < slats; i++)
        {
            int x = baseX + i * spacing + spacing / 2 - slatWidth / 2;
            FillRect(image, x, baseY, slatWidth, CellSize, color);
        }
    }

    private static void DrawMotorizedShade(Image image, int baseX, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        // Segmented roller shade, partially open (covers top 60%)
        int segments = 6;
        int coveredHeight = (int)(CellSize * 0.6f);
        int segmentHeight = coveredHeight / segments;

        // Main shade body
        FillRect(image, baseX, baseY, CellSize, coveredHeight - 4, color);

        // Segment lines (subtle horizontal gaps)
        var transparent = new Color(0, 0, 0, 0);
        for (int i = 1; i < segments; i++)
        {
            int y = baseY + i * segmentHeight;
            FillRect(image, baseX, y, CellSize, 2, transparent);
        }

        // Bottom bar
        FillRect(image, baseX, baseY + coveredHeight - 6, CellSize, 6, color);
    }

    private static void DrawTechCurtains(Image image, int baseX, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        // Angular geometric drapes - sharp triangular folds

        // Left curtain - angular triangular shape
        FillTriangle(image, baseX, baseY, baseX + 45, baseY, baseX, baseY + CellSize, color);
        FillTriangle(image, baseX + 45, baseY, baseX + 30, baseY + CellSize / 2, baseX, baseY + CellSize, color);

        // Right curtain - angular triangular shape
        FillTriangle(image, baseX + CellSize, baseY, baseX + CellSize - 45, baseY, baseX + CellSize, baseY + CellSize, color);
        FillTriangle(image, baseX + CellSize - 45, baseY, baseX + CellSize - 30, baseY + CellSize / 2, baseX + CellSize, baseY + CellSize, color);

        // Top rail - flush with top
        FillRect(image, baseX, baseY, CellSize, 8, color);
    }

    private static void DrawMeshScreen(Image image, int baseX, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        // Fine grid pattern - industrial mesh
        int gridSize = 8;

        for (int y = 0; y < CellSize; y += gridSize)
        {
            for (int x = 0; x < CellSize; x += gridSize)
            {
                // Draw small squares at grid intersections
                FillRect(image, baseX + x, baseY + y, 3, 3, color);
            }
        }
    }

    // ========================================
    // Cats (on windowsill - bottom of window)
    // ========================================

    private static void DrawCatSitting(Image image, int cx, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        // Cat sitting on windowsill - positioned at bottom
        int bottom = baseY + CellSize;
        FillEllipse(image, cx, bottom - 28, 22, 28, color);  // Body at bottom
        FillEllipse(image, cx, bottom - 60, 16, 16, color);  // Head above body
        // Ear bumps
        FillEllipse(image, cx - 14, bottom - 72, 6, 8, color);
        FillEllipse(image, cx + 14, bottom - 72, 6, 8, color);
    }

    private static void DrawCatStanding(Image image, int cx, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        // Cat standing on windowsill - positioned at bottom
        int bottom = baseY + CellSize;
        FillEllipse(image, cx + 5, bottom - 22, 35, 18, color);  // Horizontal body
        FillEllipse(image, cx - 28, bottom - 30, 14, 14, color); // Head
        // Ear bumps
        FillEllipse(image, cx - 36, bottom - 42, 5, 7, color);
        FillEllipse(image, cx - 22, bottom - 42, 5, 7, color);
        // Tail up
        FillEllipse(image, cx + 38, bottom - 38, 6, 20, color);
    }

    private static void DrawCatLying(Image image, int cx, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        // Cat loafing on windowsill - positioned at bottom
        int bottom = baseY + CellSize;
        FillEllipse(image, cx, bottom - 18, 38, 18, color);     // Wide low body
        FillEllipse(image, cx - 30, bottom - 26, 14, 12, color); // Head slightly raised
        // Ear bumps
        FillEllipse(image, cx - 38, bottom - 36, 5, 6, color);
        FillEllipse(image, cx - 24, bottom - 36, 5, 6, color);
    }

    // ========================================
    // Tech/Industrial Objects (on windowsill)
    // ========================================

    private static void DrawDuct(Image image, int baseX, int baseY)
    {
        var color = new Color(1, 1, 1, 1);

        // Simple horizontal duct running across window
        FillRect(image, baseX, baseY + 45, CellSize, 28, color);

        // Mounting brackets
        FillRect(image, baseX + 20, baseY + 40, 8, 5, color);
        FillRect(image, baseX + 100, baseY + 40, 8, 5, color);
    }

    private static void DrawPipes(Image image, int baseX, int baseY)
    {
        var color = new Color(1, 1, 1, 1);

        // Horizontal pipes running across window
        FillRect(image, baseX, baseY + 15, CellSize, 12, color);
        FillRect(image, baseX, baseY + 50, CellSize, 8, color);
        FillRect(image, baseX, baseY + 85, CellSize, 14, color);

        // Vertical connecting pipe on left
        FillRect(image, baseX + 20, baseY + 15, 10, 84, color);

        // Valve/junction on middle pipe
        FillEllipse(image, baseX + 80, baseY + 54, 12, 12, color);

        // Small elbow joint
        FillRect(image, baseX + 95, baseY + 85, 20, 14, color);
        FillRect(image, baseX + 105, baseY + 70, 10, 20, color);
    }

    private static void DrawAntenna(Image image, int cx, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        int bottom = baseY + CellSize;

        // Satellite dish - parabolic shape
        FillEllipse(image, cx - 5, bottom - 55, 35, 25, color);

        // Clear inner part to make it dish-shaped
        var transparent = new Color(0, 0, 0, 0);
        FillEllipse(image, cx - 8, bottom - 52, 25, 18, transparent);

        // Feed arm (diagonal line to center)
        FillRect(image, cx + 5, bottom - 70, 4, 25, color);
        // Feed horn at end
        FillEllipse(image, cx + 7, bottom - 73, 6, 6, color);

        // Mounting pole
        FillRect(image, cx - 3, bottom - 35, 6, 35, color);

        // Base
        FillRect(image, cx - 12, bottom - 8, 24, 8, color);
    }

    // ========================================
    // More Tech/Industrial Objects
    // ========================================

    private static void DrawSecurityCamera(Image image, int cx, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        int bottom = baseY + CellSize;

        // Mounting bracket on ceiling/top
        FillRect(image, cx - 4, baseY, 8, 20, color);

        // Camera housing (angled box)
        FillRect(image, cx - 18, baseY + 18, 36, 22, color);

        // Lens (cylinder shape)
        FillEllipse(image, cx + 20, baseY + 29, 12, 10, color);

        // Status light
        FillRect(image, cx - 14, baseY + 22, 4, 4, color);
    }

    private static void DrawCableBundle(Image image, int baseX, int baseY)
    {
        var color = new Color(1, 1, 1, 1);

        // Vertical cable runs - industrial conduits
        // Left cable bundle
        FillRect(image, baseX + 8, baseY, 10, CellSize, color);
        FillRect(image, baseX + 22, baseY, 6, CellSize, color);

        // Right cable bundle
        FillRect(image, baseX + CellSize - 18, baseY, 10, CellSize, color);
        FillRect(image, baseX + CellSize - 28, baseY, 6, CellSize, color);

        // Horizontal junction boxes
        FillRect(image, baseX + 5, baseY + 30, 35, 12, color);
        FillRect(image, baseX + CellSize - 40, baseY + 70, 35, 12, color);
    }

    private static void DrawGenerator(Image image, int cx, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        int bottom = baseY + CellSize;

        // Main generator body
        FillRect(image, cx - 28, bottom - 65, 56, 55, color);

        // Top housing/motor
        FillRect(image, cx - 20, bottom - 80, 40, 18, color);

        // Exhaust pipe on side
        FillRect(image, cx + 25, bottom - 90, 8, 30, color);

        // Control panel (cutout)
        var transparent = new Color(0, 0, 0, 0);
        FillRect(image, cx - 22, bottom - 55, 18, 25, transparent);

        // Vents on side
        FillRect(image, cx + 5, bottom - 50, 18, 3, transparent);
        FillRect(image, cx + 5, bottom - 42, 18, 3, transparent);
        FillRect(image, cx + 5, bottom - 34, 18, 3, transparent);

        // Base/feet
        FillRect(image, cx - 32, bottom - 10, 64, 10, color);
    }

    private static void DrawWallVent(Image image, int cx, int baseY)
    {
        var color = new Color(1, 1, 1, 1);
        int ventY = baseY + CellSize / 2;

        // Vent frame
        FillRect(image, cx - 35, ventY - 25, 70, 50, color);

        // Horizontal slats
        var transparent = new Color(0, 0, 0, 0);
        for (int i = 0; i < 5; i++)
        {
            int y = ventY - 20 + i * 10;
            FillRect(image, cx - 30, y, 60, 5, transparent);
        }
    }

    private static void DrawLargeVent(Image image, int baseX, int baseY)
    {
        var color = new Color(1, 1, 1, 1);

        // Large industrial vent covering most of window
        FillRect(image, baseX + 8, baseY + 8, CellSize - 16, CellSize - 16, color);

        // Grid pattern
        var transparent = new Color(0, 0, 0, 0);
        // Horizontal bars
        for (int i = 1; i < 6; i++)
        {
            int y = baseY + 8 + i * 18;
            FillRect(image, baseX + 12, y, CellSize - 24, 6, transparent);
        }
        // Vertical bars
        for (int i = 1; i < 6; i++)
        {
            int x = baseX + 8 + i * 18;
            FillRect(image, x, baseY + 12, 6, CellSize - 24, transparent);
        }
    }

    // Drawing helpers
    private static void FillRect(Image image, int x, int y, int width, int height, Color color)
    {
        for (int py = y; py < y + height && py < AtlasSize; py++)
        {
            for (int px = x; px < x + width && px < AtlasSize; px++)
            {
                if (px >= 0 && py >= 0)
                {
                    image.SetPixel(px, py, color);
                }
            }
        }
    }

    private static void FillEllipse(Image image, int cx, int cy, int rx, int ry, Color color)
    {
        for (int py = cy - ry; py <= cy + ry; py++)
        {
            for (int px = cx - rx; px <= cx + rx; px++)
            {
                float dx = (px - cx) / (float)rx;
                float dy = (py - cy) / (float)ry;
                if (dx * dx + dy * dy <= 1.0f && px >= 0 && py >= 0 && px < AtlasSize && py < AtlasSize)
                {
                    image.SetPixel(px, py, color);
                }
            }
        }
    }

    private static void FillTriangle(Image image, int x1, int y1, int x2, int y2, int x3, int y3, Color color)
    {
        // Simple scanline triangle fill
        int minY = Mathf.Min(y1, Mathf.Min(y2, y3));
        int maxY = Mathf.Max(y1, Mathf.Max(y2, y3));
        int minX = Mathf.Min(x1, Mathf.Min(x2, x3));
        int maxX = Mathf.Max(x1, Mathf.Max(x2, x3));

        for (int py = minY; py <= maxY && py < AtlasSize; py++)
        {
            for (int px = minX; px <= maxX && px < AtlasSize; px++)
            {
                if (px >= 0 && py >= 0 && PointInTriangle(px, py, x1, y1, x2, y2, x3, y3))
                {
                    image.SetPixel(px, py, color);
                }
            }
        }
    }

    private static bool PointInTriangle(int px, int py, int x1, int y1, int x2, int y2, int x3, int y3)
    {
        float d1 = Sign(px, py, x1, y1, x2, y2);
        float d2 = Sign(px, py, x2, y2, x3, y3);
        float d3 = Sign(px, py, x3, y3, x1, y1);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    private static float Sign(int px, int py, int x1, int y1, int x2, int y2)
    {
        return (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
    }
}
