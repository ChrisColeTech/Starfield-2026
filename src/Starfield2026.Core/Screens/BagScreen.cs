using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Items;
using Starfield2026.Core.Pokemon;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.UI;

namespace Starfield2026.Core.Screens;

/// <summary>
/// Modern bag screen — dark slate with electric purple accents.
/// Left party mini-display, right item list with pouch tabs.
/// </summary>
public class BagScreen : IScreenOverlay
{
    private readonly PlayerInventory _inventory;
    private readonly Party? _party;

    private static readonly ItemCategory[] PouchOrder =
    {
        ItemCategory.Medicine, ItemCategory.Pokeball, ItemCategory.Battle,
        ItemCategory.Berry, ItemCategory.KeyItem, ItemCategory.TM,
    };
    private static readonly string[] PouchLabels =
    {
        "Medicine", "Balls", "Battle", "Berries", "Key Items", "TMs",
    };
    private int _tabIndex;
    private int _itemIndex;
    private int _scrollOffset;

    private enum Focus { Tabs, Items, Party, Back }
    private Focus _focus = Focus.Tabs;
    private int _partyIndex;
    private readonly PopupModal _popup = new();
    private Rectangle[] _partyCardRects = new Rectangle[Party.MaxSize];

    private enum Phase { FadeIn, Active, FadeOut }
    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;
    private const float FadeDuration = 0.2f;

    public bool IsFinished { get; private set; }

    public BagScreen(PlayerInventory inventory, Party? party)
    {
        _inventory = inventory;
        _party = party;
    }

    private IReadOnlyList<InventorySlot> CurrentItems =>
        _inventory.GetPouch(PouchOrder[_tabIndex]);

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

        if (_focus == Focus.Party)
        {
            if (input.Cancel) { _focus = Focus.Items; return; }
            int maxIdx = (_party?.Count ?? 1); // partyIndex == Count means Cancel button
            if (input.Up) _partyIndex = Math.Max(0, _partyIndex - 1);
            if (input.Down) _partyIndex = Math.Min(maxIdx, _partyIndex + 1);
            if (input.Confirm)
            {
                if (_partyIndex >= (_party?.Count ?? 0))
                {
                    // Cancel button selected
                    _focus = Focus.Items;
                }
                else if (_party != null && _partyIndex < _party.Count)
                {
                    var slot = CurrentItems[_itemIndex];
                    var anchor = _partyCardRects[_partyIndex];
                    _popup.ShowQuantity($"How many?", slot.Quantity, anchor,
                        qty =>
                        {
                            // TODO: apply item effect ×qty to _party[_partyIndex], consume qty from slot
                            _focus = Focus.Items;
                        },
                        () => { /* cancel → back to party */ });
                }
            }
            return;
        }

        if (input.Cancel) { BeginExit(); return; }

        // Left/Right switches tabs from any focus
        if (input.Left)
        {
            if (_tabIndex > 0) { _tabIndex--; _itemIndex = 0; _scrollOffset = 0; _focus = Focus.Items; }
            else if (_focus != Focus.Back) { _focus = Focus.Back; }
        }
        if (input.Right)
        {
            if (_focus == Focus.Back) { _focus = CurrentItems.Count > 0 ? Focus.Items : Focus.Tabs; }
            else if (_tabIndex < PouchOrder.Length - 1) { _tabIndex++; _itemIndex = 0; _scrollOffset = 0; _focus = Focus.Items; }
        }

