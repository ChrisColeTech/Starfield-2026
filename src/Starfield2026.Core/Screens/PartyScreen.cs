using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Pokemon;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.UI;

namespace Starfield2026.Core.Screens;

/// <summary>
/// Controls how the party screen behaves when opened.
/// </summary>
public enum PartyScreenMode
{
    /// <summary>View-only party screen from the pause menu.</summary>
    View,
    /// <summary>Battle switch-in: selecting a Pokémon returns its index.</summary>
    BattleSwitchIn,
}

/// <summary>
/// Modern party screen — dark slate gradient with electric purple accents.
/// Left cards for party slots, right side reserved for 3D model display.
/// Selecting a Pokémon opens a popup context menu (Switch In / Summary / Cancel).
/// </summary>
public class PartyScreen : IScreenOverlay
{
    private readonly Party _party;
    private readonly PartyScreenMode _mode;
    private int _selectedIndex;
    private bool _onBackButton;
    private readonly PopupModal _popup = new();

    private enum Phase { FadeIn, Active, FadeOut }
    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;
    private const float FadeDuration = 0.2f;

    // Stored layout rects for popup positioning
    private Rectangle[] _cardRects = new Rectangle[Party.MaxSize];

    public int SelectedSwitchIndex { get; private set; } = -1;
    public bool IsFinished { get; private set; }

    public PartyScreen(Party party, PartyScreenMode mode = PartyScreenMode.View)
    {
        _party = party;
        _mode = mode;
    }

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
        if (_popup.IsOpen)
        {
            _popup.Update(input);
            return;
        }

        if (input.Cancel) { SelectedSwitchIndex = -1; BeginExit(); return; }

        if (_onBackButton)
        {
            if (input.Up) { _onBackButton = false; return; }
            if (input.Confirm) { SelectedSwitchIndex = -1; BeginExit(); }
            return;
        }

        if (input.Up) _selectedIndex = Math.Max(0, _selectedIndex - 1);
        if (input.Down)
        {
            if (_selectedIndex >= _party.Count - 1) { _onBackButton = true; return; }
            _selectedIndex = Math.Min(_party.Count - 1, _selectedIndex + 1);
        }

