using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI.Screens;

/// <summary>
/// Two-level character select: pick a category (Characters/Pokemon), then scroll a list of model names.
/// </summary>
public class CharacterSelectScreen : IScreenOverlay
{
    private enum Phase { FadeIn, Navigation, FadeOut }
    private enum Level { Category, Items }

    private const float FadeDuration = 0.25f;
    private const int Padding = 20;
    private const int CardSpacing = 10;

    private readonly Category[] _categories;
    private Level _level = Level.Category;
    private int _catIndex;
    private int _itemIndex;
    private int _scrollOffset;
    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;

    private Rectangle[] _catCardRects = Array.Empty<Rectangle>();
    private readonly PopupModal _popup = new();

    public string? SelectedFolder { get; private set; }
    public bool IsFinished { get; private set; }

    public CharacterSelectScreen(string[] folders, string[] displayNames, string[] categories)
    {
        var groups = new Dictionary<string, List<(string folder, string name)>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < folders.Length; i++)
        {
            string f = folders[i];
            string n = i < displayNames.Length ? displayNames[i] : f;
            string cat = i < categories.Length ? categories[i] : "Other";
            if (!groups.TryGetValue(cat, out var list))
            {
                list = new List<(string, string)>();
                groups[cat] = list;
            }
            list.Add((f, n));
        }

