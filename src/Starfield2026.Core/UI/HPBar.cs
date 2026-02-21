using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.UI;

/// <summary>
/// BDSP-style HP bar with green→yellow→red color transitions.
/// </summary>
public static class HPBar
{
    private static readonly Color BarBg = new(40, 40, 40, 200);
    private static readonly Color BarBorder = new(60, 60, 60, 200);

    /// <summary>Draw a simple HP fill bar (no label).</summary>
    public static void Draw(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, float percent)
    {
        percent = Math.Clamp(percent, 0f, 1f);

        // Background
        sb.Draw(pixel, bounds, BarBg);

        // Fill
        int fillW = (int)(bounds.Width * percent);
        if (fillW > 0)
        {
            Color fillColor = GetHPColor(percent);
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, fillW, bounds.Height), fillColor);
        }

        // Top highlight line (BDSP triple-line style)
        if (bounds.Height >= 3)
        {
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), Color.White * 0.15f);
        }
    }

    /// <summary>Draw the BDSP triple-line HP bar style.</summary>
    public static void DrawTripleLine(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, float percent)
    {
        percent = Math.Clamp(percent, 0f, 1f);

        // Dark track behind bar
        sb.Draw(pixel, bounds, BarBg);
        sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), BarBorder);
        sb.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), BarBorder);

        // Fill
        int fillW = (int)(bounds.Width * percent);
        if (fillW > 0)
        {
            Color baseColor = GetHPColor(percent);
            Color lightColor = Color.Lerp(baseColor, Color.White, 0.3f);
            Color darkColor = Color.Lerp(baseColor, Color.Black, 0.3f);

            int h = bounds.Height;
            int topH = Math.Max(1, h / 3);
            int botH = Math.Max(1, h / 3);
            int midH = h - topH - botH;

            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, fillW, topH), lightColor);
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y + topH, fillW, midH), baseColor);
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y + topH + midH, fillW, botH), darkColor);
        }
    }

    /// <summary>Get HP bar color based on percentage (green → yellow → red).</summary>
    public static Color GetHPColor(float percent)
    {
        if (percent > 0.5f)
            return Color.Lerp(new Color(255, 200, 0), new Color(0, 200, 80), (percent - 0.5f) * 2f);
        else
            return Color.Lerp(new Color(220, 50, 50), new Color(255, 200, 0), percent * 2f);
    }
}
