using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Items;
using Starfield2026.Core.Rendering;

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
                     PixelFont uiFont, int screenWidth, int screenHeight, int fontScale = 3)
    {
        uiFont.Scale = fontScale;
        var fullRect = new Rectangle(0, 0, screenWidth, screenHeight);

        int pad = 6 * fontScale;
        int tabH = 20 * fontScale;
        int botH = 22 * fontScale;
        int cellSp = 2 * fontScale;

        // Gradient background
        UIStyle.DrawTripleGradient(sb, pixel, fullRect, GradTop, GradMid, GradBot);

        // Top bar — pouch tabs
        DrawPouchTabs(sb, pixel, uiFont, screenWidth, pad, tabH, fontScale);

        // Item grid
        int gridY = pad + tabH + 4 * fontScale;
        int gridH = screenHeight - gridY - botH - pad;
        int gridW = screenWidth - pad * 2;
        int cellW = (gridW - cellSp * (GridColumns - 1)) / GridColumns;
        int cellH = (gridH - cellSp * (GridRows - 1)) / GridRows;

        if (_cellRects.Length != ItemsPerPage)
            _cellRects = new Rectangle[ItemsPerPage];

        for (int i = 0; i < ItemsPerPage; i++)
        {
            int col = i % GridColumns;
            int row = i / GridColumns;
            int cx = pad + col * (cellW + cellSp);
            int cy = gridY + row * (cellH + cellSp);
            var cellRect = new Rectangle(cx, cy, cellW, cellH);
            _cellRects[i] = cellRect;

            int itemIdx = PageStart + i;
            bool selected = _cursorZone == CursorZone.Items && i == _selectedItem;

            if (itemIdx < _currentPouchItems.Count)
            {
                var slot = _currentPouchItems[itemIdx];
                DrawFilledCell(sb, pixel, uiFont, cellRect, slot, selected, fontScale);
            }
            else
            {
                DrawEmptyCell(sb, pixel, cellRect, fontScale);
            }
        }

        // Bottom bar
        DrawBottomBar(sb, pixel, uiFont, screenWidth, screenHeight, pad, botH, fontScale);

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
                                PixelFont uiFont,
                                int screenWidth, int pad, int tabH, int fontScale)
    {
        uiFont.Scale = fontScale;
        int tabW = (screenWidth - pad * 2) / Pouches.Length;
        int tabY = pad;

        // Tab background strip
        sb.Draw(pixel, new Rectangle(pad, tabY, screenWidth - pad * 2, tabH),
            new Color(255, 255, 255, 200));

        if (_tabRects.Length != Pouches.Length)
            _tabRects = new Rectangle[Pouches.Length];

        for (int i = 0; i < Pouches.Length; i++)
        {
            int tx = pad + i * tabW;
            var tabRect = new Rectangle(tx + 2 * fontScale, tabY + 2 * fontScale, tabW - 4 * fontScale, tabH - 4 * fontScale);
            _tabRects[i] = tabRect;
            bool selected = i == _selectedPouch;

            sb.Draw(pixel, tabRect, selected ? TabSelected : TabNormal);
            if (selected && _cursorZone == CursorZone.Pouches)
                DrawBorder(sb, pixel, tabRect, 2 * fontScale, CellBorderSelected);

            int labelW = uiFont.MeasureWidth(Pouches[i].Label);
            int labelX = tabRect.X + (tabRect.Width - labelW) / 2;
            int labelY = tabRect.Y + (tabRect.Height - uiFont.CharHeight) / 2;
            DrawText(sb, uiFont, Pouches[i].Label,
                new Vector2(labelX, labelY), selected ? Color.Black : new Color(60, 60, 60), fontScale);
        }
    }

    private void DrawFilledCell(SpriteBatch sb, Texture2D pixel,
                                 PixelFont uiFont,
                                 Rectangle rect, InventorySlot slot, bool selected, int fontScale)
    {
        uiFont.Scale = fontScale;
        // Top half
        int topH = rect.Height / 2;
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, topH), CellFillTop);
        // Bottom half
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y + topH, rect.Width, rect.Height - topH), CellFillBot);

        // Border
        DrawBorder(sb, pixel, rect, 2 * fontScale, selected ? CellBorderSelected : CellBorder);

        // Item name — centered in top half
        var itemDef = ItemRegistry.GetItem(slot.ItemId);
        string name = itemDef?.Name ?? $"Item #{slot.ItemId}";
        int nameW = uiFont.MeasureWidth(name);
        int nameX = rect.X + (rect.Width - nameW) / 2;
        int nameY = rect.Y + (topH - uiFont.CharHeight) / 2;
        DrawText(sb, uiFont, name, new Vector2(nameX, nameY), new Color(50, 40, 50), fontScale);

        // Quantity — centered in bottom half
        string qty = $"x{slot.Quantity}";
        int qtyW = uiFont.MeasureWidth(qty);
        int qtyX = rect.X + (rect.Width - qtyW) / 2;
        int qtyY = rect.Y + topH + (rect.Height - topH - uiFont.CharHeight) / 2;
        DrawText(sb, uiFont, qty, new Vector2(qtyX, qtyY), new Color(80, 70, 80), fontScale);
    }

    private void DrawEmptyCell(SpriteBatch sb, Texture2D pixel, Rectangle rect, int fontScale)
    {
        DrawBorder(sb, pixel, rect, 2 * fontScale, CellEmpty);
    }

    private void DrawBottomBar(SpriteBatch sb, Texture2D pixel,
                                PixelFont uiFont,
                                int screenWidth, int screenHeight, int pad, int botH, int fontScale)
    {
        uiFont.Scale = fontScale;
        int barY = screenHeight - botH;

        // Page info
        string pageText = $"Page {_currentPage + 1}/{PageCount}";
        DrawText(sb, uiFont, pageText,
            new Vector2(pad + 40 * fontScale, barY + 6 * fontScale), Color.White, fontScale);

        // Page arrows
        _leftArrowRect = new Rectangle(pad + 5 * fontScale, barY + 5 * fontScale, 15 * fontScale, 15 * fontScale);
        _rightArrowRect = new Rectangle(pad + 25 * fontScale, barY + 5 * fontScale, 15 * fontScale, 15 * fontScale);
        if (_currentPage > 0)
            DrawText(sb, uiFont, "<",
                new Vector2(pad + 5 * fontScale, barY + 5 * fontScale), Color.White, fontScale * 1.5f);
        if (_currentPage < PageCount - 1)
            DrawText(sb, uiFont, ">",
                new Vector2(pad + 25 * fontScale, barY + 5 * fontScale), Color.White, fontScale * 1.5f);

        // Cancel button
        int cancelW = 70 * fontScale;
        int cancelH = 20 * fontScale;
        int cancelX = screenWidth - cancelW - pad;
        int cancelY = barY + (botH - cancelH) / 2;
        var cancelRect = new Rectangle(cancelX, cancelY, cancelW, cancelH);
        _cancelRect = cancelRect;

        bool cancelSel = _cursorZone == CursorZone.Cancel;
        sb.Draw(pixel, cancelRect, cancelSel ? CancelSelected : CancelNormal);
        if (cancelSel)
            DrawBorder(sb, pixel, cancelRect, 2 * fontScale, new Color(255, 255, 255, 180));

        DrawText(sb, uiFont, "CANCEL",
            new Vector2(cancelX + (cancelW - uiFont.MeasureWidth("CANCEL")) / 2, cancelY + (cancelH - uiFont.CharHeight) / 2), Color.White, fontScale);
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
