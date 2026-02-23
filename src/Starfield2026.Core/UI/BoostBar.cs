using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.UI;

public static class BoostBar
{
    private static readonly Color BarBg = new(40, 40, 40, 200);
    private static readonly Color PurpleFill = new(120, 60, 220);
    private static readonly Color PurpleDrain = new(160, 90, 255);

    public static void Draw(SpriteBatch sb, Texture2D pixel, Rectangle bounds,
        int boostCount, bool isActive, float activePercent)
    {
        // Background
        sb.Draw(pixel, bounds, BarBg);

        // Bar shows current boost fuel:
        //   Has boosts, not active → full (ready to use)
        //   Active → draining from 100% to 0%
        //   No boosts, not active → empty
        float percent;
        Color fillColor;

        if (isActive)
        {
            percent = Math.Clamp(activePercent, 0f, 1f);
            fillColor = PurpleDrain;
        }
        else if (boostCount > 0)
        {
            percent = 1f;
            fillColor = PurpleFill;
        }
        else
        {
            percent = 0f;
            fillColor = PurpleFill;
        }

        int fillW = (int)(bounds.Width * percent);
        if (fillW > 0)
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, fillW, bounds.Height), fillColor);

        // Top highlight
        if (bounds.Height >= 3)
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), Color.White * 0.15f);
    }
}
