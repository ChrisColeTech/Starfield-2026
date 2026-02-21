using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI.Screens;

/// <summary>
/// Two-level character select: pick a category, then pick a character.
/// Categories are auto-grouped by folder prefix (tr → Trainers, pm → Pokemon, etc.).
/// </summary>
public class CharacterSelectScreen : IScreenOverlay
{
    private enum Phase { FadeIn, Navigation, FadeOut }
    private enum Level { Category, Items }

    private const float FadeDuration = 0.25f;
    private const int GridColumns = 4;
    private const int GridRows = 3;
    private const int ItemsPerPage = GridColumns * GridRows;
    private const int CardSpacing = 10;
    private const int BottomHeight = 50;
    private const int Padding = 20;

    private readonly Category[] _categories;
    private Level _level = Level.Category;
    private int _catIndex;
    private int _itemIndex;
    private int _page;
    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;

    private enum BottomFocus { None, Prev, Next, Back }
    private BottomFocus _bottomFocus = BottomFocus.None;

    private Rectangle[] _cardRects = Array.Empty<Rectangle>();
    private Rectangle _prevRect, _nextRect, _backRect;
    private readonly PopupModal _popup = new();

    public string? SelectedFolder { get; private set; }
    public bool IsFinished { get; private set; }

    public CharacterSelectScreen(string[] folders, string[] displayNames)
    {
        var groups = new Dictionary<string, List<(string folder, string name)>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < folders.Length; i++)
        {
            string f = folders[i];
            string n = i < displayNames.Length ? displayNames[i] : f;
            string prefix = GetPrefix(f);
            if (!groups.TryGetValue(prefix, out var list))
            {
                list = new List<(string, string)>();
                groups[prefix] = list;
            }
            list.Add((f, n));
        }