        _categories = groups
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Category
            {
                Label = g.Key,
                Folders = g.Value.Select(x => x.folder).ToArray(),
                Names = g.Value.Select(x => x.name).ToArray(),
            })
            .ToArray();
    }

    public void Update(float deltaTime, InputSnapshot input)
    {
        switch (_phase)
        {
            case Phase.FadeIn:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration) { _fadeTimer = FadeDuration; _phase = Phase.Navigation; }
                break;
            case Phase.Navigation:
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

        if (input.Cancel)
        {
            if (_level == Level.Items) { _level = Level.Category; _itemIndex = 0; _scrollOffset = 0; }
            else { SelectedFolder = null; BeginExit(); }
            return;
        }

        if (_level == Level.Category)
            UpdateCategoryNav(input);
        else
            UpdateListNav(input);
    }

    private void UpdateCategoryNav(InputSnapshot input)
    {
        // Mouse click on category cards
        if (input.MouseClicked)
        {
            for (int i = 0; i < _catCardRects.Length && i < _categories.Length; i++)
            {
                if (_catCardRects[i].Contains(input.MousePosition))
                {
                    _catIndex = i;
                    EnterItems();
                    return;
                }
            }
        }

        // Mouse hover
        for (int i = 0; i < _catCardRects.Length && i < _categories.Length; i++)
        {
            if (_catCardRects[i].Contains(input.MousePosition))
                _catIndex = i;
        }

        // Keyboard
        if (input.Left && _catIndex > 0) _catIndex--;
        if (input.Right && _catIndex < _categories.Length - 1) _catIndex++;
        if (input.Up && _catIndex > 0) _catIndex--;
        if (input.Down && _catIndex < _categories.Length - 1) _catIndex++;

        if (input.Confirm && _categories.Length > 0)
            EnterItems();
    }

    private void UpdateListNav(InputSnapshot input)
    {
        var cat = _categories[_catIndex];
        int count = cat.Folders.Length;
        if (count == 0) return;

        // Keyboard
        if (input.Up && _itemIndex > 0) _itemIndex--;
        if (input.Down && _itemIndex < count - 1) _itemIndex++;

        if (input.Confirm)
            ShowSelectPopup(_itemIndex);
    }

    private void ShowSelectPopup(int itemIndex)
    {
        var cat = _categories[_catIndex];
        string name = itemIndex < cat.Names.Length ? cat.Names[itemIndex] : cat.Folders[itemIndex];
        string[] options = { "Select", "Cancel" };
        _popup.ShowMenu(name, options, Rectangle.Empty, idx =>
        {
            if (idx == 0) { SelectedFolder = cat.Folders[itemIndex]; BeginExit(); }
        }, () => { /* cancel closes */ });
    }

    private void EnterItems()
    {
        _level = Level.Items;
        _itemIndex = 0;
        _scrollOffset = 0;
    }

    private void BeginExit() { _phase = Phase.FadeOut; _fadeTimer = 0f; }

    public void Draw(SpriteBatch sb, Texture2D pixel,
                     PixelFont uiFont, int screenWidth, int screenHeight, int fontScale = 3)
    {
        uiFont.Scale = fontScale;
        int lineH = uiFont.CharHeight;
        int smallScale = Math.Max(1, fontScale * 2 / 3);
        int radius = Math.Max(2, fontScale * 2);
        int shadowOff = Math.Max(1, fontScale);

        var fullRect = new Rectangle(0, 0, screenWidth, screenHeight);
        UIDraw.VerticalGradient(sb, pixel, fullRect, UITheme.GradTop, UITheme.GradBot);

        if (_level == Level.Category)
            DrawCategories(sb, pixel, uiFont, screenWidth, screenHeight, fontScale, lineH, radius, shadowOff);
        else
            DrawItemList(sb, pixel, uiFont, screenWidth, screenHeight, fontScale, lineH, radius, shadowOff, smallScale);

        _popup.Draw(sb, pixel, uiFont, fontScale, screenWidth, screenHeight);

        // Fade
        float fadeAlpha = _phase switch
        {
            Phase.FadeIn => 1f - _fadeTimer / FadeDuration,
            Phase.FadeOut => _fadeTimer / FadeDuration,
            _ => 0f
        };
        if (fadeAlpha > 0f)
            sb.Draw(pixel, fullRect, Color.Black * fadeAlpha);
    }

    private void DrawCategories(SpriteBatch sb, Texture2D pixel, PixelFont uiFont,
        int sw, int sh, int fs, int lineH, int radius, int shadowOff)
    {
        DrawText(sb, uiFont, "SELECT CATEGORY", new Vector2(Padding, Padding), UITheme.TextPrimary, fs);

        int cardW = Math.Min(300 * fs / 3, (sw - Padding * 3) / Math.Max(1, _categories.Length));
        int cardH = 80 * fs / 3;
        int totalW = _categories.Length * cardW + (_categories.Length - 1) * CardSpacing;
        int startX = sw / 2 - totalW / 2;
        int startY = sh / 2 - cardH / 2;

        if (_catCardRects.Length < _categories.Length)
            _catCardRects = new Rectangle[_categories.Length];

        for (int i = 0; i < _categories.Length; i++)
        {
            int cx = startX + i * (cardW + CardSpacing);
            _catCardRects[i] = new Rectangle(cx, startY, cardW, cardH);
            bool selected = i == _catIndex;

            UIDraw.ShadowedPanel(sb, pixel, _catCardRects[i], radius,
                selected ? UITheme.PurpleSelected : UITheme.SlateCard, shadowOff, Color.Black * 0.3f);
            if (selected)
                UIDraw.GlowBorder(sb, pixel, _catCardRects[i], radius, UITheme.PurpleGlow);

            var cat = _categories[i];
            uiFont.Scale = fs;
            DrawText(sb, uiFont, cat.Label,
                new Vector2(cx + cardW / 2 - uiFont.MeasureWidth(cat.Label) / 2, startY + cardH / 2 - lineH),
                Color.White, fs);

            string countStr = $"{cat.Folders.Length} models";
            uiFont.Scale = Math.Max(1, fs * 2 / 3);
            DrawText(sb, uiFont, countStr,
                new Vector2(cx + cardW / 2 - uiFont.MeasureWidth(countStr) / 2, startY + cardH / 2 + 4 * fs / 3),
                UITheme.TextSecondary, Math.Max(1, fs * 2 / 3));
            uiFont.Scale = fs;
        }
    }

    private void DrawItemList(SpriteBatch sb, Texture2D pixel, PixelFont uiFont,
        int sw, int sh, int fs, int lineH, int radius, int shadowOff, int smallScale)
    {
        var cat = _categories[_catIndex];

        // Title + breadcrumb
        DrawText(sb, uiFont, cat.Label.ToUpperInvariant(), new Vector2(Padding, Padding), UITheme.TextPrimary, fs);
        uiFont.Scale = smallScale;
        DrawText(sb, uiFont, "< Esc to go back",
            new Vector2(Padding, Padding + lineH + 2 * fs / 3), UITheme.TextSecondary, smallScale);
        uiFont.Scale = fs;

        // List area
        int listTop = Padding + lineH * 2 + fs;
        int listBottom = sh - Padding;
        int rowH = lineH + fs * 2;
        int visibleCount = Math.Max(1, (listBottom - listTop) / rowH);
        int listW = sw - Padding * 2;

        // Keep selection visible
        if (_itemIndex < _scrollOffset) _scrollOffset = _itemIndex;
        if (_itemIndex >= _scrollOffset + visibleCount) _scrollOffset = _itemIndex - visibleCount + 1;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, cat.Folders.Length - visibleCount));

        for (int vi = 0; vi < visibleCount && _scrollOffset + vi < cat.Folders.Length; vi++)
        {
            int idx = _scrollOffset + vi;
            int y = listTop + vi * rowH;
            var rowRect = new Rectangle(Padding, y, listW, rowH - 2);
            bool selected = idx == _itemIndex;

            UIDraw.ShadowedPanel(sb, pixel, rowRect, radius,
                selected ? UITheme.PurpleSelected : UITheme.SlateCard, shadowOff, Color.Black * 0.2f);
            if (selected)
                UIDraw.GlowBorder(sb, pixel, rowRect, radius, UITheme.PurpleGlow);

            string name = idx < cat.Names.Length ? cat.Names[idx] : cat.Folders[idx];
            uiFont.Scale = fs;
            DrawText(sb, uiFont, name,
                new Vector2(Padding + fs * 3, y + (rowH - 2 - lineH) / 2), Color.White, fs);
        }

        // Scroll indicator
        if (cat.Folders.Length > visibleCount)
        {
            uiFont.Scale = smallScale;
            string scrollInfo = $"{_itemIndex + 1}/{cat.Folders.Length}";
            DrawText(sb, uiFont, scrollInfo,
                new Vector2(sw - Padding - uiFont.MeasureWidth(scrollInfo), listTop - lineH),
                UITheme.TextSecondary, smallScale);
            uiFont.Scale = fs;
        }
    }

    private static void DrawText(SpriteBatch sb, PixelFont uiFont, string text, Vector2 pos, Color color, float scale)
    {
        uiFont.Scale = (int)Math.Max(1, scale);
        UIDraw.ShadowedText(sb, uiFont, text, pos, color, UITheme.TextShadow);
    }

    private sealed class Category
    {
        public string Label = "";
        public string[] Folders = Array.Empty<string>();
        public string[] Names = Array.Empty<string>();
    }
}
