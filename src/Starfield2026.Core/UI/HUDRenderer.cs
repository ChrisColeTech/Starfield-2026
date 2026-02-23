using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.Save;
using Starfield2026.Core.Systems;

namespace Starfield2026.Core.UI;

public class HUDRenderer
{
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private PixelFont _font = null!;

    public void Initialize(SpriteBatch spriteBatch, Texture2D pixel)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _font = new PixelFont(spriteBatch, pixel);
    }

    public void Draw(GraphicsDevice device, GameState state, AmmoSystem? ammo, BoostSystem? boosts, string? activeScreenType, float? speed = null, int overworldBoosts = 0, Vector3? playerWorldPos = null, float playerYaw = 0f, List<Vector3>? enemyPositions = null, float? fuelPercent = null, bool fuelOverheated = false)
    {
        int screenW = device.Viewport.Width;
        int screenH = device.Viewport.Height;

        int scale = UITheme.GetFontScale(screenW);
        int textScale = Math.Max(1, scale - 1);
        _font.Scale = textScale;

        int margin = 0;
        int pad = 6 * scale;
        int radius = Math.Max(2, scale * 2);
        int lineH = _font.CharHeight;
        int lineGap = 3 * scale;
        int shadowOff = Math.Max(1, scale);

        // ═══════════════════════════════════════════════
        //  RIGHT PANEL — HP + HP numbers + Coins
        // ═══════════════════════════════════════════════
        int barW = 120 * (screenW / 800);
        int barH = Math.Max(8, 10 * (screenW / 800));
        if (barW < 80) barW = 80;

        bool showCoins = activeScreenType == "overworld" || activeScreenType == "freeroam";
        int rightRows = 2;
        if (showCoins) rightRows = 3;

        int rightPanelW = barW + pad * 2;
        int rightPanelH = pad + barH + lineGap + lineH * (rightRows - 1) + lineGap * (rightRows - 2) + pad;
        int rightPanelX = screenW - rightPanelW - margin;
        int rightPanelY = margin;
        var rightPanel = new Rectangle(rightPanelX, rightPanelY, rightPanelW, rightPanelH);

        // Panel with drop shadow
        UIDraw.ShadowedPanel(_spriteBatch, _pixel, rightPanel, radius,
            UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);

        // HP bar
        int barX = rightPanelX + pad;
        int barY = rightPanelY + pad;
        HPBar.Draw(_spriteBatch, _pixel, new Rectangle(barX, barY, barW, barH), state.HealthPercent);

        // HP text (right-aligned under bar)
        int contentY = barY + barH + lineGap;
        string hpText = $"{state.CurrentHealth}/{state.MaxHealth}";
        int hpTextW = _font.MeasureWidth(hpText);
        UIDraw.ShadowedText(_spriteBatch, _font, hpText,
            new Vector2(rightPanel.Right - pad - hpTextW, contentY),
            UITheme.TextSecondary, UITheme.TextShadow);

        // Coins
        if (showCoins)
        {
            contentY += lineH + lineGap;
            string coinText = $"Coins: {state.TotalCoins}";
            int coinW = _font.MeasureWidth(coinText);
            UIDraw.ShadowedText(_spriteBatch, _font, coinText,
                new Vector2(rightPanel.Right - pad - coinW, contentY),
                UITheme.WarmHighlight, UITheme.TextShadow);
        }

        // ═══════════════════════════════════════════════
        //  LEFT PANEL — Ammo / Boosts
        // ═══════════════════════════════════════════════
        bool noAmmoScreen = activeScreenType == "overworld" || activeScreenType == "freeroam";
        bool hasAmmo = ammo != null && !noAmmoScreen;
        bool hasBoosts = boosts != null && (boosts.BoostCount > 0 || boosts.IsActive);
        bool hasSpeed = (activeScreenType == "driving" || activeScreenType == "space" || activeScreenType == "floaty") && speed.HasValue;
        bool hasFuel = fuelPercent.HasValue;

        int leftLineCount = 0;
        if (hasAmmo) leftLineCount++;
        if (hasBoosts) leftLineCount++;

        if (leftLineCount > 0 || hasFuel)
        {
            int leftPanelW = barW + pad * 2;
            int boostRowH = hasBoosts ? (lineH + lineGap + barH) : 0;
            int ammoRowH = hasAmmo ? lineH : 0;
            int fuelRowH = hasFuel ? (lineGap + lineH + lineGap + barH) : 0;
            int totalRowH = ammoRowH + boostRowH + (hasAmmo && hasBoosts ? lineGap : 0) + fuelRowH;
            int leftPanelH = pad + totalRowH + pad;
            int leftPanelX = margin;
            int leftPanelY = margin;
            var leftPanel = new Rectangle(leftPanelX, leftPanelY, leftPanelW, leftPanelH);

            UIDraw.ShadowedPanel(_spriteBatch, _pixel, leftPanel, radius,
                UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);

            int ly = leftPanelY + pad;

            if (hasAmmo && ammo != null)
            {
                var ammoColor = ammo.SelectedType == ProjectileType.Gold ? UITheme.WarmHighlight : Color.Red;
                int ammoCount = ammo.GetSelectedAmmoCount();
                string ammoText = $"Ammo: {ammoCount}";
                UIDraw.ShadowedText(_spriteBatch, _font, ammoText,
                    new Vector2(leftPanelX + pad, ly), ammoColor, UITheme.TextShadow);
                ly += lineH + lineGap;
            }

            if (hasBoosts && boosts != null)
            {
                int boostCount = boosts.BoostCount + (boosts.IsActive ? 1 : 0);
                string boostText = $"Boosts: {boostCount}";
                UIDraw.ShadowedText(_spriteBatch, _font, boostText,
                    new Vector2(leftPanelX + pad, ly), UITheme.PurpleAccent, UITheme.TextShadow);
                ly += lineH + lineGap;
                BoostBar.Draw(_spriteBatch, _pixel,
                    new Rectangle(leftPanelX + pad, ly, barW, barH),
                    boosts.BoostCount, boosts.IsActive, boosts.ActivePercent);
                ly += barH;
            }

            if (hasFuel)
            {
                ly += lineGap;
                var fuelColor = fuelOverheated ? Color.Red : new Color(60, 160, 255);
                string fuelText = fuelOverheated ? "COOLDOWN" : "Fuel";
                UIDraw.ShadowedText(_spriteBatch, _font, fuelText,
                    new Vector2(leftPanelX + pad, ly), fuelColor, UITheme.TextShadow);
                ly += lineH + lineGap;

                // Background
                var fuelBarRect = new Rectangle(leftPanelX + pad, ly, barW, barH);
                _spriteBatch.Draw(_pixel, fuelBarRect, new Color(20, 30, 50));
                // Fill
                int fillW = (int)(barW * fuelPercent!.Value);
                if (fillW > 0)
                {
                    var fillColor = fuelOverheated ? new Color(200, 50, 50) : new Color(60, 160, 255);
                    _spriteBatch.Draw(_pixel, new Rectangle(fuelBarRect.X, fuelBarRect.Y, fillW, barH), fillColor);
                }
            }
        }

        // ═══════════════════════════════════════════════
        //  SPEED / ALTIMETER — bottom-right panel
        // ═══════════════════════════════════════════════
        bool isFloaty = activeScreenType == "floaty";
        if (isFloaty && hasSpeed && playerWorldPos.HasValue)
        {
            // Combined panel with both speed and altitude
            string speedText = $"{(int)Math.Abs(speed!.Value)} mph";
            string altText = $"{(int)playerWorldPos.Value.Y} ft";
            int speedTextW = _font.MeasureWidth(speedText);
            int altTextW = _font.MeasureWidth(altText);
            int maxTextW = Math.Max(speedTextW, altTextW);
            int combPanelW = maxTextW + pad * 2;
            int combPanelH = lineH * 2 + lineGap + pad * 2;
            int combPanelX = screenW - combPanelW - margin;
            int combPanelY = screenH - combPanelH - margin;
            var combPanel = new Rectangle(combPanelX, combPanelY, combPanelW, combPanelH);

            UIDraw.ShadowedPanel(_spriteBatch, _pixel, combPanel, radius,
                UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);
            UIDraw.ShadowedText(_spriteBatch, _font, speedText,
                new Vector2(combPanel.Right - pad - speedTextW, combPanelY + pad),
                UITheme.TextPrimary, UITheme.TextShadow);
            UIDraw.ShadowedText(_spriteBatch, _font, altText,
                new Vector2(combPanel.Right - pad - altTextW, combPanelY + pad + lineH + lineGap),
                UITheme.TextSecondary, UITheme.TextShadow);
        }
        else if (hasSpeed)
        {
            // Speed-only panel for driving/space
            string speedText = $"{(int)Math.Abs(speed!.Value)} mph";
            int speedTextW = _font.MeasureWidth(speedText);
            int speedPanelW = speedTextW + pad * 2;
            int speedPanelH = lineH + pad * 2;
            int speedPanelX = screenW - speedPanelW - margin;
            int speedPanelY = screenH - speedPanelH - margin;
            var speedPanel = new Rectangle(speedPanelX, speedPanelY, speedPanelW, speedPanelH);

            UIDraw.ShadowedPanel(_spriteBatch, _pixel, speedPanel, radius,
                UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);
            UIDraw.ShadowedText(_spriteBatch, _font, speedText,
                new Vector2(speedPanelX + pad, speedPanelY + pad), UITheme.TextPrimary, UITheme.TextShadow);
        }

        // ═══════════════════════════════════════════════
        //  MINIMAP — bottom-left circle (all screens)
        //  Rotates with player yaw, north indicator
        // ═══════════════════════════════════════════════
        if (playerWorldPos.HasValue)
        {
            int mapDiameter = Math.Max(80, 120 * screenW / 800);
            int mapRadius = mapDiameter / 2;
            int mapPad = 4 * scale;

            // Center of the minimap circle on screen
            int cx = margin + mapPad + mapRadius;
            int cy = screenH - margin - mapPad - mapRadius;

            // Shadow circle
            DrawFilledCircle(_spriteBatch, _pixel, cx + shadowOff, cy + shadowOff, mapRadius + mapPad, Color.Black * 0.3f);
            // Background circle
            DrawFilledCircle(_spriteBatch, _pixel, cx, cy, mapRadius + mapPad, UITheme.SlatePanelBg);

            // Draw quadrant fills rotated by player yaw
            // For each pixel in the circle, rotate by -yaw to get world direction,
            // then color by which quadrant that world position falls in
            // Minimap view radius in world units — how much world area the minimap shows
            float mapViewRadius = 500f;
            float playerX = playerWorldPos.Value.X;
            float playerZ = playerWorldPos.Value.Z;

            float sinYaw = (float)Math.Sin(playerYaw);
            float cosYaw = (float)Math.Cos(playerYaw);

            bool useQuadrants = activeScreenType == "floaty" || activeScreenType == "freeroam";
            Color nwColor, neColor, swColor, seColor;
            if (useQuadrants)
            {
                nwColor = new Color(40, 200, 80, 150);
                neColor = new Color(60, 140, 220, 150);
                swColor = new Color(220, 180, 40, 150);
                seColor = new Color(160, 60, 200, 150);
            }
            else
            {
                Color gridColor = activeScreenType switch
                {
                    "driving" => new Color(80, 120, 80, 150),
                    "space" => new Color(0, 140, 255, 150),
                    "overworld" => new Color(40, 180, 80, 150),
                    _ => new Color(0, 180, 220, 150),
                };
                nwColor = neColor = swColor = seColor = gridColor;
            }

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

                    // Rotate screen pixel to world offset from player
                    float wx = -(lx * cosYaw + ly * sinYaw) * mapViewRadius;
                    float wz = (lx * sinYaw - ly * cosYaw) * mapViewRadius;

                    // Actual world coordinates
                    float worldX = playerX + wx;
                    float worldZ = playerZ + wz;

                    Color c;
                    if (worldX < 0)
                        c = worldZ < 0 ? nwColor : swColor;
                    else
                        c = worldZ < 0 ? neColor : seColor;

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

            // Quadrant divider lines — world X=0 and Z=0 axes, rotated by yaw
            var lineColor = UITheme.TextSecondary * 0.5f;
            // World origin offset from player in minimap-pixel space
            float originOffX = -playerX / mapViewRadius;
            float originOffZ = -playerZ / mapViewRadius;
            float scx = -originOffX * cosYaw + originOffZ * sinYaw;
            float scy = -originOffX * sinYaw - originOffZ * cosYaw;
            int worldCx = cx + (int)(scx * mapRadius);
            int worldCy = cy + (int)(scy * mapRadius);
            // World X axis on screen: direction = (-cos, -sin)
            // World Z axis on screen: direction = (sin, -cos)
            for (int i = -mapRadius; i <= mapRadius; i++)
            {
                // World X axis line
                int px = worldCx + (int)(-cosYaw * i);
                int py = worldCy + (int)(-sinYaw * i);
                int distSq = (px - cx) * (px - cx) + (py - cy) * (py - cy);
                if (distSq < mapRadius * mapRadius)
                    _spriteBatch.Draw(_pixel, new Rectangle(px, py, 1, 1), lineColor);
                // World Z axis line
                px = worldCx + (int)(sinYaw * i);
                py = worldCy + (int)(-cosYaw * i);
                distSq = (px - cx) * (px - cx) + (py - cy) * (py - cy);
                if (distSq < mapRadius * mapRadius)
                    _spriteBatch.Draw(_pixel, new Rectangle(px, py, 1, 1), lineColor);
            }

            // Circle border
            DrawCircleOutline(_spriteBatch, _pixel, cx, cy, mapRadius, UITheme.TextSecondary * 0.6f);

            // Player dot at center
            int dotSize = Math.Max(3, 2 * scale);
            _spriteBatch.Draw(_pixel, new Rectangle(cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize), Color.White);

            // Enemy red dots on minimap
            if (enemyPositions != null)
            {
                int enemyDotSize = Math.Max(2, scale + 1);
                foreach (var ePos in enemyPositions)
                {
                    float ex = (ePos.X - playerX) / mapViewRadius;
                    float ez = (ePos.Z - playerZ) / mapViewRadius;
                    // Rotate to screen space using same transform as minimap
                    float sx = -(ex * cosYaw + ez * sinYaw);
                    float sy = -(ex * sinYaw - ez * cosYaw);
                    // Check if within circle
                    if (sx * sx + sy * sy < 1f)
                    {
                        int epx = cx + (int)(sx * mapRadius);
                        int epy = cy + (int)(sy * mapRadius);
                        _spriteBatch.Draw(_pixel, new Rectangle(epx - enemyDotSize / 2, epy - enemyDotSize / 2, enemyDotSize, enemyDotSize), Color.Red);
                    }
                }
            }

            // North indicator "N" — north = world (0, -1) in XZ
            float nDirX = -sinYaw;
            float nDirY = cosYaw;
            int nLen = mapRadius - 4 * scale;
            int nx = cx + (int)(nDirX * nLen);
            int ny = cy + (int)(nDirY * nLen);
            int savedScale = _font.Scale;
            _font.Scale = Math.Max(1, scale - 1);
            int nw = _font.MeasureWidth("N");
            _font.Draw("N", nx - nw / 2, ny - _font.CharHeight / 2, new Color(255, 60, 60));
            _font.Scale = savedScale;
        }
    }

    private static void DrawFilledCircle(SpriteBatch sb, Texture2D pixel, int cx, int cy, int r, Color color)
    {
        for (int row = -r; row <= r; row++)
        {
            int dx = (int)Math.Sqrt(r * r - row * row);
            if (dx <= 0) continue;
            sb.Draw(pixel, new Rectangle(cx - dx, cy + row, dx * 2, 1), color);
        }
    }

    private static void DrawCircleOutline(SpriteBatch sb, Texture2D pixel, int cx, int cy, int r, Color color)
    {
        // Midpoint circle scan — draw 1px border
        for (int row = -r; row <= r; row++)
        {
            int dx = (int)Math.Sqrt(r * r - row * row);
            int dxInner = (int)Math.Sqrt(Math.Max(0, (r - 1) * (r - 1) - row * row));
            int width = dx - dxInner;
            if (width <= 0) width = 1;
            sb.Draw(pixel, new Rectangle(cx - dx, cy + row, width, 1), color);
            sb.Draw(pixel, new Rectangle(cx + dx - width, cy + row, width, 1), color);
        }
    }

    public void DrawTransition(GraphicsDevice device, float alpha)
    {
        if (alpha > 0)
        {
            _spriteBatch.Draw(_pixel,
                new Rectangle(0, 0, device.Viewport.Width, device.Viewport.Height),
                Color.Black * alpha);
        }
    }
}
