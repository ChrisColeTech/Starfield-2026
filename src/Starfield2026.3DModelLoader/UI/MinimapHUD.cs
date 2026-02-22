#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.UI;

public class MinimapHUD
{
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private SpriteFont? _font;

    public void Initialize(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont? font = null)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _font = font;
    }

    public void Draw(GraphicsDevice device, Vector3 playerWorldPos, float playerYaw, string statusText)
    {
        int screenW = device.Viewport.Width;
        int screenH = device.Viewport.Height;
        int scale = Math.Max(1, screenW / 400);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        // --- Minimap ---
        DrawMinimap(screenW, screenH, scale, playerWorldPos, playerYaw);

        // --- Status bar at top ---
        DrawStatusBar(screenW, scale, statusText);

        // --- Controls help at bottom-right ---
        DrawControls(screenW, screenH, scale);

        _spriteBatch.End();
    }

    private void DrawMinimap(int screenW, int screenH, int scale, Vector3 playerWorldPos, float playerYaw)
    {
        int mapDiameter = Math.Max(80, 120 * screenW / 800);
        int mapRadius = mapDiameter / 2;
        int mapPad = 4 * scale;
        int margin = 4 * scale;
        int shadowOff = Math.Max(1, scale);

        int cx = margin + mapPad + mapRadius;
        int cy = screenH - margin - mapPad - mapRadius;

        var bgColor = new Color(20, 25, 45, 200);
        var borderColor = new Color(120, 130, 150, 150);

        // Shadow + background circle
        DrawFilledCircle(cx + shadowOff, cy + shadowOff, mapRadius + mapPad, Color.Black * 0.3f);
        DrawFilledCircle(cx, cy, mapRadius + mapPad, bgColor);

        float worldHalf = 500f;
        float sinYaw = (float)Math.Sin(playerYaw);
        float cosYaw = (float)Math.Cos(playerYaw);

        float pnx = (playerWorldPos.X + worldHalf) / (worldHalf * 2f);
        float pnz = (playerWorldPos.Z + worldHalf) / (worldHalf * 2f);

        var nwColor = new Color(40, 200, 80, 150);
        var neColor = new Color(60, 140, 220, 150);
        var swColor = new Color(220, 180, 40, 150);
        var seColor = new Color(160, 60, 200, 150);

        // Draw quadrant-colored pixels, rotated by player yaw
        for (int row = -mapRadius; row <= mapRadius; row++)
        {
            int dx = (int)Math.Sqrt(mapRadius * mapRadius - row * row);
            if (dx <= 0) continue;

            int y = cy + row;
            int xStart = cx - dx;
            int xEnd = cx + dx;

            Color? spanColor = null;
            int spanStart = xStart;

            for (int x = xStart; x <= xEnd; x++)
            {
                float lx = (float)(x - cx) / mapRadius;
                float ly = (float)(y - cy) / mapRadius;

                float wx = -(lx * cosYaw + ly * sinYaw);
                float wy = lx * sinYaw - ly * cosYaw;

                float worldNx = pnx + wx * 0.5f;
                float worldNz = pnz + wy * 0.5f;

                Color c;
                if (worldNx < 0 || worldNx > 1 || worldNz < 0 || worldNz > 1)
                    c = bgColor;
                else if (worldNx < 0.5f)
                    c = worldNz < 0.5f ? nwColor : swColor;
                else
                    c = worldNz < 0.5f ? neColor : seColor;

                if (c != spanColor)
                {
                    if (spanColor.HasValue && x > spanStart)
                        _spriteBatch.Draw(_pixel, new Rectangle(spanStart, y, x - spanStart, 1), spanColor.Value);
                    spanColor = c;
                    spanStart = x;
                }
            }
            if (spanColor.HasValue && xEnd >= spanStart)
                _spriteBatch.Draw(_pixel, new Rectangle(spanStart, y, xEnd - spanStart + 1, 1), spanColor.Value);
        }

        // Quadrant divider lines
        var lineColor = new Color(180, 180, 200, 80);
        float centerOffX = (0.5f - pnx) / 0.5f;
        float centerOffZ = (0.5f - pnz) / 0.5f;
        float scx = -centerOffX * cosYaw + centerOffZ * sinYaw;
        float scy = -centerOffX * sinYaw - centerOffZ * cosYaw;
        int worldCx = cx + (int)(scx * mapRadius);
        int worldCy = cy + (int)(scy * mapRadius);

        for (int i = -mapRadius; i <= mapRadius; i++)
        {
            int px = worldCx + (int)(-cosYaw * i);
            int py = worldCy + (int)(-sinYaw * i);
            if ((px - cx) * (px - cx) + (py - cy) * (py - cy) < mapRadius * mapRadius)
                _spriteBatch.Draw(_pixel, new Rectangle(px, py, 1, 1), lineColor);

            px = worldCx + (int)(sinYaw * i);
            py = worldCy + (int)(-cosYaw * i);
            if ((px - cx) * (px - cx) + (py - cy) * (py - cy) < mapRadius * mapRadius)
                _spriteBatch.Draw(_pixel, new Rectangle(px, py, 1, 1), lineColor);
        }

        // Border
        DrawCircleOutline(cx, cy, mapRadius, borderColor);

        // Player dot
        int dotSize = Math.Max(3, 2 * scale);
        _spriteBatch.Draw(_pixel, new Rectangle(cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize), Color.White);

        // North indicator
        float nDirX = -sinYaw;
        float nDirY = cosYaw;
        int nLen = mapRadius - 4 * scale;
        int nx = cx + (int)(nDirX * nLen);
        int ny = cy + (int)(nDirY * nLen);
        int nSize = Math.Max(3, 2 * scale);
        _spriteBatch.Draw(_pixel, new Rectangle(nx - nSize / 2, ny - nSize / 2, nSize, nSize), new Color(255, 60, 60));
    }

    private void DrawStatusBar(int screenW, int scale, string statusText)
    {
        int barH = 6 + 12 * scale;
        var barBg = new Color(20, 25, 45, 200);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenW, barH), barBg);

        if (_font != null)
        {
            _spriteBatch.DrawString(_font, statusText, new Vector2(8, 4), Color.White);
        }
    }

    private void DrawControls(int screenW, int screenH, int scale)
    {
        if (_font == null) return;

        string[] lines = { "WASD: Move", "Shift: Run", "Tab: Next Character", "Esc: Quit" };
        int y = screenH - 8;
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var size = _font.MeasureString(lines[i]);
            y -= (int)size.Y + 2;
            _spriteBatch.DrawString(_font, lines[i], new Vector2(screenW - size.X - 8, y), new Color(180, 180, 200, 150));
        }
    }

    private void DrawFilledCircle(int cx, int cy, int r, Color color)
    {
        for (int row = -r; row <= r; row++)
        {
            int dx = (int)Math.Sqrt(r * r - row * row);
            if (dx <= 0) continue;
            _spriteBatch.Draw(_pixel, new Rectangle(cx - dx, cy + row, dx * 2, 1), color);
        }
    }

    private void DrawCircleOutline(int cx, int cy, int r, Color color)
    {
        for (int row = -r; row <= r; row++)
        {
            int dx = (int)Math.Sqrt(r * r - row * row);
            int dxInner = (int)Math.Sqrt(Math.Max(0, (r - 1) * (r - 1) - row * row));
            int width = dx - dxInner;
            if (width <= 0) width = 1;
            _spriteBatch.Draw(_pixel, new Rectangle(cx - dx, cy + row, width, 1), color);
            _spriteBatch.Draw(_pixel, new Rectangle(cx + dx - width, cy + row, width, 1), color);
        }
    }
}
