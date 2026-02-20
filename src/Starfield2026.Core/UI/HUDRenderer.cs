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
    
    public void Draw(GraphicsDevice device, GameState state, AmmoSystem? ammo, BoostSystem? boosts, string? activeScreenType, float? speed = null)
    {
        int screenW = device.Viewport.Width;
        int screenH = device.Viewport.Height;
        
        _font.SetScaleFromResolution(screenW);
        
        int margin = 10 + (screenW / 800 - 1) * 5;
        
        int barW = 120 * (screenW / 800);
        int barH = 10 * (screenW / 800);
        if (barW < 80) barW = 80;
        if (barH < 8) barH = 8;
        int barX = screenW - barW - margin;
        int barY = margin;
        
        _spriteBatch.Draw(_pixel, new Rectangle(barX - 1, barY - 1, barW + 2, barH + 2), Color.Black * 0.8f);
        
        float pct = state.HealthPercent;
        int fillW = (int)(barW * pct);
        Color barColor = pct > 0.5f ? Color.LimeGreen :
                         pct > 0.25f ? Color.Yellow : Color.Red;
        if (fillW > 0)
            _spriteBatch.Draw(_pixel, new Rectangle(barX, barY, fillW, barH), barColor);
        
        string hpText = $"{state.CurrentHealth}/{state.MaxHealth}";
        int hpTextW = _font.MeasureWidth(hpText);
        _font.Draw(hpText, screenW - hpTextW - margin, barY + barH + 4, Color.White);
        
        int leftY = margin;
        
        if (activeScreenType == "overworld")
        {
            string coinText = $"Coins: {state.TotalCoins}";
            int textW = _font.MeasureWidth(coinText);
            _font.Draw(coinText, screenW - textW - margin, barY + barH + 4 + _font.CharHeight + 4, Color.Gold);
        }
        else if (ammo != null)
        {
            var ammoColor = ammo.SelectedType == ProjectileType.Gold ? Color.Gold : Color.Red;
            int ammoCount = ammo.GetSelectedAmmoCount();
            string ammoText = $"Ammo: {ammoCount}";
            _font.Draw(ammoText, margin, leftY, ammoColor);
            leftY += _font.CharHeight + 4;
        }
        
        // Boost count (upper-left, under ammo)
        if (boosts != null && activeScreenType != "overworld")
        {
            string boostText = $"Boosts: {boosts.BoostCount}";
            _font.Draw(boostText, margin, leftY, Color.DodgerBlue);
            leftY += _font.CharHeight + 4;
        }
        
        if ((activeScreenType == "driving" || activeScreenType == "space") && speed.HasValue)
        {
            int speedometerY = screenH - margin - _font.CharHeight;
            string speedText = $"{(int)Math.Abs(speed.Value)} mph";
            _font.Draw(speedText, margin, speedometerY, Color.White);
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
