using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Items;
using Starfield2026.Core.UI.Fonts;

namespace Starfield2026.Core.UI.Screens;

/// <summary>
/// Full-screen bag overlay. Pouch tabs, 4x5 item grid, pagination.
/// </summary>
public class BagScreen : IScreenOverlay
{
    private enum Phase { FadeIn, Navigation, FadeOut }
    private enum CursorZone { Pouches, Items, Cancel }

    private const float FadeDuration = 0.3f;
    private const int GridColumns = 4;
    private const int GridRows = 5;
    private const int ItemsPerPage = GridColumns * GridRows;
    private const int Padding = 20;
    private const int TabHeight = 40;
    private const int BottomBarHeight = 44;
    private const int CellSpacing = 4;

    // Colors
    private static readonly Color GradTop = new(215, 230, 230);
    private static readonly Color GradMid = new(230, 165, 0);
    private static readonly Color GradBot = new(245, 180, 30);
    private static readonly Color CellFillTop = new(240, 225, 255);
    private static readonly Color CellFillBot = new(190, 175, 205);
    private static readonly Color CellBorder = new(75, 75, 75);
    private static readonly Color CellBorderSelected = new(48, 150, 255);
    private static readonly Color CellEmpty = new(255, 200, 145, 100);
    private static readonly Color TabNormal = new(180, 180, 180);
    private static readonly Color TabSelected = new(255, 255, 255);
    private static readonly Color CancelNormal = new(60, 60, 60);
    private static readonly Color CancelSelected = new(48, 120, 255);

    // Pouch definitions
    private static readonly (string Label, ItemCategory[] Categories)[] Pouches =
    {
        ("Items",   new[] { ItemCategory.Medicine, ItemCategory.Battle }),
        ("Balls",   new[] { ItemCategory.Pokeball }),
        ("Berries", new[] { ItemCategory.Berry }),
        ("Key",     new[] { ItemCategory.KeyItem }),
        ("TMs",     new[] { ItemCategory.TM, ItemCategory.HM }),
    };

    private readonly PlayerInventory _inventory;

    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;
    private CursorZone _cursorZone = CursorZone.Pouches;
    private int _selectedPouch;
    private int _selectedItem;
    private int _currentPage;

    // Cached pouch contents
    private IReadOnlyList<InventorySlot> _currentPouchItems = Array.Empty<InventorySlot>();

    // Cached layout for mouse hit testing (set during Draw)
    private Rectangle[] _tabRects = Array.Empty<Rectangle>();
    private Rectangle[] _cellRects = Array.Empty<Rectangle>();
    private Rectangle _cancelRect;
    private Rectangle _leftArrowRect;
    private Rectangle _rightArrowRect;

    public bool IsFinished { get; private set; }

    public BagScreen(PlayerInventory inventory)
    {
        _inventory = inventory;
        RefreshPouch();
    }

    private void RefreshPouch()
    {
        _currentPouchItems = _inventory.GetPouch(Pouches[_selectedPouch].Categories);
        _currentPage = 0;
        _selectedItem = 0;
    }

    private int PageCount => Math.Max(1, (_currentPouchItems.Count + ItemsPerPage - 1) / ItemsPerPage);
    private int PageStart => _currentPage * ItemsPerPage;
    private int PageItemCount => Math.Min(ItemsPerPage, _currentPouchItems.Count - PageStart);

