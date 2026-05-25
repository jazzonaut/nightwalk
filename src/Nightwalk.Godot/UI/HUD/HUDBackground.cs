using Godot;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// Cyberpunk-style multi-edged polygon background for the HUD.
/// Complex angular design with tech details and neon accents.
/// </summary>
public sealed partial class HUDBackground : Control
{
    // Shape dimensions
    private const float Width = 210f;
    private const float Height = 160f;
    private const float LeftHeight = 185f;  // Taller on left side

    // Colors
    private static readonly Color FillColor = new(0.03f, 0.05f, 0.08f, 0.65f);
    private static readonly Color FillColorInner = new(0.02f, 0.03f, 0.05f, 0.35f);
    private static readonly Color BorderColor = new(0f, 0.85f, 0.85f, 0.8f);
    private static readonly Color GlowColor = new(0f, 0.6f, 0.6f, 0.3f);
    private static readonly Color AccentColor = new(0f, 0.5f, 0.5f, 0.4f);
    private static readonly Color DetailColor = new(0f, 0.7f, 0.7f, 0.2f);

    // Border
    private const float BorderWidth = 1.5f;
    private const float GlowWidth = 5f;
    private const float ThinLine = 1f;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Width, LeftHeight);
        Size = new Vector2(Width, LeftHeight);
        Position = new Vector2(-HUDTheme.EdgeMargin, -HUDTheme.EdgeMargin);
    }

    public override void _Draw()
    {
        // Complex angular shape with multiple notches and cuts
        //
        //  P0 ───── P1      P2 ── P3
        //  │         \    /       │
        //  │          \  /        P4  <- top notch out
        //  │           \/          \
        //  │                        \
        //  │                         P5
        //  │                        /
        //  │              P7 ───── P6  <- mid notch
        //  │             /
        //  │            /
        //  P10 ── P9   P8
        //
        Vector2[] mainPoly = new Vector2[]
        {
            new(0, 0),                          // Top-left corner
            new(Width - 15, 0),                 // Top-right
            new(Width, 12),                     // Right notch
            new(Width - 20, 40),                // Right angle 1
            new(Width - 45, 65),                // Right angle 2
            new(Width - 60, Height - 20),       // Right angle 3
            new(Width - 80, Height),            // Bottom-right (x=130)
            new(105, Height + 5),               // Jag down (subtle)
            new(85, Height),                    // Jag up
            new(65, Height + 8),                // Jag down (subtle)
            new(45, LeftHeight - 12),           // Jag up
            new(25, LeftHeight - 5),            // Jag down
            new(12, LeftHeight - 10),           // Jag up
            new(0, LeftHeight),                 // Bottom-left
        };

        // Draw main fill
        DrawPolygon(mainPoly, new Color[] { FillColor });

        // Tech details
        DrawTechDetails();

        // Corner accents
        DrawCornerAccents();
    }

    private void DrawTechDetails()
    {
        // Horizontal scan line
        DrawLine(new Vector2(10, 30), new Vector2(Width * 0.4f, 30), DetailColor, ThinLine);

        // Tick marks under scan line
        for (int i = 0; i < 4; i++)
        {
            float x = 15 + i * 12;
            DrawLine(new Vector2(x, 33), new Vector2(x + 6, 33), AccentColor, ThinLine);
        }

        // Diagonal tech line
        DrawLine(
            new Vector2(Width - 65, 40),
            new Vector2(Width - 90, Height - 30),
            DetailColor,
            ThinLine
        );

        // Small data blocks near bottom
        for (int i = 0; i < 3; i++)
        {
            float x = 35 + i * 18;
            DrawRect(new Rect2(x, Height - 25, 8, 3), AccentColor);
        }

        // Hexagon accent mark
        DrawHexMark(new Vector2(Width - 55, 60), 4f, DetailColor);
    }

    private void DrawCornerAccents()
    {
        // Top-left bracket
        var bracketColor = BorderColor with { A = 0.5f };
        DrawLine(new Vector2(5, 5), new Vector2(5, 20), bracketColor, ThinLine);
        DrawLine(new Vector2(5, 5), new Vector2(20, 5), bracketColor, ThinLine);
        DrawCircle(new Vector2(9, 9), 2f, AccentColor);

        // Small corner detail bottom-left
        DrawLine(new Vector2(5, Height - 20), new Vector2(5, Height - 12), bracketColor, ThinLine);
    }

    private void DrawHexMark(Vector2 center, float radius, Color color)
    {
        var points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Pi / 3f - Mathf.Pi / 6f;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        DrawPolyline(points, color, ThinLine, true);
    }

    private static Vector2[] InsetPolygon(Vector2[] poly, float amount)
    {
        Vector2 center = Vector2.Zero;
        foreach (var p in poly) center += p;
        center /= poly.Length;

        var result = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            var dir = (center - poly[i]).Normalized();
            result[i] = poly[i] + dir * amount;
        }
        return result;
    }
}
