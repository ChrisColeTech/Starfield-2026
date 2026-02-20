using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.UI;

/// <summary>
/// Shared rendering style matching the old engine's Window decorations
/// and FontColors definitions.
/// </summary>
public static class UIStyle
{
    // Battle decoration border gradient (top-to-bottom: black → dark gray → gray)
    private static readonly Color BorderLine1 = new(0, 0, 0, 200);
    private static readonly Color BorderLine2 = new(30, 30, 30, 200);
    private static readonly Color BorderLine3 = new(60, 60, 60, 200);
    private static readonly Color BorderInnerEdge = Color.White;
    private static readonly Color BattleFill = new(80, 80, 80, 200);

    // Text colors — from old engine FontColors.cs (3-channel: transparent, main, outline)
    // DefaultWhite_I
    public static readonly Color TextNormal = new(239, 239, 239);
    public static readonly Color TextNormalOutline = new(132, 132, 132);
    // DefaultYellow_O
    public static readonly Color TextSelected = new(255, 224, 22);
    public static readonly Color TextSelectedOutline = new(188, 165, 16);
    // DefaultDisabled
    public static readonly Color TextDisabled = new(133, 133, 141);
    public static readonly Color TextDisabledOutline = new(58, 50, 50);
    // DefaultDarkGray_I (used for standard message text)
    public static readonly Color TextDarkGray = new(90, 82, 82);
    public static readonly Color TextDarkGrayOutline = new(165, 165, 173);
    // Prompt color
    public static readonly Color TextPrompt = new(160, 160, 180);

    // Menu selection highlight
    public static readonly Color SelectionHighlight = new(100, 100, 120, 100);

    // Standard (overworld) panel colors — from old engine's GrayRounded decoration
    private static readonly Color StandardBorder = new(80, 80, 80);
    private static readonly Color StandardFill = Color.White;

