using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI;

/// <summary>
/// Low-level drawing primitives for the UI system.
/// No game logic — only pixel-level rendering helpers.
/// </summary>
public static class UIDraw
{
    // ── Rectangles ──

    /// <summary>Draw a filled rounded rectangle.</summary>
    public static void RoundedRect(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, int radius, Color color)
    {
        if (radius <= 0)
        {
            sb.Draw(pixel, bounds, color);
            return;
        }

        int r = Math.Min(radius, Math.Min(bounds.Width / 2, bounds.Height / 2));

        // Draw as non-overlapping horizontal scan-lines (no double-alpha)
        for (int row = 0; row < bounds.Height; row++)
        {
            int y = bounds.Y + row;
            int inset = 0;

            if (row < r)
            {
                // Top corner region — inset from sides
                int dy = r - row;
                int dx = (int)Math.Sqrt(r * r - dy * dy);
                inset = r - dx;
            }
            else if (row >= bounds.Height - r)
            {
                // Bottom corner region — inset from sides
                int dy = row - (bounds.Height - r) + 1;
                int dx = (int)Math.Sqrt(r * r - dy * dy);
                inset = r - dx;
            }

            sb.Draw(pixel, new Rectangle(bounds.X + inset, y, bounds.Width - inset * 2, 1), color);
        }
    }

    /// <summary>Draw a rounded rectangle outline.</summary>
    public static void RoundedRectOutline(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, int radius, int thickness, Color color)
    {
        int r = Math.Min(radius, Math.Min(bounds.Width / 2, bounds.Height / 2));
        int t = Math.Max(1, thickness);

        // Top edge
        sb.Draw(pixel, new Rectangle(bounds.X + r, bounds.Y, bounds.Width - r * 2, t), color);
        // Bottom edge
        sb.Draw(pixel, new Rectangle(bounds.X + r, bounds.Bottom - t, bounds.Width - r * 2, t), color);
        // Left edge
        sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y + r, t, bounds.Height - r * 2), color);
        // Right edge
        sb.Draw(pixel, new Rectangle(bounds.Right - t, bounds.Y + r, t, bounds.Height - r * 2), color);

        // Corner arcs
        if (r > 0)
        {
            DrawCornerArc(sb, pixel, bounds.X + r, bounds.Y + r, r, t, color, true, true);
            DrawCornerArc(sb, pixel, bounds.Right - r - 1, bounds.Y + r, r, t, color, false, true);
            DrawCornerArc(sb, pixel, bounds.X + r, bounds.Bottom - r - 1, r, t, color, true, false);
            DrawCornerArc(sb, pixel, bounds.Right - r - 1, bounds.Bottom - r - 1, r, t, color, false, false);
        }
    }

    /// <summary>Draw a panel with drop shadow, rounded fill, and optional border.</summary>
    public static void ShadowedPanel(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, int radius, Color bg, int shadowOffset, Color shadowColor)
    {
        // Shadow
        var shadowRect = new Rectangle(bounds.X + shadowOffset, bounds.Y + shadowOffset,
            bounds.Width, bounds.Height);
        RoundedRect(sb, pixel, shadowRect, radius, shadowColor);

        // Fill
        RoundedRect(sb, pixel, bounds, radius, bg);
    }

    /// <summary>Draw a glow/highlight border around a panel.</summary>
    public static void GlowBorder(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, int radius, Color color)
    {
        RoundedRectOutline(sb, pixel, bounds, radius, 1, color);
    }

    // ── Text ──

    /// <summary>Draw text with a 1px offset drop shadow.</summary>
    public static void ShadowedText(SpriteBatch sb, PixelFont font,
        string text, Vector2 position, Color color, Color shadowColor)
    {
        font.Draw(text, (int)position.X + 1, (int)position.Y + 1, shadowColor);
        font.Draw(text, (int)position.X, (int)position.Y, color);
    }

    /// <summary>Convert a string to Title Case.</summary>
    public static string TitleCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
    }

    // ── Gradient ──

    /// <summary>Draw a smooth vertical gradient between two colors.</summary>
    public static void VerticalGradient(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, Color top, Color bottom)
    {
        for (int y = 0; y < bounds.Height; y++)
        {
            float t = (float)y / Math.Max(1, bounds.Height - 1);
            Color c = Color.Lerp(top, bottom, t);
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y + y, bounds.Width, 1), c);
        }
    }

    // ── Internal corner helpers ──

    private static void DrawCornerFill(SpriteBatch sb, Texture2D pixel,
        int cx, int cy, int r, Color color, bool flipX, bool flipY)
    {
        for (int dy = 0; dy <= r; dy++)
        {
            int dx = (int)Math.Sqrt(r * r - dy * dy);
            int x = flipX ? cx - dx : cx;
            int y = flipY ? cy - dy : cy + dy;
            int w = dx + (flipX ? 0 : 1);
            sb.Draw(pixel, new Rectangle(x, y, w, 1), color);
        }
    }

    private static void DrawCornerArc(SpriteBatch sb, Texture2D pixel,
        int cx, int cy, int r, int thickness, Color color, bool flipX, bool flipY)
    {
        for (int dy = 0; dy <= r; dy++)
        {
            int dxOuter = (int)Math.Sqrt(r * r - dy * dy);
            int rInner = Math.Max(0, r - thickness);
            int dxInner = dy <= rInner ? (int)Math.Sqrt(rInner * rInner - dy * dy) : 0;
            int arcWidth = dxOuter - dxInner;
            if (arcWidth <= 0) continue;

            int x = flipX ? cx - dxOuter : cx + dxInner;
            int y = flipY ? cy - dy : cy + dy;
            sb.Draw(pixel, new Rectangle(x, y, arcWidth, 1), color);
        }
    }
}