        var cats = new List<Category>();
        foreach (var kvp in groups.OrderBy(g => CategorySortKey(g.Key)))
        {
            cats.Add(new Category
            {
                Prefix = kvp.Key,
                Label = GetCategoryLabel(kvp.Key),
                Folders = kvp.Value.Select(x => x.folder).ToArray(),
                Names = kvp.Value.Select(x => x.name).ToArray(),
            });
        }
        _categories = cats.ToArray();
    }

    private static string GetPrefix(string folder)
    {
        int i = 0;
        while (i < folder.Length && char.IsLetter(folder[i])) i++;
        string prefix = i > 0 ? folder[..i] : "other";
        return prefix.ToLowerInvariant();
    }

    private static string GetCategoryLabel(string prefix) => prefix switch
    {
        "tr" => "Trainers",
        "pm" => "Pokemon",
        "ob" => "Objects",
        "group" => "Other Models",
        _ => prefix.ToUpperInvariant(),
    };

    private static int CategorySortKey(string prefix) => prefix switch
    {
        "tr" => 0,
        "pm" => 1,
        "ob" => 2,
        "group" => 3,
        _ => 4,
    };

    private Category CurrentCat => _categories[_catIndex];
    private int TotalPages => Math.Max(1, (int)Math.Ceiling(CurrentCat.Folders.Length / (double)ItemsPerPage));
    private int PageStart => _page * ItemsPerPage;
    private int PageCount => Math.Min(ItemsPerPage, CurrentCat.Folders.Length - PageStart);

    private int CatPages => Math.Max(1, (int)Math.Ceiling(_categories.Length / (double)ItemsPerPage));
    private int CatPageStart => _page * ItemsPerPage;
    private int CatPageCount => Math.Min(ItemsPerPage, _categories.Length - CatPageStart);

    private int VisibleCount => _level == Level.Category ? CatPageCount : PageCount;
    private int CurrentTotalPages => _level == Level.Category ? CatPages : TotalPages;

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
            if (_level == Level.Items) { _level = Level.Category; _page = 0; _itemIndex = 0; _bottomFocus = BottomFocus.None; }
            else { SelectedFolder = null; BeginExit(); }
            return;
        }

        if (input.PageLeft) ChangePage(-1);
        if (input.PageRight) ChangePage(1);

        int selIdx = _level == Level.Category ? _catIndex : _itemIndex;
        int count = VisibleCount;

        // Mouse
        if (input.MouseClicked)
        {
            for (int i = 0; i < _cardRects.Length && i < count; i++)
            {
                if (_cardRects[i].Contains(input.MousePosition))
                {
                    _bottomFocus = BottomFocus.None;
                    if (_level == Level.Category) { _catIndex = CatPageStart + i; EnterItems(); }
                    else { ShowSelectPopup(PageStart + i); }
                    return;
                }
            }
            if (_prevRect.Contains(input.MousePosition)) { ChangePage(-1); return; }
            if (_nextRect.Contains(input.MousePosition)) { ChangePage(1); return; }
            if (_backRect.Contains(input.MousePosition))
            {
                if (_level == Level.Items) { _level = Level.Category; _page = 0; _itemIndex = 0; _bottomFocus = BottomFocus.None; }
                else { SelectedFolder = null; BeginExit(); }
                return;
            }
        }

        // Mouse hover
        if (!input.MouseClicked)
        {
            for (int i = 0; i < _cardRects.Length && i < count; i++)
            {
                if (_cardRects[i].Contains(input.MousePosition))
                {
                    if (_level == Level.Category) _catIndex = CatPageStart + i; else _itemIndex = i;
                    _bottomFocus = BottomFocus.None;
                }
            }
            if (_prevRect.Contains(input.MousePosition)) _bottomFocus = BottomFocus.Prev;
            else if (_nextRect.Contains(input.MousePosition)) _bottomFocus = BottomFocus.Next;
            else if (_backRect.Contains(input.MousePosition)) _bottomFocus = BottomFocus.Back;
        }

        // Bottom row keyboard nav
        if (_bottomFocus != BottomFocus.None)
        {
            if (input.Up && count > 0)
            {
                _bottomFocus = BottomFocus.None;
                int idx = Math.Min(count - 1, (GridRows - 1) * GridColumns);
                if (_level == Level.Category) _catIndex = CatPageStart + idx; else _itemIndex = idx;
                return;
            }
            if (input.Left)
            {
                _bottomFocus = _bottomFocus switch { BottomFocus.Back => BottomFocus.Next, BottomFocus.Next => BottomFocus.Prev, _ => _bottomFocus };
                return;
            }
            if (input.Right)
            {
                _bottomFocus = _bottomFocus switch { BottomFocus.Prev => BottomFocus.Next, BottomFocus.Next => BottomFocus.Back, _ => _bottomFocus };
                return;
            }
            if (input.Confirm)
            {
                if (_bottomFocus == BottomFocus.Prev) ChangePage(-1);
                else if (_bottomFocus == BottomFocus.Next) ChangePage(1);
                else if (_bottomFocus == BottomFocus.Back)
                {
                    if (_level == Level.Items) { _level = Level.Category; _page = 0; _itemIndex = 0; _bottomFocus = BottomFocus.None; }
                    else { SelectedFolder = null; BeginExit(); }
                }
            }
            return;
        }

        // Grid nav
        int localIdx = _level == Level.Category ? _catIndex - CatPageStart : _itemIndex;
        int col = localIdx % GridColumns;
        int row = localIdx / GridColumns;

        if (input.Left && col > 0) localIdx--;
        if (input.Right && col < GridColumns - 1 && localIdx + 1 < count) localIdx++;
        if (input.Up && row > 0) localIdx -= GridColumns;
        if (input.Down)
        {
            int next = localIdx + GridColumns;
            if (next < count) localIdx = next;
            else { _bottomFocus = BottomFocus.Prev; return; }
        }

        if (_level == Level.Category) _catIndex = CatPageStart + localIdx; else _itemIndex = localIdx;

        if (input.Confirm && localIdx < count)
        {
            if (_level == Level.Category) { _catIndex = CatPageStart + localIdx; EnterItems(); }
            else { ShowSelectPopup(PageStart + localIdx); }
        }
    }

    private void ShowSelectPopup(int folderIndex)
    {
        var anchor = _cardRects[_level == Level.Category ? _catIndex - CatPageStart : _itemIndex];
        string name = folderIndex < CurrentCat.Names.Length ? CurrentCat.Names[folderIndex] : CurrentCat.Folders[folderIndex];
        string[] options = { "Select", "Cancel" };
        _popup.ShowMenu(name, options, anchor, idx =>
        {
            if (idx == 0) { SelectedFolder = CurrentCat.Folders[folderIndex]; BeginExit(); }
        }, () => { /* cancel closes */ });
    }

    private void EnterItems()
    {
        _level = Level.Items;
        _page = 0;
        _itemIndex = 0;
        _bottomFocus = BottomFocus.None;
    }

    private void ChangePage(int delta)
    {
        int total = CurrentTotalPages;
        int newPage = Math.Clamp(_page + delta, 0, total - 1);
        if (newPage == _page) return;
        _page = newPage;
        int count = VisibleCount;
        int localIdx = _level == Level.Category ? _catIndex - CatPageStart : _itemIndex;
        if (localIdx >= count)
        {
            if (_level == Level.Category) _catIndex = CatPageStart + count - 1; else _itemIndex = count - 1;
        }
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

        // Title
        string title;
        if (_level == Level.Category)
            title = "SELECT CATEGORY";
        else
            title = $"{CurrentCat.Label.ToUpperInvariant()}  ({_page + 1}/{TotalPages})  [{CurrentCat.Folders.Length}]";
        DrawText(sb, uiFont, title, new Vector2(Padding, Padding - 4 * fontScale), UITheme.TextPrimary, fontScale);

        // Breadcrumb for item level
        if (_level == Level.Items)
        {
            DrawText(sb, uiFont, "< Esc to go back",
                new Vector2(Padding, Padding + lineH + 2 * fontScale), UITheme.TextSecondary, smallScale);
        }

        // Grid
        int gridY = Padding + (_level == Level.Items ? lineH * 2 + 4 : lineH + 12);
        int bottomBarH = 14 * fontScale;
        int gridW = screenWidth - Padding * 2;
        int gridH = screenHeight - gridY - Padding - bottomBarH;
        int cardW = (gridW - CardSpacing * (GridColumns - 1)) / GridColumns;
        int cardH = (gridH - CardSpacing * (GridRows - 1)) / GridRows;

        int count = VisibleCount;
        if (_cardRects.Length < ItemsPerPage)
            _cardRects = new Rectangle[ItemsPerPage];

        for (int i = 0; i < count; i++)
        {
            int c = i % GridColumns;
            int r = i / GridColumns;
            int cx = Padding + c * (cardW + CardSpacing);
            int cy = gridY + r * (cardH + CardSpacing);
            _cardRects[i] = new Rectangle(cx, cy, cardW, cardH);

            int localSel = _level == Level.Category ? _catIndex - CatPageStart : _itemIndex;
            bool selected = _bottomFocus == BottomFocus.None && i == localSel;

            UIDraw.ShadowedPanel(sb, pixel, _cardRects[i], radius,
                selected ? UITheme.PurpleSelected : UITheme.SlateCard, shadowOff, Color.Black * 0.3f);
            if (selected)
                UIDraw.GlowBorder(sb, pixel, _cardRects[i], radius, UITheme.PurpleGlow);

            if (_level == Level.Category)
            {
                int gi = CatPageStart + i;
                var cat = _categories[gi];
                uiFont.Scale = fontScale;
                DrawText(sb, uiFont, cat.Label,
                    new Vector2(cx + cardW / 2 - uiFont.MeasureWidth(cat.Label) / 2, cy + cardH / 2 - lineH), Color.White, fontScale);
                string countStr = $"{cat.Folders.Length} models";
                uiFont.Scale = smallScale;
                DrawText(sb, uiFont, countStr,
                    new Vector2(cx + cardW / 2 - uiFont.MeasureWidth(countStr) / 2, cy + cardH / 2 + 4 * fontScale), UITheme.TextSecondary, smallScale);
                DrawText(sb, uiFont, cat.Prefix,
                    new Vector2(cx + 8 * fontScale, cy + cardH - lineH - 4 * fontScale), UITheme.TextDisabled, smallScale);
                uiFont.Scale = fontScale;
            }
            else
            {
                int gi = PageStart + i;
                string name = gi < CurrentCat.Names.Length ? CurrentCat.Names[gi] : CurrentCat.Folders[gi];
                uiFont.Scale = fontScale;
                DrawText(sb, uiFont, name,
                    new Vector2(cx + cardW / 2 - uiFont.MeasureWidth(name) / 2, cy + cardH / 2 - lineH / 2), Color.White, fontScale);
                uiFont.Scale = smallScale;
                DrawText(sb, uiFont, CurrentCat.Folders[gi],
                    new Vector2(cx + 8 * fontScale, cy + cardH - lineH - 4 * fontScale), UITheme.TextDisabled, smallScale);
                uiFont.Scale = fontScale;
            }
        }

        // Bottom bar
        int btnW = 32 * fontScale;
        int btnH = 10 * fontScale;
        int bottomY = screenHeight - bottomBarH + (bottomBarH - btnH) / 2;
        int totalPages = CurrentTotalPages;
        bool hasPrev = _page > 0;
        bool hasNext = _page < totalPages - 1;

        _prevRect = new Rectangle(Padding, bottomY, btnW, btnH);
        DrawButton(sb, pixel, uiFont, _prevRect, "<< Q",
            _bottomFocus == BottomFocus.Prev, hasPrev, fontScale);

        _nextRect = new Rectangle(Padding + btnW + CardSpacing, bottomY, btnW, btnH);
        DrawButton(sb, pixel, uiFont, _nextRect, "E >>",
            _bottomFocus == BottomFocus.Next, hasNext, fontScale);

        _backRect = new Rectangle(screenWidth - btnW - Padding, bottomY, btnW, btnH);
        UIDraw.ShadowedPanel(sb, pixel, _backRect, radius,
            _bottomFocus == BottomFocus.Back ? new Color(120, 50, 50) : UITheme.SlateCard,
            shadowOff, Color.Black * 0.3f);
        if (_bottomFocus == BottomFocus.Back)
            UIDraw.GlowBorder(sb, pixel, _backRect, radius, UITheme.PurpleGlow);
        string backLabel = _level == Level.Items ? "Back" : "Close";
        uiFont.Scale = fontScale;
        DrawText(sb, uiFont, backLabel,
            new Vector2(_backRect.X + btnW / 2 - uiFont.MeasureWidth(backLabel) / 2, _backRect.Y + (btnH - lineH) / 2), Color.White, fontScale);

        // Page dots
        if (totalPages > 1 && totalPages <= 30)
        {
            int dotSize = Math.Max(4, fontScale * 2);
            int dotSpacing = dotSize * 2;
            int dotsW = totalPages * dotSpacing - (dotSpacing - dotSize);
            int dotsX = screenWidth / 2 - dotsW / 2;
            for (int p = 0; p < totalPages; p++)
            {
                var dotR = new Rectangle(dotsX + p * dotSpacing, bottomY + btnH / 2 - dotSize / 2, dotSize, dotSize);
                sb.Draw(pixel, dotR, p == _page ? UITheme.PurpleAccent : UITheme.PurpleMuted);
            }
        }
        else if (totalPages > 30)
        {
            string pageStr = $"Page {_page + 1}/{totalPages}";
            uiFont.Scale = smallScale;
            DrawText(sb, uiFont, pageStr,
                new Vector2(screenWidth / 2 - uiFont.MeasureWidth(pageStr) / 2, bottomY + (btnH - lineH) / 2), UITheme.TextSecondary, smallScale);
            uiFont.Scale = fontScale;
        }

        // ── Popup (via PopupModal) ──
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

    private void DrawButton(SpriteBatch sb, Texture2D pixel, PixelFont uiFont,
                            Rectangle rect, string label, bool selected, bool enabled, int fontScale = 3)
    {
        uiFont.Scale = fontScale;
        int lineH = uiFont.CharHeight;
        int radius = Math.Max(2, fontScale * 2);
        int shadowOff = Math.Max(1, fontScale);

        Color bg = selected ? UITheme.PurpleAccent : (enabled ? UITheme.SlateCard : new Color(20, 22, 32, 100));
        UIDraw.ShadowedPanel(sb, pixel, rect, radius, bg, shadowOff, Color.Black * 0.3f);
        if (selected) UIDraw.GlowBorder(sb, pixel, rect, radius, UITheme.PurpleGlow);
        DrawText(sb, uiFont, label,
            new Vector2(rect.X + rect.Width / 2 - uiFont.MeasureWidth(label) / 2, rect.Y + (rect.Height - lineH) / 2),
            enabled ? Color.White : UITheme.TextDisabled, fontScale);
    }

    private static void DrawText(SpriteBatch sb, PixelFont uiFont, string text, Vector2 pos, Color color, float scale)
    {
        uiFont.Scale = (int)Math.Max(1, scale);
        UIDraw.ShadowedText(sb, uiFont, text, pos, color, UITheme.TextShadow);
    }

    private sealed class Category
    {
        public string Prefix = "";
        public string Label = "";
        public string[] Folders = Array.Empty<string>();
        public string[] Names = Array.Empty<string>();
    }
}