    /// <summary>
    /// Draw a battle-style panel matching the old engine's Window.Decoration.Battle.
    /// Gradient border: 1px black → 1px dark gray → 1px gray, then 2px white inner edge,
    /// with semi-transparent dark fill.
    /// </summary>
    public static void DrawBattlePanel(SpriteBatch sb, Texture2D pixel, Rectangle r)
    {
        // Top gradient border (3 lines)
        sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), BorderLine1);
        sb.Draw(pixel, new Rectangle(r.X, r.Y + 1, r.Width, 1), BorderLine2);
        sb.Draw(pixel, new Rectangle(r.X, r.Y + 2, r.Width, 1), BorderLine3);

        // Bottom gradient border (reversed: gray → dark gray → black)
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 3, r.Width, 1), BorderLine3);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 2, r.Width, 1), BorderLine2);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), BorderLine1);

        // Left gradient border (3 lines)
        sb.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), BorderLine1);
        sb.Draw(pixel, new Rectangle(r.X + 1, r.Y, 1, r.Height), BorderLine2);
        sb.Draw(pixel, new Rectangle(r.X + 2, r.Y, 1, r.Height), BorderLine3);

        // Right gradient border (reversed)
        sb.Draw(pixel, new Rectangle(r.Right - 3, r.Y, 1, r.Height), BorderLine3);
        sb.Draw(pixel, new Rectangle(r.Right - 2, r.Y, 1, r.Height), BorderLine2);
        sb.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), BorderLine1);

        // White inner edge (2px all sides, inside the gradient)
        var inner = new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
        sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 2), BorderInnerEdge);          // top
        sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), BorderInnerEdge); // bottom
        sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 2, inner.Height), BorderInnerEdge);         // left
        sb.Draw(pixel, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), BorderInnerEdge); // right

        // Fill area inside white edges
        var fill = new Rectangle(inner.X + 2, inner.Y + 2, inner.Width - 4, inner.Height - 4);
        sb.Draw(pixel, fill, BattleFill);
    }

    /// <summary>
    /// Draw an overworld-style panel matching the old engine's GrayRounded decoration.
    /// 2px gray border, white fill. No rounded corners (approximation).
    /// </summary>
    public static void DrawStandardPanel(SpriteBatch sb, Texture2D pixel, Rectangle r)
    {
        // 2px gray border
        sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 2), StandardBorder);              // top
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), StandardBorder);     // bottom
        sb.Draw(pixel, new Rectangle(r.X, r.Y, 2, r.Height), StandardBorder);             // left
        sb.Draw(pixel, new Rectangle(r.Right - 2, r.Y, 2, r.Height), StandardBorder);     // right

        // White fill
        var fill = new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
        sb.Draw(pixel, fill, StandardFill);
    }

    /// <summary>
    /// Draw text with a 1px outline offset for readability (approximates the engine's
    /// multi-color font rendering with SpriteFont).
    /// </summary>
    public static void DrawShadowedText(SpriteBatch sb, SpriteFont font, string text,
        Vector2 position, Color color, Color outline)
    {
        sb.DrawString(font, text, position + new Vector2(1, 1), outline);
        sb.DrawString(font, text, position, color);
    }

    /// <summary>
    /// Draw a small right-pointing triangle (menu cursor). Size is in pixels.
    /// </summary>
    public static void DrawArrowRight(SpriteBatch sb, Texture2D pixel, int x, int y, int size, Color color)
    {
        for (int row = 0; row < size; row++)
        {
            int halfSize = size / 2;
            int width = row <= halfSize ? row + 1 : size - row;
            sb.Draw(pixel, new Rectangle(x, y + row, width, 1), color);
        }
    }

    /// <summary>
    /// Draw a small downward-pointing triangle (advance prompt). Size is in pixels.
    /// </summary>
    public static void DrawArrowDown(SpriteBatch sb, Texture2D pixel, int x, int y, int size, Color color)
    {
        for (int row = 0; row < size; row++)
        {
            int width = size - row * 2;
            if (width <= 0) break;
            int offsetX = row;
            sb.Draw(pixel, new Rectangle(x + offsetX, y + row, width, 1), color);
        }
    }

    /// <summary>
    /// Draw a vertical 3-color gradient using horizontal 1px bands.
    /// Blends top→mid in upper half, mid→bottom in lower half.
    /// </summary>
    public static void DrawTripleGradient(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, Color top, Color mid, Color bottom)
    {
        int halfH = bounds.Height / 2;
        for (int y = 0; y < bounds.Height; y++)
        {
            Color c = y < halfH
                ? Color.Lerp(top, mid, (float)y / halfH)
                : Color.Lerp(mid, bottom, (float)(y - halfH) / (bounds.Height - halfH));
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y + y, bounds.Width, 1), c);
        }
    }

    /// <summary>
    /// Draw an HP bar: dark background with colored fill.
    /// Green > 50%, yellow > 20%, red otherwise.
    /// </summary>
    public static void DrawHPBar(SpriteBatch sb, Texture2D pixel, Rectangle bounds, float hpPercent)
    {
        // Background
        sb.Draw(pixel, bounds, new Color(40, 40, 40));

        // Fill
        int fillW = (int)(bounds.Width * MathHelper.Clamp(hpPercent, 0f, 1f));
        if (fillW > 0)
        {
            Color barColor = hpPercent > 0.5f ? new Color(80, 200, 80)
                           : hpPercent > 0.2f ? new Color(220, 200, 40)
                           : new Color(220, 60, 60);
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, fillW, bounds.Height), barColor);
        }
    }

    // Triple-line HP bar colors (from old engine RenderUtils.cs)
    private static readonly Color HPGreenSides = new(0, 140, 41);
    private static readonly Color HPGreenMid = new(0, 255, 74);
    private static readonly Color HPYellowSides = new(156, 99, 16);
    private static readonly Color HPYellowMid = new(247, 181, 0);
    private static readonly Color HPRedSides = new(148, 33, 49);
    private static readonly Color HPRedMid = new(255, 49, 66);
    private static readonly Color HPBorder = new(49, 49, 49);
    private static readonly Color EXPFill = new(0, 160, 255);

    /// <summary>
    /// Draw a triple-stripe HP bar matching the old engine.
    /// 5px tall at scale 1: 1px border, 3 inner stripes (sides/mid/sides).
    /// </summary>
    public static void DrawTripleLineHPBar(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, float hpPercent)
    {
        // 1px border
        sb.Draw(pixel, bounds, HPBorder);

        // Inner area (1px inset)
        int ix = bounds.X + 1;
        int iy = bounds.Y + 1;
        int iw = bounds.Width - 2;
        int ih = bounds.Height - 2;

        if (iw <= 0 || ih <= 0) return;

        // Pick colors based on HP threshold
        Color sides, mid;
        if (hpPercent > 0.5f)
            (sides, mid) = (HPGreenSides, HPGreenMid);
        else if (hpPercent > 0.2f)
            (sides, mid) = (HPYellowSides, HPYellowMid);
        else
            (sides, mid) = (HPRedSides, HPRedMid);

        int fillW = (int)(iw * MathHelper.Clamp(hpPercent, 0f, 1f));
        if (fillW <= 0) return;

        // 3 stripes: top=sides, middle=mid, bottom=sides
        int stripeH = ih / 3;
        int remainder = ih - stripeH * 3;
        int midH = stripeH + remainder; // give extra pixels to middle stripe

        sb.Draw(pixel, new Rectangle(ix, iy, fillW, stripeH), sides);
        sb.Draw(pixel, new Rectangle(ix, iy + stripeH, fillW, midH), mid);
        sb.Draw(pixel, new Rectangle(ix, iy + stripeH + midH, fillW, stripeH), sides);
    }

    /// <summary>
    /// Draw an EXP bar: 3px tall, 1px border, blue fill.
    /// </summary>
    public static void DrawEXPBar(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, float expPercent)
    {
        sb.Draw(pixel, bounds, HPBorder);

        int ix = bounds.X + 1;
        int iy = bounds.Y + 1;
        int iw = bounds.Width - 2;
        int ih = bounds.Height - 2;

        if (iw <= 0 || ih <= 0) return;

        int fillW = (int)(iw * MathHelper.Clamp(expPercent, 0f, 1f));
        if (fillW > 0)
            sb.Draw(pixel, new Rectangle(ix, iy, fillW, ih), EXPFill);
    }
}