    public void Update(float deltaTime, InputSnapshot input)
    {
        switch (_phase)
        {
            case Phase.FadeIn:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration)
                    _phase = Phase.Navigation;
                break;

            case Phase.Navigation:
                UpdateNavigation(input);
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

        // Mouse click — check tabs, cells, cancel, page arrows
        if (input.MouseClicked)
        {
            for (int i = 0; i < _tabRects.Length; i++)
            {
                if (_tabRects[i].Contains(input.MousePosition))
                {
                    _cursorZone = CursorZone.Pouches;
                    if (i != _selectedPouch)
                    {
                        _selectedPouch = i;
                        RefreshPouch();
                    }
                    return;
                }
            }
            for (int i = 0; i < _cellRects.Length && i < PageItemCount; i++)
            {
                if (_cellRects[i].Contains(input.MousePosition))
                {
                    _cursorZone = CursorZone.Items;
                    _selectedItem = i;
                    return;
                }
            }
            if (_cancelRect.Contains(input.MousePosition))
            {
                BeginExit();
                return;
            }
            if (_leftArrowRect.Contains(input.MousePosition) && _currentPage > 0)
            {
                _currentPage--;
                return;
            }
            if (_rightArrowRect.Contains(input.MousePosition) && _currentPage < PageCount - 1)
            {
                _currentPage++;
                return;
            }
        }

        // Mouse hover — update selection
        if (!input.MouseClicked)
        {
            for (int i = 0; i < _tabRects.Length; i++)
            {
                if (_tabRects[i].Contains(input.MousePosition))
                {
                    _cursorZone = CursorZone.Pouches;
                    if (i != _selectedPouch)
                    {
                        _selectedPouch = i;
                        RefreshPouch();
                    }
                }
            }
            for (int i = 0; i < _cellRects.Length && i < PageItemCount; i++)
            {
                if (_cellRects[i].Contains(input.MousePosition))
                {
                    _cursorZone = CursorZone.Items;
                    _selectedItem = i;
                }
            }
            if (_cancelRect.Contains(input.MousePosition))
                _cursorZone = CursorZone.Cancel;
        }

        switch (_cursorZone)
        {
            case CursorZone.Pouches:
                if (input.Left && _selectedPouch > 0)
                {
                    _selectedPouch--;
                    RefreshPouch();
                }
                if (input.Right && _selectedPouch < Pouches.Length - 1)
                {
                    _selectedPouch++;
                    RefreshPouch();
                }
                if (input.Down)
                {
                    _cursorZone = PageItemCount > 0 ? CursorZone.Items : CursorZone.Cancel;
                    _selectedItem = 0;
                }
                break;

            case CursorZone.Items:
                NavigateItemGrid(input);
                if (input.Up && _selectedItem < GridColumns)
                    _cursorZone = CursorZone.Pouches;
                if (input.Down && _selectedItem / GridColumns >= GridRows - 1)
                    _cursorZone = CursorZone.Cancel;
                break;

            case CursorZone.Cancel:
                if (input.Up)
                {
                    _cursorZone = PageItemCount > 0 ? CursorZone.Items : CursorZone.Pouches;
                    // Select bottom-left item
                    if (_cursorZone == CursorZone.Items)
                        _selectedItem = Math.Min(PageItemCount - 1, (GridRows - 1) * GridColumns);
                }
                if (input.Confirm)
                    BeginExit();
                if (input.Left && _currentPage > 0)
                    _currentPage--;
                if (input.Right && _currentPage < PageCount - 1)
                    _currentPage++;
                break;
        }
    }

