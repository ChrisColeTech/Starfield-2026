using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.UI;

/// <summary>
/// EXP bar — warm gold fill on dark slate track.
/// </summary>
public static class EXPBar
{
    private static readonly Color FillColor = UITheme.WarmHighlight;
    private static readonly Color FillHighlight = new(255, 210, 120);

    /// <summary>Draw an EXP fill bar.</summary>
    public static void Draw(SpriteBatch sb, Texture2D pixel,
        Rectangle bounds, float percent)
    {
        percent = Math.Clamp(percent, 0f, 1f);

        // Background track
        sb.Draw(pixel, bounds, new Color(20, 22, 32, 200));

        // Fill
        int fillW = (int)(bounds.Width * percent);
        if (fillW > 0)
        {
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, fillW, bounds.Height), FillColor);

            // Top highlight for depth
            if (bounds.Height >= 2)
            {
                sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, fillW, 1), FillHighlight);
            }
        }
    }
}
