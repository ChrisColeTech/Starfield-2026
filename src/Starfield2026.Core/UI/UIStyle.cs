using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI
{
    /// <summary>
    /// Shared utility functions for common UI rendering tasks (text shadows, gradients, HP bars).
    /// </summary>
    public static class UIStyle
    {
        public static void DrawShadowedText(SpriteBatch sb, PixelFont font, string text, Vector2 position, Color color, Color shadowColor)
        {
            // Caller sets font.Scale before calling — we don't overwrite it
            int offset = Math.Max(1, font.Scale);
            font.Draw(text, (int)position.X + offset, (int)position.Y + offset, shadowColor);
            font.Draw(text, (int)position.X, (int)position.Y, color);
        }

        public static void DrawTripleGradient(SpriteBatch sb, Texture2D pixel, Rectangle bounds, Color top, Color mid, Color bot)
        {
            // Simplified: Draw solid for performance, or implement proper gradient if desired
            int halfH = bounds.Height / 2;
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, halfH), top);
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y + halfH, bounds.Width, bounds.Height - halfH), bot);
        }

        public static void DrawHPBar(SpriteBatch sb, Texture2D pixel, Rectangle bounds, float percentage)
        {
            sb.Draw(pixel, bounds, Color.Black * 0.8f);
            Color barColor = percentage > 0.5f ? Color.LimeGreen : (percentage > 0.2f ? Color.Orange : Color.Red);
            int bw = (int)(bounds.Width * MathHelper.Clamp(percentage, 0f, 1f));
            if (bw > 0)
                sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bw, bounds.Height), barColor);
        }

        public static void DrawTripleLineHPBar(SpriteBatch sb, Texture2D pixel, Rectangle bounds, float hpPct, float shieldPct = 0f)
        {
            DrawHPBar(sb, pixel, bounds, hpPct);
        }

        public static void DrawEXPBar(SpriteBatch sb, Texture2D pixel, Rectangle bounds, float percentage)
        {
            sb.Draw(pixel, bounds, Color.Black * 0.8f);
            int bw = (int)(bounds.Width * MathHelper.Clamp(percentage, 0f, 1f));
            if (bw > 0)
                sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bw, bounds.Height), Color.Cyan);
        }

        public static void DrawRightArrow(SpriteBatch sb, Texture2D pixel, Vector2 position, int size, Color color)
        {
            for (int x = 0; x < size; x++)
            {
                int h = size - x;
                sb.Draw(pixel, new Rectangle((int)position.X + x, (int)position.Y + (size - h) / 2, 1, h), color);
            }
        }

        public static void DrawDownArrow(SpriteBatch sb, Texture2D pixel, Vector2 position, int size, Color color)
        {
            for (int y = 0; y < size; y++)
            {
                int w = size - y;
                sb.Draw(pixel, new Rectangle((int)position.X + (size - w) / 2, (int)position.Y + y, w, 1), color);
            }
        }
    }
}