    private void NavigateItemGrid(InputSnapshot input)
    {
        int col = _selectedItem % GridColumns;
        int row = _selectedItem / GridColumns;

        if (input.Left && col > 0)
            _selectedItem--;
        if (input.Right && col < GridColumns - 1 && _selectedItem + 1 < PageItemCount)
            _selectedItem++;
        if (input.Up && row > 0)
            _selectedItem -= GridColumns;
        if (input.Down && row < GridRows - 1)
        {
            int next = _selectedItem + GridColumns;
            if (next < PageItemCount)
                _selectedItem = next;
        }
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
        UIStyle.DrawTripleGradient(sb, pixel, fullRect, GradTop, GradMid, GradBot);

        // Top bar — pouch tabs
        DrawPouchTabs(sb, pixel, fontRenderer, fallbackFont, screenWidth);

        // Item grid
        int gridY = Padding + TabHeight + 8;
        int gridH = screenHeight - gridY - BottomBarHeight - Padding;
        int gridW = screenWidth - Padding * 2;
        int cellW = (gridW - CellSpacing * (GridColumns - 1)) / GridColumns;
        int cellH = (gridH - CellSpacing * (GridRows - 1)) / GridRows;

        if (_cellRects.Length != ItemsPerPage)
            _cellRects = new Rectangle[ItemsPerPage];

        for (int i = 0; i < ItemsPerPage; i++)
        {
            int col = i % GridColumns;
            int row = i / GridColumns;
            int cx = Padding + col * (cellW + CellSpacing);
            int cy = gridY + row * (cellH + CellSpacing);
            var cellRect = new Rectangle(cx, cy, cellW, cellH);
            _cellRects[i] = cellRect;

            int itemIdx = PageStart + i;
            bool selected = _cursorZone == CursorZone.Items && i == _selectedItem;

            if (itemIdx < _currentPouchItems.Count)
            {
                var slot = _currentPouchItems[itemIdx];
                DrawFilledCell(sb, pixel, fontRenderer, fallbackFont, cellRect, slot, selected);
            }
            else
            {
                DrawEmptyCell(sb, pixel, cellRect);
            }
        }

        // Bottom bar
        DrawBottomBar(sb, pixel, fontRenderer, fallbackFont, screenWidth, screenHeight);

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

    private void DrawPouchTabs(SpriteBatch sb, Texture2D pixel,
                                KermFontRenderer? fontRenderer, SpriteFont fallbackFont,
                                int screenWidth)
    {
        int tabW = (screenWidth - Padding * 2) / Pouches.Length;
        int tabY = Padding;

        // Tab background strip
        sb.Draw(pixel, new Rectangle(Padding, tabY, screenWidth - Padding * 2, TabHeight),
            new Color(255, 255, 255, 200));

        if (_tabRects.Length != Pouches.Length)
            _tabRects = new Rectangle[Pouches.Length];

        for (int i = 0; i < Pouches.Length; i++)
        {
            int tx = Padding + i * tabW;
            var tabRect = new Rectangle(tx + 2, tabY + 2, tabW - 4, TabHeight - 4);
            _tabRects[i] = tabRect;
            bool selected = i == _selectedPouch;

            sb.Draw(pixel, tabRect, selected ? TabSelected : TabNormal);
            if (selected && _cursorZone == CursorZone.Pouches)
                DrawBorder(sb, pixel, tabRect, 2, CellBorderSelected);

            DrawText(sb, fontRenderer, fallbackFont, Pouches[i].Label,
                new Vector2(tx + 10, tabY + 8), selected ? Color.Black : new Color(60, 60, 60), 2);
        }
    }

    private void DrawFilledCell(SpriteBatch sb, Texture2D pixel,
                                 KermFontRenderer? fontRenderer, SpriteFont fallbackFont,
                                 Rectangle rect, InventorySlot slot, bool selected)
    {
        // Top half
        int topH = rect.Height / 2;
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, topH), CellFillTop);
        // Bottom half
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y + topH, rect.Width, rect.Height - topH), CellFillBot);

        // Border
        DrawBorder(sb, pixel, rect, 2, selected ? CellBorderSelected : CellBorder);

        // Item name
        var itemDef = ItemRegistry.GetItem(slot.ItemId);
        string name = itemDef?.Name ?? $"Item #{slot.ItemId}";
        DrawText(sb, fontRenderer, fallbackFont, name,
            new Vector2(rect.X + 6, rect.Y + 4), new Color(50, 40, 50), 2);

        // Quantity
        string qty = $"x{slot.Quantity}";
        DrawText(sb, fontRenderer, fallbackFont, qty,
            new Vector2(rect.X + 6, rect.Y + topH + 4), new Color(80, 70, 80), 2);
    }

    private void DrawEmptyCell(SpriteBatch sb, Texture2D pixel, Rectangle rect)
    {
        DrawBorder(sb, pixel, rect, 2, CellEmpty);
    }

    private void DrawBottomBar(SpriteBatch sb, Texture2D pixel,
                                KermFontRenderer? fontRenderer, SpriteFont fallbackFont,
                                int screenWidth, int screenHeight)
    {
        int barY = screenHeight - BottomBarHeight;

        // Page info
        string pageText = $"Page {_currentPage + 1}/{PageCount}";
        DrawText(sb, fontRenderer, fallbackFont, pageText,
            new Vector2(Padding + 80, barY + 12), Color.White, 2);

        // Page arrows
        _leftArrowRect = new Rectangle(Padding + 10, barY + 10, 30, 30);
        _rightArrowRect = new Rectangle(Padding + 50, barY + 10, 30, 30);
        if (_currentPage > 0)
            DrawText(sb, fontRenderer, fallbackFont, "<",
                new Vector2(Padding + 10, barY + 10), Color.White, 3);
        if (_currentPage < PageCount - 1)
            DrawText(sb, fontRenderer, fallbackFont, ">",
                new Vector2(Padding + 50, barY + 10), Color.White, 3);

        // Cancel button
        int cancelW = 100;
        int cancelH = 32;
        int cancelX = screenWidth - cancelW - Padding;
        int cancelY = barY + (BottomBarHeight - cancelH) / 2;
        var cancelRect = new Rectangle(cancelX, cancelY, cancelW, cancelH);
        _cancelRect = cancelRect;

        bool cancelSel = _cursorZone == CursorZone.Cancel;
        sb.Draw(pixel, cancelRect, cancelSel ? CancelSelected : CancelNormal);
        if (cancelSel)
            DrawBorder(sb, pixel, cancelRect, 2, new Color(255, 255, 255, 180));

        DrawText(sb, fontRenderer, fallbackFont, "CANCEL",
            new Vector2(cancelX + 10, cancelY + 6), Color.White, 2);
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
