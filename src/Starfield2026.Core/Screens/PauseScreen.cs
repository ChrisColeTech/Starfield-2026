using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.UI;

namespace Starfield2026.Core.Screens;

/// <summary>
/// Full-screen pause menu with tabbed Settings / Controls panels.
/// Triggered by Tab, closed by Cancel.
/// </summary>
public class PauseScreen : IScreenOverlay
{
    private enum Phase { FadeIn, Active, FadeOut }
    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;
    private const float FadeDuration = 0.15f;

    private int _tabIndex;
    private int _settingsIndex;
    private static readonly string[] TabLabels = { "Settings", "Controls" };

    // Settings options
    private static readonly string[] SettingsOptions = { "Select Character", "Resume" };

    /// <summary>Set when the user picks "Select Character".</summary>
    public bool RequestCharacterSelect { get; private set; }
    public bool IsFinished { get; private set; }

    public void Update(float deltaTime, InputSnapshot input)
    {
        switch (_phase)
        {
            case Phase.FadeIn:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration) _phase = Phase.Active;
                break;
            case Phase.Active:
                UpdateNavigation(input);
                break;
            case Phase.FadeOut:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration) IsFinished = true;
                break;
        }
    }

    private void UpdateNavigation(InputSnapshot input)
    {
        if (input.Cancel) { BeginExit(); return; }

        // Tab switching
        if (input.Left) _tabIndex = Math.Max(0, _tabIndex - 1);
        if (input.Right) _tabIndex = Math.Min(TabLabels.Length - 1, _tabIndex + 1);

        if (_tabIndex == 0) // Settings
        {
            if (input.Up) _settingsIndex = Math.Max(0, _settingsIndex - 1);
            if (input.Down) _settingsIndex = Math.Min(SettingsOptions.Length - 1, _settingsIndex + 1);
            if (input.Confirm)
            {
                if (SettingsOptions[_settingsIndex] == "Select Character")
                {
                    RequestCharacterSelect = true;
                    BeginExit();
                }
                else if (SettingsOptions[_settingsIndex] == "Resume")
                {
                    BeginExit();
                }
            }
        }
        // Controls tab is read-only, no interaction needed
    }

    private void BeginExit() { _phase = Phase.FadeOut; _fadeTimer = 0f; }

    public void Draw(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int screenW, int screenH, int fontScale = 3)
    {
        int scale = fontScale;
        font.Scale = scale;

        int pad = 12 * scale;
        int radius = Math.Max(2, scale * 2);
        int shadowOff = Math.Max(2, scale * 2);
        int lineH = font.CharHeight + 4 * scale;

        // Semi-transparent backdrop
        sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * 0.7f);

        // Panel dimensions
        int panelW = Math.Min(screenW - pad * 4, 220 * scale);
        int panelH = Math.Min(screenH - pad * 4, 180 * scale);
        int panelX = (screenW - panelW) / 2;
        int panelY = (screenH - panelH) / 2;
        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

        // Panel background
        UIDraw.ShadowedPanel(sb, pixel, panelRect, radius,
            UITheme.SlatePanelBg, shadowOff * 2, Color.Black * 0.5f);
        UIDraw.GlowBorder(sb, pixel, panelRect, radius, UITheme.PurpleGlow);

        int cy = panelY + pad;

        // ── Header ──
        font.Scale = scale + 1;
        string header = "Pause";
        int hw = font.MeasureWidth(header);
        UIDraw.ShadowedText(sb, font, header,
            new Vector2(panelX + (panelW - hw) / 2, cy),
            UITheme.PurpleAccent, UITheme.TextShadow);
        cy += font.CharHeight + pad;
        font.Scale = scale;

        // ── Tabs ──
        int tabW = (panelW - pad * 2) / TabLabels.Length;
        int tabH = lineH;
        for (int i = 0; i < TabLabels.Length; i++)
        {
            int tx = panelX + pad + i * tabW;
            var tabRect = new Rectangle(tx, cy, tabW - 2 * scale, tabH);
            bool sel = i == _tabIndex;

            UIDraw.RoundedRect(sb, pixel, tabRect, scale,
                sel ? UITheme.PurpleAccent : UITheme.SlateCard);
            if (sel) UIDraw.GlowBorder(sb, pixel, tabRect, scale, UITheme.PurpleGlow);

            font.Scale = scale;
            int lw = font.MeasureWidth(TabLabels[i]);
            UIDraw.ShadowedText(sb, font, TabLabels[i],
                new Vector2(tabRect.X + (tabRect.Width - lw) / 2, tabRect.Y + (tabH - font.CharHeight) / 2),
                sel ? Color.White : UITheme.TextSecondary, UITheme.TextShadow);
        }
        cy += tabH + pad;

        // ── Divider ──
        sb.Draw(pixel, new Rectangle(panelX + pad, cy, panelW - pad * 2, scale), UITheme.PurpleMuted);
        cy += pad;

        // ── Tab content ──
        int contentTop = cy;
        int contentH = panelY + panelH - pad - contentTop;

        if (_tabIndex == 0)
            DrawSettingsTab(sb, pixel, font, panelX + pad, contentTop, panelW - pad * 2, contentH, scale);
        else
            DrawControlsTab(sb, pixel, font, panelX + pad, contentTop, panelW - pad * 2, contentH, scale);

        // Fade
        float fadeAlpha = _phase switch
        {
            Phase.FadeIn => 1f - _fadeTimer / FadeDuration,
            Phase.FadeOut => _fadeTimer / FadeDuration,
            _ => 0f,
        };
        if (fadeAlpha > 0f)
            sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * fadeAlpha);
    }

    private void DrawSettingsTab(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int scale)
    {
        font.Scale = scale;
        int lineH = font.CharHeight + 6 * scale;
        int radius = Math.Max(1, scale);

        for (int i = 0; i < SettingsOptions.Length; i++)
        {
            int iy = y + i * lineH;
            bool sel = i == _settingsIndex;

            if (sel)
            {
                var hlRect = new Rectangle(x, iy, w, lineH - 2 * scale);
                UIDraw.RoundedRect(sb, pixel, hlRect, radius, UITheme.PurpleSelected);
                UIDraw.GlowBorder(sb, pixel, hlRect, radius, UITheme.PurpleGlow);
            }

            int tw = font.MeasureWidth(SettingsOptions[i]);
            UIDraw.ShadowedText(sb, font, SettingsOptions[i],
                new Vector2(x + (w - tw) / 2, iy + (lineH - 2 * scale - font.CharHeight) / 2),
                sel ? Color.White : UITheme.TextPrimary, UITheme.TextShadow);
        }
    }

    private void DrawControlsTab(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int scale)
    {
        font.Scale = scale;
        int lineH = font.CharHeight + 3 * scale;
        int sectionGap = 4 * scale;
        int radius = Math.Max(1, scale);
        int cy = y;

        // Control sections
        DrawControlSection(sb, pixel, font, x, ref cy, w, lineH, sectionGap, radius, scale,
            "General", new[]
            {
                ("Tab", "Pause Menu"),
                ("Esc", "Cancel / Back"),
                ("Enter", "Confirm"),
            });

        DrawControlSection(sb, pixel, font, x, ref cy, w, lineH, sectionGap, radius, scale,
            "Overworld", new[]
            {
                ("WASD", "Move"),
                ("Shift", "Run"),
                ("Space", "Jump"),
                ("Q/E", "Camera"),
            });

        DrawControlSection(sb, pixel, font, x, ref cy, w, lineH, sectionGap, radius, scale,
            "Driving", new[]
            {
                ("W/S", "Accel / Brake"),
                ("A/D", "Steer"),
            });

        DrawControlSection(sb, pixel, font, x, ref cy, w, lineH, sectionGap, radius, scale,
            "Space Flight", new[]
            {
                ("WASD", "Fly"),
                ("Space", "Boost"),
                ("Scroll", "Zoom"),
            });

        DrawControlSection(sb, pixel, font, x, ref cy, w, lineH, sectionGap, radius, scale,
            "Battle", new[]
            {
                ("Arrows", "Navigate"),
                ("Enter", "Confirm"),
                ("Esc", "Cancel"),
            });
    }

    private static void DrawControlSection(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, ref int cy, int w, int lineH, int sectionGap, int radius, int scale,
        string title, (string key, string action)[] controls)
    {
        // Section header
        font.Scale = scale;
        UIDraw.ShadowedText(sb, font, title,
            new Vector2(x, cy), UITheme.PurpleAccent, UITheme.TextShadow);
        cy += lineH;

        // Key-action pairs
        int keyColW = w / 3;
        foreach (var (key, action) in controls)
        {
            // Key on the left
            font.Draw(key, x + scale * 2, cy, UITheme.TextSecondary);
            // Action on the right
            font.Draw(action, x + keyColW, cy, UITheme.TextPrimary);
            cy += lineH;
        }

        cy += sectionGap;
    }
}