        if (input.Confirm && _selectedIndex < _party.Count)
        {
            var anchor = _cardRects[_selectedIndex];
            string[] options = _mode == PartyScreenMode.BattleSwitchIn
                ? new[] { "Switch In", "Summary", "Cancel" }
                : new[] { "Summary", "Cancel" };

            _popup.ShowMenu("", options, anchor, idx =>
            {
                string choice = options[idx];
                if (choice == "Switch In")
                {
                    var pkmn = _party[_selectedIndex];
                    if (!pkmn.IsFainted) { SelectedSwitchIndex = _selectedIndex; BeginExit(); }
                }
                // "Summary" → TODO
                // "Cancel" → just closes
            }, () => { /* cancel closes popup */ });
        }
    }

    private void BeginExit() { _phase = Phase.FadeOut; _fadeTimer = 0f; }

    public void Draw(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int screenW, int screenH, int fontScale = 3)
    {
        int scale = UITheme.GetFontScale(screenW);
        font.Scale = scale;

        int pad = 8 * scale;
        int radius = Math.Max(2, scale * 2);
        int shadowOff = Math.Max(1, scale);

        // ── Background gradient ──
        UIDraw.VerticalGradient(sb, pixel, new Rectangle(0, 0, screenW, screenH),
            UITheme.GradTop, UITheme.GradBot);

        int leftW = (int)(screenW * 0.38f);

        // ── Title ──
        UIDraw.ShadowedText(sb, font, "Pokemon",
            new Vector2(pad, pad), UITheme.TextPrimary, UITheme.TextShadow);

        // ── Party cards ──
        int cardsTop = pad * 2 + font.CharHeight;
        int bottomReserve = font.CharHeight + pad * 3;
        int availableH = screenH - cardsTop - pad - bottomReserve;
        int cardSpacing = 3 * scale;
        int cardH = (availableH - cardSpacing * (Party.MaxSize - 1)) / Party.MaxSize;

        for (int i = 0; i < Party.MaxSize; i++)
        {
            int cardY = cardsTop + i * (cardH + cardSpacing);
            var cardRect = new Rectangle(pad, cardY, leftW, cardH);
            _cardRects[i] = cardRect;
            bool selected = !_popup.IsOpen && !_onBackButton && i == _selectedIndex;
            bool isEmpty = i >= _party.Count;

            if (isEmpty)
            {
                UIDraw.RoundedRect(sb, pixel, cardRect, scale, new Color(30, 34, 48, 80));
                continue;
            }

            var pkmn = _party[i];
            Color cardBg = pkmn.IsFainted ? UITheme.CardFainted :
                           (selected ? new Color(50, 40, 80, 220) : UITheme.SlateCard);

            UIDraw.ShadowedPanel(sb, pixel, cardRect, radius,
                cardBg, shadowOff, Color.Black * 0.3f);
            if (selected)
            {
                UIDraw.GlowBorder(sb, pixel, cardRect, radius, UITheme.PurpleGlow);
                UIDraw.RoundedRectOutline(sb, pixel, cardRect, radius, 2, UITheme.SelectionBorder);
            }

            // Sprite placeholder box
            int spriteSize = cardH - pad;
            var spriteRect = new Rectangle(cardRect.X + pad / 2, cardRect.Y + pad / 2, spriteSize, spriteSize);
            UIDraw.RoundedRect(sb, pixel, spriteRect, scale, new Color(20, 22, 32, 120));

            int cx = spriteRect.Right + pad;
            int cy = cardRect.Y + pad / 2;

            // Name + Gender
            font.Scale = scale;
            string name = pkmn.Nickname;
            UIDraw.ShadowedText(sb, font, name, new Vector2(cx, cy), UITheme.TextPrimary, UITheme.TextShadow);

            int nameW = font.MeasureWidth(name);
            if (pkmn.Gender != Gender.Unknown)
            {
                string gs = pkmn.Gender == Gender.Male ? "M" : "F";
                Color gc = pkmn.Gender == Gender.Male ? UITheme.GenderMale : UITheme.GenderFemale;
                font.Draw(gs, cx + nameW + font.CharWidth / 2, cy, gc);
            }

            // Level (right side)
            string lvText = $"Lv.{pkmn.Level}";
            int lvW = font.MeasureWidth(lvText);
            UIDraw.ShadowedText(sb, font, lvText,
                new Vector2(cardRect.Right - pad - lvW, cy), UITheme.TextSecondary, UITheme.TextShadow);

            // HP bar row
            int hpY = cy + font.CharHeight + 2 * scale;
            font.Scale = scale;
            font.Draw("HP", cx, hpY, UITheme.HPLabel);
            int hpLabelW = font.MeasureWidth("HP ");
            int barX = cx + hpLabelW;
            int barW = cardRect.Right - pad - barX;
            int barH = Math.Max(3, 4 * scale);
            int barY = hpY + (font.CharHeight - barH) / 2;
            HPBar.DrawTripleLine(sb, pixel, new Rectangle(barX, barY, barW, barH), pkmn.HPPercent);

            // HP numbers
            if (cardH > font.CharHeight * 3 + pad)
            {
                int numY = hpY + font.CharHeight + scale;
                string hpNum = $"{pkmn.CurrentHP}/{pkmn.MaxHP}";
                int numW = font.MeasureWidth(hpNum);
                font.Draw(hpNum, cardRect.Right - pad - numW, numY, UITheme.TextSecondary);
            }
        }

        // ── Right side: 3D model area ──
        int rightX = pad + leftW + pad;
        int rightW = screenW - rightX - pad;
        var modelArea = new Rectangle(rightX, cardsTop, rightW, availableH);
        UIDraw.RoundedRect(sb, pixel, modelArea, radius, new Color(30, 34, 48, 60));

        // ── Back button ──
        int buttonsH = font.CharHeight + pad * 2;
        int bottomY = screenH - pad - buttonsH;
        int btnW = Math.Min(50 * scale, leftW);
        var backRect = new Rectangle(pad, bottomY, btnW, buttonsH);
        bool backSel = _onBackButton && !_popup.IsOpen;

        UIDraw.ShadowedPanel(sb, pixel, backRect, radius,
            backSel ? UITheme.PurpleAccent : UITheme.SlateCard, shadowOff, Color.Black * 0.3f);
        if (backSel) UIDraw.GlowBorder(sb, pixel, backRect, radius, UITheme.PurpleGlow);

        font.Scale = scale;
        string backLabel = "Back";
        int blw = font.MeasureWidth(backLabel);
        UIDraw.ShadowedText(sb, font, backLabel,
            new Vector2(backRect.X + backRect.Width / 2 - blw / 2, backRect.Y + (buttonsH - font.CharHeight) / 2),
            backSel ? Color.White : UITheme.TextPrimary, UITheme.TextShadow);

        // ── Popup (via PopupModal) ──
        _popup.Draw(sb, pixel, font, scale, screenW, screenH);

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
}
