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

    public void Draw(GraphicsDevice device, GameState state, AmmoSystem? ammo, BoostSystem? boosts, string? activeScreenType, float? speed = null, int overworldBoosts = 0, Vector3? playerWorldPos = null, float playerYaw = 0f)
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
        bool hasBoosts = (boosts != null && !noAmmoScreen) || (activeScreenType == "overworld" && overworldBoosts > 0);
        bool hasSpeed = (activeScreenType == "driving" || activeScreenType == "space") && speed.HasValue;

        int leftLineCount = 0;
        if (hasAmmo) leftLineCount++;
        if (hasBoosts) leftLineCount++;

        if (leftLineCount > 0)
        {
            int leftPanelW = barW + pad * 2;
            int leftPanelH = pad + lineH * leftLineCount + lineGap * Math.Max(0, leftLineCount - 1) + pad;
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

            if (hasBoosts)
            {
                int boostCount = activeScreenType == "overworld" ? overworldBoosts : boosts?.BoostCount ?? 0;
                string boostText = $"Boosts: {boostCount}";
                UIDraw.ShadowedText(_spriteBatch, _font, boostText,
                    new Vector2(leftPanelX + pad, ly), UITheme.PurpleAccent, UITheme.TextShadow);
            }
        }

        // ═══════════════════════════════════════════════
        //  SPEED — bottom-left panel
        // ═══════════════════════════════════════════════
        if (hasSpeed)
        {
            string speedText = $"{(int)Math.Abs(speed!.Value)} mph";
            int speedTextW = _font.MeasureWidth(speedText);
            int speedPanelW = speedTextW + pad * 2;
            int speedPanelH = lineH + pad * 2;
            int speedPanelX = margin;
            int speedPanelY = screenH - speedPanelH - margin;
            var speedPanel = new Rectangle(speedPanelX, speedPanelY, speedPanelW, speedPanelH);

            UIDraw.ShadowedPanel(_spriteBatch, _pixel, speedPanel, radius,
                UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);
            UIDraw.ShadowedText(_spriteBatch, _font, speedText,
                new Vector2(speedPanelX + pad, speedPanelY + pad), UITheme.TextPrimary, UITheme.TextShadow);
        }

        // ═══════════════════════════════════════════════
        //  MINIMAP — bottom-left circle (freeroam & overworld)
        //  Rotates with player yaw, north indicator
        // ═══════════════════════════════════════════════
        if ((activeScreenType == "freeroam" || activeScreenType == "overworld") && playerWorldPos.HasValue)
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
            float worldHalf = 500f;
            // Player forward = (Sin(yaw), 0, Cos(yaw)), +Z = south, -Z = north
            // We want minimap "up" = player forward, so rotate screen coords by yaw
            float sinYaw = (float)Math.Sin(playerYaw);
            float cosYaw = (float)Math.Cos(playerYaw);

            // Player normalized position (0..1 range)
            float pnx = (playerWorldPos.Value.X + worldHalf) / (worldHalf * 2f);
            float pnz = (playerWorldPos.Value.Z + worldHalf) / (worldHalf * 2f);

            bool isFreeroam = activeScreenType == "freeroam";
            var nwColor = isFreeroam ? new Color(40, 200, 80, 150) : new Color(0, 180, 220, 150);
            var neColor = isFreeroam ? new Color(60, 140, 220, 150) : new Color(0, 180, 220, 150);
            var swColor = isFreeroam ? new Color(220, 180, 40, 150) : new Color(0, 180, 220, 150);
            var seColor = isFreeroam ? new Color(160, 60, 200, 150) : new Color(0, 180, 220, 150);

            for (int row = -mapRadius; row <= mapRadius; row++)
            {
                // Find horizontal extent of circle at this row
                int dx = (int)Math.Sqrt(mapRadius * mapRadius - row * row);
                if (dx <= 0) continue;

                int y = cy + row;
                int xStart = cx - dx;
                int xEnd = cx + dx;

                // Batch consecutive pixels of the same color into spans
                Color? spanColor = null;
                int spanStart = xStart;

                for (int x = xStart; x <= xEnd; x++)
                {
                    // Pixel offset from center, normalized to -1..1
                    float lx = (float)(x - cx) / mapRadius;
                    float ly = (float)(y - cy) / mapRadius;

                    // Rotate screen pixel into world space
                    // Screen up (ly<0) → player forward (Sin(yaw), Cos(yaw))
                    // Screen right (lx>0) → player right (-Cos(yaw), Sin(yaw))
                    float wx = -(lx * cosYaw + ly * sinYaw);
                    float wy = lx * sinYaw - ly * cosYaw;

                    // Map from minimap-space to world-space normalized coords
                    // minimap center = player position in world
                    float worldNx = pnx + wx * 0.5f;
                    float worldNz = pnz + wy * 0.5f;

                    Color c;
                    if (worldNx < 0 || worldNx > 1 || worldNz < 0 || worldNz > 1)
                        c = UITheme.SlatePanelBg;
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
                // Flush last span
                if (spanColor.HasValue && xEnd >= spanStart)
                    _spriteBatch.Draw(_pixel, new Rectangle(spanStart, y, xEnd - spanStart + 1, 1), spanColor.Value);
            }

            // Quadrant divider lines — world X=0 and Z=0 axes, rotated by yaw
            // Inverse transform (world→screen): lx = -wx*cos + wy*sin, ly = -wx*sin - wy*cos
            var lineColor = UITheme.TextSecondary * 0.5f;
            // World center offset from player in minimap-normalized space
            float centerOffX = (0.5f - pnx) / 0.5f;
            float centerOffZ = (0.5f - pnz) / 0.5f;
            float scx = -centerOffX * cosYaw + centerOffZ * sinYaw;
            float scy = -centerOffX * sinYaw - centerOffZ * cosYaw;
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

            // North indicator — north = world (0, -1) in XZ
            // Using inverse transform: screenX = -0*cos + (-1)*sin = -sin, screenY = -0*sin - (-1)*cos = cos
            float nDirX = -sinYaw;
            float nDirY = cosYaw;
            int nLen = mapRadius - 4 * scale;
            int nx = cx + (int)(nDirX * nLen);
            int ny = cy + (int)(nDirY * nLen);
            int nSize = Math.Max(3, 2 * scale);
            _spriteBatch.Draw(_pixel, new Rectangle(nx - nSize / 2, ny - nSize / 2, nSize, nSize), new Color(255, 60, 60));
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
