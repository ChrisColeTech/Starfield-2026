using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.UI;

namespace Starfield2026.Core.Screens;

public class GameOverScreen : IScreenOverlay
{
    private enum Phase { FadeIn, Active, FadeOut }
    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;
    private float _elapsed;
    private const float FadeDuration = 0.5f;

    public bool IsFinished { get; private set; }

    public void Update(float deltaTime, InputSnapshot input)
    {
        _elapsed += deltaTime;

        switch (_phase)
        {
            case Phase.FadeIn:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration) _phase = Phase.Active;
                break;
            case Phase.Active:
                if (input.ConfirmPressed)
                {
                    _phase = Phase.FadeOut;
                    _fadeTimer = 0f;
                }
                break;
            case Phase.FadeOut:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration) IsFinished = true;
                break;
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int screenW, int screenH, int fontScale = 3)
    {
        int scale = fontScale;
        int pad = 12 * scale;
        int radius = Math.Max(2, scale * 2);
        int shadowOff = Math.Max(2, scale * 2);

        // Full-screen dark overlay
        float bgAlpha = _phase == Phase.FadeIn
            ? Math.Min(1f, _fadeTimer / FadeDuration) * 0.85f
            : 0.85f;
        sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * bgAlpha);

        // Panel
        int panelW = Math.Min(screenW - pad * 4, 180 * scale);
        int panelH = 80 * scale;
        int panelX = (screenW - panelW) / 2;
        int panelY = (screenH - panelH) / 2;
        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

        UIDraw.ShadowedPanel(sb, pixel, panelRect, radius,
            UITheme.SlatePanelBg, shadowOff * 2, Color.Black * 0.5f);
        UIDraw.GlowBorder(sb, pixel, panelRect, radius, new Color(200, 40, 40, 120));

        int cy = panelY + pad;

        // "GAME OVER" header
        font.Scale = scale + 2;
        string header = "GAME OVER";
        int hw = font.MeasureWidth(header);
        UIDraw.ShadowedText(sb, font, header,
            new Vector2(panelX + (panelW - hw) / 2, cy),
            new Color(220, 50, 50), UITheme.TextShadow);
        cy += font.CharHeight + pad * 2;

        // Prompt — blink
        font.Scale = scale;
        if (_phase == Phase.Active && (int)(_elapsed * 2.5f) % 2 == 0)
        {
            string prompt = "Press Enter to continue";
            int pw = font.MeasureWidth(prompt);
            UIDraw.ShadowedText(sb, font, prompt,
                new Vector2(panelX + (panelW - pw) / 2, cy),
                UITheme.TextSecondary, UITheme.TextShadow);
        }

        // Fade effect
        float fadeAlpha = _phase switch
        {
            Phase.FadeIn => 1f - _fadeTimer / FadeDuration,
            Phase.FadeOut => _fadeTimer / FadeDuration,
            _ => 0f,
        };
        if (fadeAlpha > 0f)
            sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * fadeAlpha);
    }
}
