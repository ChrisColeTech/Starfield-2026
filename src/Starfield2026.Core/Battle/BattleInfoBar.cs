using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Pokemon;
using Starfield2026.Core.UI;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.Battle;

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
    private static readonly Color PanelBG = new(48, 48, 48, 200);
    private static readonly Color PanelBorder = new(80, 80, 80, 200);
    private static readonly Color SeparatorColor = new(100, 100, 100, 160);
    private static readonly Color NameColor = new(239, 239, 239);
    private static readonly Color LevelColor = new(180, 180, 180);
    private static readonly Color HPLabelColor = new(140, 200, 140);
    private static readonly Color HPTextColor = new(200, 200, 200);
    private static readonly Color GenderMale = new(80, 140, 255);
    private static readonly Color GenderFemale = new(255, 100, 130);

    /// <summary>
    /// Draw the foe's info bar (compact — no HP numbers, no EXP bar).
    /// </summary>
    public static void DrawFoeBar(SpriteBatch sb, Texture2D pixel,
        PixelFont uiFont,
        Rectangle bounds, BattlePokemon pkmn, int fontScale = 2)
    {
        int internalScale = Math.Max(1, fontScale - 1);
        uiFont.Scale = internalScale;
        DrawPanel(sb, pixel, bounds);

        int pad = 6 * fontScale;
        int fontH = uiFont.CharHeight;
        int charW = uiFont.CharWidth;

        // ── Row 1: Name + Gender (left), Level (right) ──
        int nameY = bounds.Y + pad;

        DrawText(sb, uiFont, pkmn.Nickname,
            new Vector2(bounds.X + pad, nameY), NameColor, internalScale);

        int nameWidth = uiFont.MeasureWidth(pkmn.Nickname);
        DrawGenderSymbol(sb, uiFont, pkmn.Gender,
            bounds.X + pad + nameWidth + charW / 2, nameY, internalScale);

        string lvText = $"Lv{pkmn.Level}";
        int lvWidth = uiFont.MeasureWidth(lvText);
        DrawText(sb, uiFont, lvText,
            new Vector2(bounds.Right - pad - lvWidth, nameY), LevelColor, internalScale);

        // ── Separator line ──
        int sepY = nameY + fontH + 2 * fontScale;
        sb.Draw(pixel, new Rectangle(bounds.X + pad, sepY, bounds.Width - pad * 2, 1), SeparatorColor);

        // ── Row 2: "HP" label + HP bar ──
        int hpRowY = sepY + 2 * fontScale;
        int hpLabelW = uiFont.MeasureWidth("HP "); // "HP" + small gap
        DrawText(sb, uiFont, "HP",
            new Vector2(bounds.X + pad, hpRowY), HPLabelColor, internalScale);

        int barX = bounds.X + pad + hpLabelW;
        int barW = bounds.Right - pad - barX;
        int barH = 3 * fontScale;
        int barY = hpRowY + (fontH - barH) / 2; // vertically center bar with label
        UIStyle.DrawTripleLineHPBar(sb, pixel,
            new Rectangle(barX, barY, barW, barH),
            pkmn.DisplayHPPercent);

        // ── Status (below HP bar, if set) ──
        if (pkmn.StatusAbbreviation != null)
        {
            DrawText(sb, uiFont, pkmn.StatusAbbreviation,
                new Vector2(bounds.X + pad, hpRowY + fontH + fontScale), new Color(255, 100, 100), internalScale);
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
        DrawPanel(sb, pixel, bounds);

        int pad = 6 * fontScale;
        int fontH = uiFont.CharHeight;
        int charW = uiFont.CharWidth;

        // ── Row 1: Name + Gender (left), Level (right) ──
        int nameY = bounds.Y + pad;

        DrawText(sb, uiFont, pkmn.Nickname,
            new Vector2(bounds.X + pad, nameY), NameColor, internalScale);

        int nameWidth = uiFont.MeasureWidth(pkmn.Nickname);
        DrawGenderSymbol(sb, uiFont, pkmn.Gender,
            bounds.X + pad + nameWidth + charW / 2, nameY, internalScale);

        string lvText = $"Lv{pkmn.Level}";
        int lvWidth = uiFont.MeasureWidth(lvText);
        DrawText(sb, uiFont, lvText,
            new Vector2(bounds.Right - pad - lvWidth, nameY), LevelColor, internalScale);

        // ── Separator line ──
        int sepY = nameY + fontH + 2 * fontScale;
        sb.Draw(pixel, new Rectangle(bounds.X + pad, sepY, bounds.Width - pad * 2, 1), SeparatorColor);

        // ── Row 2: "HP" label + HP bar ──
        int hpRowY = sepY + 2 * fontScale;
        int hpLabelW = uiFont.MeasureWidth("HP ");
        DrawText(sb, uiFont, "HP",
            new Vector2(bounds.X + pad, hpRowY), HPLabelColor, internalScale);

        int barX = bounds.X + pad + hpLabelW;
        int barW = bounds.Right - pad - barX;
        int barH = 2 * fontScale;
        int barY = hpRowY + (fontH - barH) / 2;
        UIStyle.DrawTripleLineHPBar(sb, pixel,
            new Rectangle(barX, barY, barW, barH),
            pkmn.DisplayHPPercent);

        // ── Row 3: HP numbers (right-aligned) ──
        int hpTextY = hpRowY + fontH + fontScale;
        string hpText = $"{(int)pkmn.DisplayHP}/{pkmn.MaxHP}";
        int hpTextWidth = uiFont.MeasureWidth(hpText);
        DrawText(sb, uiFont, hpText,
            new Vector2(bounds.Right - pad - hpTextWidth, hpTextY), HPTextColor, internalScale);

        // ── Status (left side of HP numbers row) ──
        if (pkmn.StatusAbbreviation != null)
        {
            DrawText(sb, uiFont, pkmn.StatusAbbreviation,
                new Vector2(bounds.X + pad, hpTextY), new Color(255, 100, 100), internalScale);
        }

        // ── EXP bar at bottom ──
        int expBarH = 2 * fontScale;
        int expY = bounds.Bottom - expBarH - pad / 2;
        UIStyle.DrawEXPBar(sb, pixel,
            new Rectangle(bounds.X + pad, expY, bounds.Width - pad * 2, expBarH),
            expPercent);
    }

    private static void DrawPanel(SpriteBatch sb, Texture2D pixel, Rectangle bounds)
    {
        // 1px border
        sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), PanelBorder);
        sb.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), PanelBorder);
        sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), PanelBorder);
        sb.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), PanelBorder);

        // Fill
        var fill = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
        sb.Draw(pixel, fill, PanelBG);
    }

    private static void DrawGenderSymbol(SpriteBatch sb,
        PixelFont uiFont, Gender gender, int x, int y, int scale)
    {
        if (gender == Gender.Unknown) return;
        string symbol = gender == Gender.Male ? "M" : "F";
        Color color = gender == Gender.Male ? GenderMale : GenderFemale;
        DrawText(sb, uiFont, symbol, new Vector2(x, y), color, scale);
    }

    private static void DrawText(SpriteBatch sb,
        PixelFont uiFont, string text, Vector2 position, Color color, int scale)
    {
        uiFont.Scale = scale;
        UIStyle.DrawShadowedText(sb, uiFont, text, position, color, Color.Black * 0.5f);
    }
}
