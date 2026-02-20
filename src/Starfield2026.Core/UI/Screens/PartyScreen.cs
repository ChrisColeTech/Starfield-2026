using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Items;
using Starfield2026.Core.Pokemon;
using Starfield2026.Core.UI.Fonts;

namespace Starfield2026.Core.UI.Screens;

public enum PartyScreenMode { PauseMenu, BattleSwitchIn }

/// <summary>
/// Full-screen party overlay. 2x3 grid of Pokemon cards with gradient background.
/// </summary>
public class PartyScreen : IScreenOverlay
{
    private enum Phase { FadeIn, Navigation, ActionPopup, FadeOut }

    private const float FadeDuration = 0.3f;
    private const int GridColumns = 2;
    private const int GridRows = 3;
    private const int CardSpacing = 8;
    private const int BottomSectionHeight = 60;
    private const int Padding = 20;

    // Card colors
    private static readonly Color CardFill = new(48, 48, 48, 196);
    private static readonly Color CardFillFainted = new(120, 30, 60, 224);
    private static readonly Color CardBorder = new(48, 180, 255, 200);
    private static readonly Color BackButtonNormal = new(48, 48, 48);
    private static readonly Color BackButtonSelected = new(96, 48, 48);

    // Gradient colors
    private static readonly Color PauseGradTop = new(222, 50, 60);
    private static readonly Color PauseGradMid = new(190, 40, 50);
    private static readonly Color PauseGradBot = new(255, 180, 200);
    private static readonly Color BattleGradTop = new(85, 0, 115);
    private static readonly Color BattleGradMid = new(145, 0, 195);
    private static readonly Color BattleGradBot = new(100, 65, 255);

    private readonly Party _party;
    private readonly PartyScreenMode _mode;

    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;
    private int _selectedIndex;
    private bool _onBackButton;

    // Action popup
    private readonly BattleMenuBox _actionMenu = new() { Columns = 1, UseStandardStyle = true };
    private int _actionTarget = -1;

    // Cached layout for mouse hit testing (set during Draw)
    private Rectangle[] _cardRects = Array.Empty<Rectangle>();
    private Rectangle _backRect;

    public bool IsFinished { get; private set; }

    /// <summary>Index of the Pokemon selected for switch-in, or -1 if cancelled.</summary>
    public int SelectedSwitchIndex { get; private set; } = -1;

    public PartyScreen(Party party, PartyScreenMode mode)
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
                if (_fadeTimer >= FadeDuration)
                {
                    _fadeTimer = FadeDuration;
                    _phase = Phase.Navigation;
                }
                break;

            case Phase.Navigation:
                UpdateNavigation(input);
                break;

            case Phase.ActionPopup:
                _actionMenu.Update(
                    input.Left, input.Right, input.Up, input.Down,
                    input.Confirm, input.Cancel, Point.Zero, false);
                break;