        switch (_focus)
        {
            case Focus.Tabs:
                if (input.Down) { _focus = CurrentItems.Count > 0 ? Focus.Items : Focus.Back; _itemIndex = 0; }
                break;
            case Focus.Items:
                var items = CurrentItems;
                if (input.Up && _itemIndex > 0) _itemIndex--;
                if (input.Down) { if (_itemIndex < items.Count - 1) _itemIndex++; else _focus = Focus.Back; }
                if (input.Confirm && _itemIndex < items.Count && _party != null && _party.Count > 0)
                {
                    _focus = Focus.Party;
                    _partyIndex = 0;
                }
                break;
            case Focus.Back:
                if (input.Up) _focus = Focus.Items;
                if (input.Confirm) BeginExit();
                break;
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

        // ── Title ──
        UIDraw.ShadowedText(sb, font, "Bag",
            new Vector2(pad, pad), UITheme.TextPrimary, UITheme.TextShadow);

        int contentTop = pad * 2 + font.CharHeight;
        int buttonsH = font.CharHeight + pad * 2;
        int contentH = screenH - contentTop - pad - buttonsH - pad;

        // ── Left: Party mini-display ──
        int leftW = (int)(screenW * 0.30f);
        DrawPartyMini(sb, pixel, font, pad, contentTop, leftW, contentH, scale);

        // ── Right panel ──
        int rightX = pad + leftW + pad;
        int rightW = screenW - rightX - pad;

        // ── Pouch tabs ──
        int tabH = font.CharHeight + pad;
        int tabGap = 2 * scale;
        int tabW = (rightW - tabGap * (PouchOrder.Length - 1)) / PouchOrder.Length;

        for (int i = 0; i < PouchOrder.Length; i++)
        {
            int tx = rightX + i * (tabW + tabGap);
            var tabRect = new Rectangle(tx, contentTop, tabW, tabH);
            bool sel = i == _tabIndex;

            UIDraw.RoundedRect(sb, pixel, tabRect, scale,
                sel ? UITheme.PurpleAccent : UITheme.SlateCard);
            if (sel) UIDraw.GlowBorder(sb, pixel, tabRect, scale, UITheme.PurpleGlow);

            font.Scale = Math.Max(1, scale - 1);
            string label = PouchLabels[i];
            int lw = font.MeasureWidth(label);
            Color tabText = sel ? Color.White : UITheme.TextSecondary;
            font.Draw(label, tabRect.X + (tabRect.Width - lw) / 2,
                tabRect.Y + (tabH - font.CharHeight) / 2, tabText);
        }

        // ── Item list ──
        int listTop = contentTop + tabH + 3 * scale;
        int descH = font.CharHeight * 2 + pad * 2;
        int listH = contentH - tabH - 3 * scale - descH - 3 * scale;
        var listRect = new Rectangle(rightX, listTop, rightW, listH);
        UIDraw.ShadowedPanel(sb, pixel, listRect, radius,
            UITheme.SlateCard, shadowOff, Color.Black * 0.3f);

        var items = CurrentItems;
        font.Scale = scale;
        int itemRowH = font.CharHeight + 4 * scale;
        int visibleItems = Math.Max(1, listH / itemRowH);

        if (_itemIndex < _scrollOffset) _scrollOffset = _itemIndex;
        if (_itemIndex >= _scrollOffset + visibleItems) _scrollOffset = _itemIndex - visibleItems + 1;

        if (items.Count == 0)
        {
            string empty = "No items";
            int ew = font.MeasureWidth(empty);
            font.Draw(empty, listRect.X + listRect.Width / 2 - ew / 2,
                listRect.Y + listRect.Height / 2 - font.CharHeight / 2, UITheme.TextDisabled);
        }
        else
        {
            for (int v = 0; v < visibleItems && v + _scrollOffset < items.Count; v++)
            {
                int i = v + _scrollOffset;
                var slot = items[i];
                var def = ItemRegistry.GetItem(slot.ItemId);
                string itemName = def?.Name ?? $"Item#{slot.ItemId}";

                int iy = listRect.Y + 2 * scale + v * itemRowH;
                var row = new Rectangle(listRect.X + 2 * scale, iy, listRect.Width - 4 * scale, itemRowH - scale);
                bool isSel = _focus == Focus.Items && i == _itemIndex;

                if (isSel)
                {
                    UIDraw.RoundedRect(sb, pixel, row, scale, UITheme.PurpleSelected);
                    UIDraw.GlowBorder(sb, pixel, row, scale, UITheme.PurpleGlow);
                    font.Scale = scale;
                    font.Draw(itemName, row.X + pad, row.Y + (row.Height - font.CharHeight) / 2, Color.White);
                }
                else
                {
                    font.Scale = scale;
                    font.Draw(itemName, row.X + pad, row.Y + (row.Height - font.CharHeight) / 2, UITheme.TextPrimary);
                }

                string qty = $"x{slot.Quantity}";
                int qw = font.MeasureWidth(qty);
                font.Draw(qty, row.Right - pad - qw, row.Y + (row.Height - font.CharHeight) / 2,
                    isSel ? Color.White : UITheme.TextSecondary);
            }
        }

        // ── Description panel ──
        int descY = listTop + listH + 3 * scale;
        var descRect = new Rectangle(rightX, descY, rightW, descH);
        UIDraw.ShadowedPanel(sb, pixel, descRect, radius,
            UITheme.SlateCard, shadowOff, Color.Black * 0.3f);

        if (_focus == Focus.Items && _itemIndex < items.Count)
        {
            var slot = items[_itemIndex];
            var def = ItemRegistry.GetItem(slot.ItemId);
            string desc = def?.Effect ?? def?.Name ?? "";
            font.Scale = scale;
            font.Draw(desc, descRect.X + pad, descRect.Y + pad, UITheme.TextPrimary);
        }

        // ── Back button ──
        int bottomY = screenH - pad - buttonsH;
        int btnW = Math.Min(50 * scale, rightW / 2);
        var backRect = new Rectangle(pad, bottomY, btnW, buttonsH);
        bool backSel = _focus == Focus.Back;
        bool backDisabled = _focus == Focus.Party || _popup.IsOpen;

        UIDraw.ShadowedPanel(sb, pixel, backRect, radius,
            backSel ? UITheme.PurpleAccent : (backDisabled ? UITheme.SlateCard * 0.4f : UITheme.SlateCard), shadowOff, Color.Black * 0.3f);
        if (backSel) UIDraw.GlowBorder(sb, pixel, backRect, radius, UITheme.PurpleGlow);

        font.Scale = scale;
        string backLabel = "Back";
        int blw = font.MeasureWidth(backLabel);
        UIDraw.ShadowedText(sb, font, backLabel,
            new Vector2(backRect.X + backRect.Width / 2 - blw / 2, backRect.Y + (buttonsH - font.CharHeight) / 2),
            backSel ? Color.White : (backDisabled ? UITheme.TextDisabled : UITheme.TextPrimary), UITheme.TextShadow);

        // ── Cancel button (visible during party selection) ──
        if (_focus == Focus.Party || _popup.IsOpen)
        {
            bool cancelSel = _focus == Focus.Party && _partyIndex >= (_party?.Count ?? 0);
            var cancelRect = new Rectangle(pad + btnW + pad, bottomY, btnW, buttonsH);
            UIDraw.ShadowedPanel(sb, pixel, cancelRect, radius,
                cancelSel ? UITheme.PurpleAccent : UITheme.SlateCard, shadowOff, Color.Black * 0.3f);
            if (cancelSel) UIDraw.GlowBorder(sb, pixel, cancelRect, radius, UITheme.PurpleGlow);

            font.Scale = scale;
            string cancelLabel = "Cancel";
            int clw = font.MeasureWidth(cancelLabel);
            UIDraw.ShadowedText(sb, font, cancelLabel,
                new Vector2(cancelRect.X + cancelRect.Width / 2 - clw / 2, cancelRect.Y + (buttonsH - font.CharHeight) / 2),
                cancelSel ? Color.White : UITheme.TextSecondary, UITheme.TextShadow);
        }

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

    private void DrawPartyMini(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int scale)
    {
        if (_party == null || _party.Count == 0) return;

        int pad = 4 * scale;
        int cardSpacing = 3 * scale;
        int cardH = (h - cardSpacing * (Party.MaxSize - 1)) / Party.MaxSize;
        int radius = Math.Max(1, scale);
        bool partyActive = _focus == Focus.Party && !_popup.IsOpen;

        for (int i = 0; i < Party.MaxSize; i++)
        {
            int cy = y + i * (cardH + cardSpacing);
            var cardRect = new Rectangle(x, cy, w, cardH);
            _partyCardRects[i] = cardRect;

            if (i >= _party.Count)
            {
                UIDraw.RoundedRect(sb, pixel, cardRect, radius, new Color(30, 34, 48, 80));
                continue;
            }

            bool sel = partyActive && i == _partyIndex;
            var pkmn = _party[i];

            UIDraw.RoundedRect(sb, pixel, cardRect, radius,
                sel ? UITheme.PurpleSelected : UITheme.SlateCard);
            if (sel)
            {
                UIDraw.GlowBorder(sb, pixel, cardRect, radius, UITheme.PurpleGlow);
                UIDraw.RoundedRectOutline(sb, pixel, cardRect, radius, 1, UITheme.SelectionBorder);
            }

            // Sprite placeholder
            int spriteSize = cardH - pad;
            var spriteRect = new Rectangle(cardRect.X + pad / 2, cardRect.Y + pad / 2, spriteSize, spriteSize);
            UIDraw.RoundedRect(sb, pixel, spriteRect, scale, new Color(20, 22, 32, 120));

            int cx = spriteRect.Right + pad;
            int textY = cardRect.Y + pad / 2;

            // Name + Gender (row 1)
            font.Scale = scale;
            string name = pkmn.Nickname;
            font.Draw(name, cx, textY, sel ? Color.White : UITheme.TextPrimary);

            int nameW = font.MeasureWidth(name);
            if (pkmn.Gender != Gender.Unknown)
            {
                string gs = pkmn.Gender == Gender.Male ? "M" : "F";
                Color gc = pkmn.Gender == Gender.Male ? UITheme.GenderMale : UITheme.GenderFemale;
                int gsW = font.MeasureWidth(gs);
                font.Draw(gs, cardRect.Right - pad - gsW, textY, gc);
            }

            // HP bar (row 2)
            int hpY = textY + font.CharHeight + scale;
            int barX = cx;
            int barW = cardRect.Right - pad - barX;
            int barH = Math.Max(2, 3 * scale);
            int barY = hpY + (font.CharHeight - barH) / 2;
            HPBar.DrawTripleLine(sb, pixel, new Rectangle(barX, barY, barW, barH), pkmn.HPPercent);

            // Row 3: HP numbers left, Level right
            if (cardH > font.CharHeight * 3 + pad)
            {
                int row3Y = hpY + font.CharHeight + scale;
                string hpNum = $"{pkmn.CurrentHP}/{pkmn.MaxHP}";
                font.Draw(hpNum, cx, row3Y, UITheme.TextSecondary);

                string lv = $"Lv.{pkmn.Level}";
                int lvW = font.MeasureWidth(lv);
                font.Draw(lv, cardRect.Right - pad - lvW, row3Y, UITheme.TextSecondary);
            }
        }
    }
}
