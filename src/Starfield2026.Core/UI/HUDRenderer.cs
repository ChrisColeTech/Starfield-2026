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

    public void Draw(GraphicsDevice device, GameState state, AmmoSystem? ammo, BoostSystem? boosts, string? activeScreenType, float? speed = null, int overworldBoosts = 0)
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

        bool showCoins = activeScreenType == "overworld";
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
        bool hasAmmo = ammo != null && activeScreenType != "overworld";
        bool hasBoosts = (boosts != null && activeScreenType != "overworld") || (activeScreenType == "overworld" && overworldBoosts > 0);
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