            case Phase.FadeOut:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration)
                    IsFinished = true;
                break;
        }
    }

    private void UpdateNavigation(InputSnapshot input)
    {
        if (input.Cancel)
        {
            BeginExit();
            return;
        }

        if (_onBackButton)
        {
            if (input.Up && _party.Count > 0)
            {
                _onBackButton = false;
                _selectedIndex = Math.Min(_party.Count - 1, (GridRows - 1) * GridColumns);
            }
            if (input.Confirm)
                BeginExit();
            return;
        }

        // Grid navigation
        int col = _selectedIndex % GridColumns;
        int row = _selectedIndex / GridColumns;

        if (input.Left && col > 0)
            _selectedIndex--;
        if (input.Right && col < GridColumns - 1 && _selectedIndex + 1 < _party.Count)
            _selectedIndex++;
        if (input.Up && row > 0)
            _selectedIndex -= GridColumns;
        if (input.Down)
        {
            int nextIdx = _selectedIndex + GridColumns;
            if (nextIdx < _party.Count)
                _selectedIndex = nextIdx;
            else
                _onBackButton = true;
        }

        if (input.Confirm && _selectedIndex < _party.Count)
            OpenActionPopup(_selectedIndex);
    }

    private void OpenActionPopup(int index)
    {
        _actionTarget = index;
            var items = _mode == PartyScreenMode.BattleSwitchIn
            ? new[]
            {
                new BattleMenuItem("Switch In", ConfirmSwitchIn),
                new BattleMenuItem("Summary"),
                new BattleMenuItem("Cancel", CloseActionPopup),
            }
            : new[]
            {
                new BattleMenuItem("Summary"),
                new BattleMenuItem("Cancel", CloseActionPopup),
            };
        _actionMenu.SetItems(items);
        _actionMenu.IsActive = true;
        _actionMenu.OnCancel = CloseActionPopup;
        _phase = Phase.ActionPopup;
    }

    private void ConfirmSwitchIn()
    {
        SelectedSwitchIndex = _actionTarget;
        _actionMenu.IsActive = false;
        BeginExit();
    }

    private void CloseActionPopup()
    {
        _actionMenu.IsActive = false;
        _actionTarget = -1;
        _phase = Phase.Navigation;
    }

    private void BeginExit()
    {
        _phase = Phase.FadeOut;
        _fadeTimer = 0f;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel,
                     KermFontRenderer? fontRenderer, KermFont? font,
                     SpriteFont fallbackFont, int screenWidth, int screenHeight, int fontScale = 3)
    {
        var fullRect = new Rectangle(0, 0, screenWidth, screenHeight);

        // Gradient background
        Color top, mid, bot;
        if (_mode == PartyScreenMode.PauseMenu)
            (top, mid, bot) = (PauseGradTop, PauseGradMid, PauseGradBot);
        else
            (top, mid, bot) = (BattleGradTop, BattleGradMid, BattleGradBot);
        UIStyle.DrawTripleGradient(sb, pixel, fullRect, top, mid, bot);

        // Calculate grid area
        int gridX = Padding;
        int gridY = Padding;
        int gridW = screenWidth - Padding * 2;
        int gridH = screenHeight - Padding * 2 - BottomSectionHeight;
        int cardW = (gridW - CardSpacing) / GridColumns;
        int cardH = (gridH - CardSpacing * (GridRows - 1)) / GridRows;

        // Draw Pokemon cards and cache rects for mouse hit testing
        if (_cardRects.Length != _party.Count)
            _cardRects = new Rectangle[_party.Count];

        for (int i = 0; i < _party.Count; i++)
        {
            int col = i % GridColumns;
            int row = i / GridColumns;
            int cx = gridX + col * (cardW + CardSpacing);
            int cy = gridY + row * (cardH + CardSpacing);
            var cardRect = new Rectangle(cx, cy, cardW, cardH);
            _cardRects[i] = cardRect;

            bool selected = !_onBackButton && i == _selectedIndex;
            DrawCard(sb, pixel, fontRenderer, font, fallbackFont, cardRect, _party[i], selected);
        }

        // Bottom section — Back button
        int backW = 120;
        int backH = 40;
        int backX = screenWidth - backW - Padding;
        int backY = screenHeight - BottomSectionHeight + (BottomSectionHeight - backH) / 2;
        var backRect = new Rectangle(backX, backY, backW, backH);
        _backRect = backRect;

        sb.Draw(pixel, backRect, _onBackButton ? BackButtonSelected : BackButtonNormal);
        if (_onBackButton)
        {
            // Selection border
            DrawBorder(sb, pixel, backRect, 2, CardBorder);
        }

        DrawText(sb, fontRenderer, fallbackFont, "Back",
            new Vector2(backX + backW / 2 - 30, backY + 8), Color.White, 3);

        // Action popup
        if (_phase == Phase.ActionPopup && _actionMenu.IsActive)
        {
            int popW = 160;
            int popH = _actionMenu.Items.Count * 40 + 16;
            int popX = screenWidth / 2 + 80;
            int popY = screenHeight / 4;
            if (fontRenderer != null && font != null)
                _actionMenu.Draw(sb, fontRenderer, font, pixel,
                    new Rectangle(popX, popY, popW, popH));
            else
                _actionMenu.Draw(sb, fallbackFont, pixel,
                    new Rectangle(popX, popY, popW, popH));
        }

        // Fade overlay
        float fadeAlpha = _phase switch
        {
            Phase.FadeIn => 1f - _fadeTimer / FadeDuration,
            Phase.FadeOut => _fadeTimer / FadeDuration,
            _ => 0f
        };
        if (fadeAlpha > 0f)
            sb.Draw(pixel, fullRect, Color.Black * fadeAlpha);
    }

    private void DrawCard(SpriteBatch sb, Texture2D pixel,
                          KermFontRenderer? fontRenderer, KermFont? font,
                          SpriteFont fallbackFont,
                          Rectangle rect, PartyPokemon pkmn, bool selected)
    {
        // Card fill
        sb.Draw(pixel, rect, pkmn.IsFainted ? CardFillFainted : CardFill);

        // Selection border
        if (selected)
            DrawBorder(sb, pixel, rect, 2, CardBorder);

        int pad = 10;
        int textY = rect.Y + pad;

        // Nickname
        DrawText(sb, fontRenderer, fallbackFont, pkmn.Nickname,
            new Vector2(rect.X + pad, textY), Color.White, 3);

        // Level (top-right)
        string lvText = $"Lv{pkmn.Level}";
        DrawText(sb, fontRenderer, fallbackFont, lvText,
            new Vector2(rect.Right - pad - lvText.Length * 10, textY), Color.White, 2);

        // HP bar
        int barY = textY + 32;
        int barW = rect.Width - pad * 2;
        UIStyle.DrawHPBar(sb, pixel, new Rectangle(rect.X + pad, barY, barW, 6), pkmn.HPPercent);

        // HP text
        string hpText = $"{pkmn.CurrentHP}/{pkmn.MaxHP}";
        DrawText(sb, fontRenderer, fallbackFont, hpText,
            new Vector2(rect.X + pad, barY + 10), new Color(200, 200, 200), 2);

        // Status
        if (pkmn.StatusAbbreviation != null)
        {
            DrawText(sb, fontRenderer, fallbackFont, pkmn.StatusAbbreviation,
                new Vector2(rect.X + pad + 100, barY + 10), new Color(255, 100, 100), 2);
        }

        // Held item
        if (pkmn.HeldItemId.HasValue)
        {
            var item = ItemRegistry.GetItem(pkmn.HeldItemId.Value);
            if (item != null)
            {
                DrawText(sb, fontRenderer, fallbackFont, item.Name,
                    new Vector2(rect.X + pad, barY + 26), new Color(180, 180, 220), 2);
            }
        }
    }

    private static void DrawText(SpriteBatch sb, KermFontRenderer? fontRenderer,
                                  SpriteFont fallbackFont, string text,
                                  Vector2 position, Color color, int scale)
    {
        if (fontRenderer != null)
            fontRenderer.DrawString(sb, text, position, scale, color);
        else
            sb.DrawString(fallbackFont, text, position, color);
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle r, int thickness, Color color)
    {
        sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Y, thickness, r.Height), color);
        sb.Draw(pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color);
    }
}
