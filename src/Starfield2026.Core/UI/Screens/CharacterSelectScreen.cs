using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.UI.Fonts;

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

    private static readonly Color CardFill = new(48, 48, 58, 200);
    private static readonly Color CardFillSelected = new(60, 80, 120, 220);
    private static readonly Color CardBorder = new(100, 200, 255, 220);
    private static readonly Color BtnNormal = new(60, 60, 70, 200);
    private static readonly Color BtnSelected = new(80, 100, 140, 220);
    private static readonly Color BtnBack = new(48, 48, 48);
    private static readonly Color BtnBackSel = new(96, 48, 48);
    private static readonly Color GradTop = new(30, 60, 100);
    private static readonly Color GradMid = new(20, 40, 80);
    private static readonly Color GradBot = new(60, 120, 180);

    private readonly Category[] _categories;
    private Level _level = Level.Category;
    private int _catIndex;
    private int _itemIndex;
    private int _page;
    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;

    // Bottom row focus
    private enum BottomFocus { None, Prev, Next, Back }
    private BottomFocus _bottomFocus = BottomFocus.None;

    // Layout rects (computed each Draw)
    private Rectangle[] _cardRects = Array.Empty<Rectangle>();
    private Rectangle _prevRect, _nextRect, _backRect;

    public string? SelectedFolder { get; private set; }
    public bool IsFinished { get; private set; }

    public CharacterSelectScreen(string[] folders, string[] displayNames)
    {
        // Group folders by prefix
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

        // Build categories sorted: Trainers first, Pokemon second, then alphabetical
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
        // Extract prefix like "tr", "pm", "ob", "group"
        int i = 0;
        while (i < folder.Length && char.IsLetter(folder[i])) i++;
        // For "group_0001" → "group", for "tr0001_00" → "tr", for "pm0025_00" → "pm"
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

    // Pagination helpers for item level
    private Category CurrentCat => _categories[_catIndex];
    private int TotalPages => Math.Max(1, (int)Math.Ceiling(CurrentCat.Folders.Length / (double)ItemsPerPage));
    private int PageStart => _page * ItemsPerPage;
    private int PageCount => Math.Min(ItemsPerPage, CurrentCat.Folders.Length - PageStart);

    // For category level
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
                    else { SelectedFolder = CurrentCat.Folders[PageStart + i]; BeginExit(); }
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
            else { SelectedFolder = CurrentCat.Folders[PageStart + localIdx]; BeginExit(); }
        }
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
                     KermFontRenderer? fontRenderer, KermFont? font,
                     SpriteFont fallbackFont, int screenWidth, int screenHeight, int fontScale = 3)
    {
        // Derived sizes from font scale
        int charW = fontScale * 5 / 3;    // approx glyph width at scale=3 → 5
        int lineH = fontScale * 7;
        int smallScale = Math.Max(1, fontScale * 2 / 3);
        int smallCharW = smallScale * 5 / 3;

        var fullRect = new Rectangle(0, 0, screenWidth, screenHeight);
        UIStyle.DrawTripleGradient(sb, pixel, fullRect, GradTop, GradMid, GradBot);

        // Title
        string title;
        if (_level == Level.Category)
            title = "SELECT CATEGORY";
        else
            title = $"{CurrentCat.Label.ToUpperInvariant()}  ({_page + 1}/{TotalPages})  [{CurrentCat.Folders.Length}]";
        DrawText(sb, fontRenderer, fallbackFont, title, new Vector2(Padding, Padding - 4), Color.White, fontScale);

        // Breadcrumb for item level
        if (_level == Level.Items)
        {
            DrawText(sb, fontRenderer, fallbackFont, "< Esc to go back",
                new Vector2(Padding, Padding + lineH + 2), new Color(140, 160, 200), smallScale);
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
            sb.Draw(pixel, _cardRects[i], selected ? CardFillSelected : CardFill);
            if (selected) DrawBorder(sb, pixel, _cardRects[i], 2, CardBorder);

            if (_level == Level.Category)
            {
                int gi = CatPageStart + i;
                var cat = _categories[gi];
                DrawText(sb, fontRenderer, fallbackFont, cat.Label,
                    new Vector2(cx + cardW / 2 - cat.Label.Length * charW, cy + cardH / 2 - lineH), Color.White, fontScale);
                string countStr = $"{cat.Folders.Length} models";
                DrawText(sb, fontRenderer, fallbackFont, countStr,
                    new Vector2(cx + cardW / 2 - countStr.Length * smallCharW, cy + cardH / 2 + 4), new Color(160, 180, 220), smallScale);
                DrawText(sb, fontRenderer, fallbackFont, cat.Prefix,
                    new Vector2(cx + 8, cy + cardH - lineH - 4), new Color(120, 120, 140), smallScale);
            }
            else
            {
                int gi = PageStart + i;
                string name = gi < CurrentCat.Names.Length ? CurrentCat.Names[gi] : CurrentCat.Folders[gi];
                DrawText(sb, fontRenderer, fallbackFont, name,
                    new Vector2(cx + cardW / 2 - name.Length * charW, cy + cardH / 2 - lineH / 2), Color.White, fontScale);
                DrawText(sb, fontRenderer, fallbackFont, CurrentCat.Folders[gi],
                    new Vector2(cx + 8, cy + cardH - lineH - 4), new Color(160, 160, 180), smallScale);
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
        DrawButton(sb, pixel, fontRenderer, fallbackFont, _prevRect, "<< Q",
            _bottomFocus == BottomFocus.Prev, hasPrev, fontScale);

        _nextRect = new Rectangle(Padding + btnW + CardSpacing, bottomY, btnW, btnH);
        DrawButton(sb, pixel, fontRenderer, fallbackFont, _nextRect, "E >>",
            _bottomFocus == BottomFocus.Next, hasNext, fontScale);

        _backRect = new Rectangle(screenWidth - btnW - Padding, bottomY, btnW, btnH);
        sb.Draw(pixel, _backRect, _bottomFocus == BottomFocus.Back ? BtnBackSel : BtnBack);
        if (_bottomFocus == BottomFocus.Back) DrawBorder(sb, pixel, _backRect, 2, CardBorder);
        string backLabel = _level == Level.Items ? "Back" : "Close";
        DrawText(sb, fontRenderer, fallbackFont, backLabel,
            new Vector2(_backRect.X + btnW / 2 - backLabel.Length * charW, _backRect.Y + (btnH - lineH) / 2), Color.White, fontScale);

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
                sb.Draw(pixel, dotR, p == _page ? CardBorder : new Color(80, 80, 100, 150));
            }
        }
        else if (totalPages > 30)
        {
            string pageStr = $"Page {_page + 1}/{totalPages}";
            DrawText(sb, fontRenderer, fallbackFont, pageStr,
                new Vector2(screenWidth / 2 - pageStr.Length * smallCharW, bottomY + (btnH - lineH) / 2), new Color(180, 180, 200), smallScale);
        }

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

    private void DrawButton(SpriteBatch sb, Texture2D pixel, KermFontRenderer? fr, SpriteFont ff,
                            Rectangle rect, string label, bool selected, bool enabled, int fontScale = 3)
    {
        int charW = fontScale * 5 / 3;
        int lineH = fontScale * 7;
        Color bg = selected ? BtnSelected : (enabled ? BtnNormal : new Color(30, 30, 30, 100));
        sb.Draw(pixel, rect, bg);
        if (selected) DrawBorder(sb, pixel, rect, 2, CardBorder);
        DrawText(sb, fr, ff, label,
            new Vector2(rect.X + rect.Width / 2 - label.Length * charW, rect.Y + (rect.Height - lineH) / 2),
            enabled ? Color.White : new Color(80, 80, 80), fontScale);
    }

    private static void DrawText(SpriteBatch sb, KermFontRenderer? fontRenderer,
                                  SpriteFont fallbackFont, string text, Vector2 pos, Color color, int scale)
    {
        if (fontRenderer != null) fontRenderer.DrawString(sb, text, pos, scale, color);
        else sb.DrawString(fallbackFont, text, pos, color);
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle r, int t, Color color)
    {
        sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, t), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - t, r.Width, t), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Y, t, r.Height), color);
        sb.Draw(pixel, new Rectangle(r.Right - t, r.Y, t, r.Height), color);
    }

    private sealed class Category
    {
        public string Prefix = "";
        public string Label = "";
        public string[] Folders = Array.Empty<string>();
        public string[] Names = Array.Empty<string>();
    }
}
