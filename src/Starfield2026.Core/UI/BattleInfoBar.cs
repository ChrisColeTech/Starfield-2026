using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Pokemon;
using Starfield2026.Core.UI;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI;

/// <summary>
/// Draws a battle info bar for one Pokemon (name, level, HP bar, optional HP text/EXP bar).
/// Layout:
///   Name ♂        Lv5
///   ─────────────────
///   HP ████████████
///   (ally only: HP numbers + EXP bar)
/// </summary>
public static class BattleInfoBar
{
    /// <summary>
    /// Draw the foe's info bar (compact — no HP numbers, no EXP bar).
    /// </summary>
    public static void DrawFoeBar(SpriteBatch sb, Texture2D pixel,
        PixelFont uiFont,
        Rectangle bounds, BattlePokemon pkmn, int fontScale = 2)
    {
        int internalScale = Math.Max(1, fontScale - 1);
        uiFont.Scale = internalScale;
        DrawPanel(sb, pixel, bounds, fontScale);

        int pad = 6 * fontScale;
        int fontH = uiFont.CharHeight;
        int charW = uiFont.CharWidth;

        // ── Row 1: Name + Gender (left), Level (right) ──
        int nameY = bounds.Y + pad;

        string name = pkmn.Nickname;
        DrawText(sb, uiFont, name,
            new Vector2(bounds.X + pad, nameY), UITheme.TextPrimary, internalScale);

        int nameWidth = uiFont.MeasureWidth(pkmn.Nickname);
        DrawGenderSymbol(sb, uiFont, pkmn.Gender,
            bounds.X + pad + nameWidth + charW / 2, nameY, internalScale);

        string lvText = $"Lv{pkmn.Level}";
        int lvWidth = uiFont.MeasureWidth(lvText);
        DrawText(sb, uiFont, lvText,
            new Vector2(bounds.Right - pad - lvWidth, nameY), UITheme.TextSecondary, internalScale);

        // ── Separator line ──
        int sepY = nameY + fontH + 2 * fontScale;
        sb.Draw(pixel, new Rectangle(bounds.X + pad, sepY, bounds.Width - pad * 2, 1), UITheme.PurpleMuted);

        // ── Row 2: "HP" label + HP bar ──
        int hpRowY = sepY + 2 * fontScale;
        int hpLabelW = uiFont.MeasureWidth("HP ");
        DrawText(sb, uiFont, "HP",
            new Vector2(bounds.X + pad, hpRowY), UITheme.HPLabel, internalScale);

        int barX = bounds.X + pad + hpLabelW;
        int barW = bounds.Right - pad - barX;
        int barH = 3 * fontScale;
        int barY = hpRowY + (fontH - barH) / 2;
        HPBar.DrawTripleLine(sb, pixel,
            new Rectangle(barX, barY, barW, barH),
            pkmn.DisplayHPPercent);

        // ── Status (below HP bar, if set) ──
        if (pkmn.StatusAbbreviation != null)
        {
            DrawText(sb, uiFont, pkmn.StatusAbbreviation,
                new Vector2(bounds.X + pad, hpRowY + fontH + fontScale), UITheme.StatusBad, internalScale);
        }
    }

    /// <summary>
    /// Draw the ally's info bar (full — HP numbers + EXP bar).
    /// </summary>
    public static void DrawAllyBar(SpriteBatch sb, Texture2D pixel,
        PixelFont uiFont,
        Rectangle bounds, BattlePokemon pkmn, float expPercent, int fontScale = 2)
    {
        int internalScale = Math.Max(1, fontScale - 1);
        uiFont.Scale = internalScale;
        DrawPanel(sb, pixel, bounds, fontScale);

        int pad = 6 * fontScale;
        int fontH = uiFont.CharHeight;
        int charW = uiFont.CharWidth;

        // ── Row 1: Name + Gender (left), Level (right) ──
        int nameY = bounds.Y + pad;

        string name = pkmn.Nickname;
        DrawText(sb, uiFont, name,
            new Vector2(bounds.X + pad, nameY), UITheme.TextPrimary, internalScale);

        int nameWidth = uiFont.MeasureWidth(pkmn.Nickname);
        DrawGenderSymbol(sb, uiFont, pkmn.Gender,
            bounds.X + pad + nameWidth + charW / 2, nameY, internalScale);

        string lvText = $"Lv{pkmn.Level}";
        int lvWidth = uiFont.MeasureWidth(lvText);
        DrawText(sb, uiFont, lvText,
            new Vector2(bounds.Right - pad - lvWidth, nameY), UITheme.TextSecondary, internalScale);

        // ── Separator line ──
        int sepY = nameY + fontH + 2 * fontScale;
        sb.Draw(pixel, new Rectangle(bounds.X + pad, sepY, bounds.Width - pad * 2, 1), UITheme.PurpleMuted);

        // ── Row 2: "HP" label + HP bar ──
        int hpRowY = sepY + 2 * fontScale;
        int hpLabelW = uiFont.MeasureWidth("HP ");
        DrawText(sb, uiFont, "HP",
            new Vector2(bounds.X + pad, hpRowY), UITheme.HPLabel, internalScale);

        int barX = bounds.X + pad + hpLabelW;
        int barW = bounds.Right - pad - barX;
        int barH = 2 * fontScale;
        int barY = hpRowY + (fontH - barH) / 2;
        HPBar.DrawTripleLine(sb, pixel,
            new Rectangle(barX, barY, barW, barH),
            pkmn.DisplayHPPercent);

        // ── Row 3: HP numbers (right-aligned) ──
        int hpTextY = hpRowY + fontH + fontScale;
        string hpText = $"{(int)pkmn.DisplayHP}/{pkmn.MaxHP}";
        int hpTextWidth = uiFont.MeasureWidth(hpText);
        DrawText(sb, uiFont, hpText,
            new Vector2(bounds.Right - pad - hpTextWidth, hpTextY), UITheme.TextSecondary, internalScale);

        // ── Status (left side of HP numbers row) ──
        if (pkmn.StatusAbbreviation != null)
        {
            DrawText(sb, uiFont, pkmn.StatusAbbreviation,
                new Vector2(bounds.X + pad, hpTextY), UITheme.StatusBad, internalScale);
        }

        // ── EXP bar at bottom ──
        int expBarH = 2 * fontScale;
        int expY = bounds.Bottom - expBarH - pad / 2;
        EXPBar.Draw(sb, pixel,
            new Rectangle(bounds.X + pad, expY, bounds.Width - pad * 2, expBarH),
            expPercent);
    }

    private static void DrawPanel(SpriteBatch sb, Texture2D pixel, Rectangle bounds, int fontScale)
    {
        int radius = Math.Max(2, fontScale * 2);
        int shadowOff = Math.Max(1, fontScale);
        UIDraw.ShadowedPanel(sb, pixel, bounds, radius,
            UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);
    }

    private static void DrawGenderSymbol(SpriteBatch sb,
        PixelFont uiFont, Gender gender, int x, int y, int scale)
    {
        if (gender == Gender.Unknown) return;
        string symbol = gender == Gender.Male ? "M" : "F";
        Color color = gender == Gender.Male ? UITheme.GenderMale : UITheme.GenderFemale;
        DrawText(sb, uiFont, symbol, new Vector2(x, y), color, scale);
    }

    private static void DrawText(SpriteBatch sb,
        PixelFont uiFont, string text, Vector2 position, Color color, int scale)
    {
        uiFont.Scale = scale;
        UIDraw.ShadowedText(sb, uiFont, text, position, color, UITheme.TextShadow);
    }
}
