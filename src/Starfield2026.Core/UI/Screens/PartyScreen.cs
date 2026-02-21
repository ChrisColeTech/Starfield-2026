using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Items;
using Starfield2026.Core.Pokemon;
using Starfield2026.Core.Rendering;

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
    private readonly MenuBox _actionMenu = new() { Columns = 1 };
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
                _actionMenu.Update(input);
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
                new MenuItem("Switch In", ConfirmSwitchIn),
                new MenuItem("Summary"),
                new MenuItem("Cancel", CloseActionPopup),
            }
            : new[]
            {
                new MenuItem("Summary"),
                new MenuItem("Cancel", CloseActionPopup),
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
                     PixelFont uiFont, int screenWidth, int screenHeight, int fontScale = 3)
    {
        uiFont.Scale = fontScale;
        var fullRect = new Rectangle(0, 0, screenWidth, screenHeight);

        int pad = 10 * fontScale;
        int cardSp = 4 * fontScale;
        int botH = 20 * fontScale;

        // Gradient background
        Color top, mid, bot;
        if (_mode == PartyScreenMode.PauseMenu)
            (top, mid, bot) = (PauseGradTop, PauseGradMid, PauseGradBot);
        else
            (top, mid, bot) = (BattleGradTop, BattleGradMid, BattleGradBot);
        UIStyle.DrawTripleGradient(sb, pixel, fullRect, top, mid, bot);

        // Calculate grid area
        int gridX = pad;
        int gridY = pad;
        int gridW = screenWidth - pad * 2;
        int gridH = screenHeight - pad * 2 - botH;
        int cardW = (gridW - cardSp) / GridColumns;
        int cardH = (gridH - cardSp * (GridRows - 1)) / GridRows;

        // Draw Pokemon cards and cache rects for mouse hit testing
        if (_cardRects.Length != _party.Count)
            _cardRects = new Rectangle[_party.Count];

        for (int i = 0; i < _party.Count; i++)
        {
            int col = i % GridColumns;
            int row = i / GridColumns;
            int cx = gridX + col * (cardW + cardSp);
            int cy = gridY + row * (cardH + cardSp);
            var cardRect = new Rectangle(cx, cy, cardW, cardH);
            _cardRects[i] = cardRect;

            bool selected = !_onBackButton && i == _selectedIndex;
            DrawCard(sb, pixel, uiFont, cardRect, _party[i], selected, fontScale);
        }

        // Bottom section — Back button
        int backW = 80 * fontScale;
        int backH = 28 * fontScale;
        int backX = screenWidth - backW - pad;
        int backY = screenHeight - botH + (botH - backH) / 2;
        var backRect = new Rectangle(backX, backY, backW, backH);
        _backRect = backRect;

        sb.Draw(pixel, backRect, _onBackButton ? BackButtonSelected : BackButtonNormal);
        if (_onBackButton)
        {
            // Selection border
            DrawBorder(sb, pixel, backRect, 2 * fontScale, CardBorder);
        }

        DrawText(sb, uiFont, "Back",
            new Vector2(backX + backW / 2 - uiFont.MeasureWidth("Back") / 2, backY + 4 * fontScale), Color.White, fontScale);

        // Action popup
        if (_phase == Phase.ActionPopup && _actionMenu.IsActive)
        {
            int popW = 80 * fontScale;
            int popH = _actionMenu.Items.Count * 20 * fontScale + 8 * fontScale;
            int popX = screenWidth / 2 + 40 * fontScale;
            int popY = screenHeight / 4;
            _actionMenu.Draw(sb, uiFont, pixel,
                new Rectangle(popX, popY, popW, popH), fontScale);
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
                          PixelFont uiFont,
                          Rectangle rect, PartyPokemon pkmn, bool selected, int fontScale)
    {
        uiFont.Scale = fontScale;

        // Card fill
        sb.Draw(pixel, rect, pkmn.IsFainted ? CardFillFainted : CardFill);

        // Selection border
        if (selected)
            DrawBorder(sb, pixel, rect, 2 * fontScale, CardBorder);

        int pad = Math.Max(2, rect.Height / 16);

        // ── LEFT SIDE: Sprite placeholder + Level ──
        int spriteSize = rect.Height - pad * 2;
        int leftW = spriteSize;
        int spriteX = rect.X + pad;
        int spriteY = rect.Y + pad;

        // Placeholder box for sprite (dark square)
        int placeholderSize = (int)(spriteSize * 0.65f);
        int phX = spriteX + (leftW - placeholderSize) / 2;
        int phY = spriteY;
        sb.Draw(pixel, new Rectangle(phX, phY, placeholderSize, placeholderSize), new Color(30, 30, 30, 180));
        DrawBorder(sb, pixel, new Rectangle(phX, phY, placeholderSize, placeholderSize), Math.Max(1, fontScale / 2), new Color(80, 80, 80, 120));

        // Level — centered below placeholder
        int lvScale = Math.Max(1, (int)(fontScale * 0.75f));
        uiFont.Scale = lvScale;
        string lvText = $"Lv{pkmn.Level}";
        int lvW = uiFont.MeasureWidth(lvText);
        int lvY = phY + placeholderSize + pad / 2;
        DrawText(sb, uiFont, lvText,
            new Vector2(spriteX + (leftW - lvW) / 2, lvY), new Color(200, 200, 200), lvScale);
        uiFont.Scale = fontScale;

        // ── RIGHT SIDE: Name + Gender, HP bar, HP text ──
        int rightX = rect.X + pad + leftW + pad;
        int rightW = rect.Right - pad - rightX;
        int rightY = rect.Y + pad;
        int rightH = rect.Height - pad * 2;
        int rowH = rightH / 4; // 4 rows: name, hp bar, hp text, status/item

        // Row 1: Name + Gender
        string name = pkmn.Nickname;
        DrawText(sb, uiFont, name,
            new Vector2(rightX, rightY), Color.White, fontScale);

        // Gender icon next to name
        if (pkmn.Gender != Pokemon.Gender.Unknown)
        {
            int nameW = uiFont.MeasureWidth(name);
            string genderSym = pkmn.Gender == Pokemon.Gender.Male ? "M" : "F";
            Color genderColor = pkmn.Gender == Pokemon.Gender.Male
                ? new Color(80, 140, 255) : new Color(255, 100, 130);
            DrawText(sb, uiFont, genderSym,
                new Vector2(rightX + nameW + uiFont.CharWidth / 2, rightY), genderColor, fontScale);
        }

        // Row 2: HP bar
        int barY = rightY + rowH;
        int barH = Math.Max(2, rowH / 3);
        UIStyle.DrawHPBar(sb, pixel, new Rectangle(rightX, barY, rightW, barH), pkmn.HPPercent);

        // Row 3: HP text
        float smallScale = Math.Max(1f, fontScale * 0.75f);
        int hpY = barY + barH + pad / 2;
        string hpText = $"{pkmn.CurrentHP}/{pkmn.MaxHP}";
        DrawText(sb, uiFont, hpText,
            new Vector2(rightX, hpY), new Color(200, 200, 200), smallScale);

        // Status (next to HP text)
        if (pkmn.StatusAbbreviation != null)
        {
            uiFont.Scale = (int)Math.Max(1, smallScale);
            int hpTextW = uiFont.MeasureWidth(hpText);
            DrawText(sb, uiFont, pkmn.StatusAbbreviation,
                new Vector2(rightX + hpTextW + uiFont.CharWidth, hpY), new Color(255, 100, 100), smallScale);
            uiFont.Scale = fontScale;
        }

        // Row 4: Held item
        if (pkmn.HeldItemId.HasValue)
        {
            var item = ItemRegistry.GetItem(pkmn.HeldItemId.Value);
            if (item != null)
            {
                int itemY = hpY + (int)(uiFont.CharHeight * smallScale / fontScale) + pad / 2;
                DrawText(sb, uiFont, item.Name,
                    new Vector2(rightX, itemY), new Color(180, 180, 220), smallScale);
            }
        }
    }

    private static void DrawText(SpriteBatch sb, PixelFont uiFont, string text,
                                  Vector2 position, Color color, float scale)
    {
        uiFont.Scale = (int)Math.Max(1, scale);
        UIStyle.DrawShadowedText(sb, uiFont, text, position, color, Color.Black * 0.5f);
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle r, int thickness, Color color)
    {
        sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Y, thickness, r.Height), color);
        sb.Draw(pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color);
    }
}
