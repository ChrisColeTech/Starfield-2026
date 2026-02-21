using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI;

/// <summary>
/// Selectable menu with grid navigation.
/// Handles D-pad input, selection highlight, confirm/cancel.
/// Used for non-battle menus (pause menu, etc.).
/// </summary>
public class MenuBox
{
    private readonly List<MenuItem> _items = new();

    public int Columns { get; set; } = 1;
    public int SelectedIndex { get; set; }
    public bool IsActive { get; set; }
    public Action? OnCancel { get; set; }
    public IReadOnlyList<MenuItem> Items => _items;

    /// <summary>Set the menu items (params overload).</summary>
    public void SetItems(params MenuItem[] items)
    {
        _items.Clear();
        _items.AddRange(items);
        SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, _items.Count - 1));
    }

    /// <summary>Process navigation and confirm/cancel input.</summary>
    public void Update(InputSnapshot input)
    {
        if (!IsActive || _items.Count == 0) return;

        int cols = Math.Max(1, Columns);
        int rows = (_items.Count + cols - 1) / cols;
        int col = SelectedIndex % cols;
        int row = SelectedIndex / cols;

        if (input.Up) row--;
        if (input.Down) row++;
        if (input.Left) col--;
        if (input.Right) col++;

        row = Math.Clamp(row, 0, rows - 1);
        col = Math.Clamp(col, 0, cols - 1);

        int newIndex = row * cols + col;
        if (newIndex >= 0 && newIndex < _items.Count)
            SelectedIndex = newIndex;

        if (input.Confirm && SelectedIndex >= 0 && SelectedIndex < _items.Count)
        {
            var item = _items[SelectedIndex];
            if (item.Enabled)
                item.OnConfirm?.Invoke();
        }

        if (input.Cancel)
            OnCancel?.Invoke();
    }

    /// <summary>Draw the menu panel with items and selection highlight.</summary>
    public void Draw(SpriteBatch sb, PixelFont font, Texture2D pixel,
        Rectangle bounds, int fontScale)
    {
        if (_items.Count == 0) return;

        int radius = Math.Max(2, fontScale * 2);
        int shadowOff = Math.Max(1, fontScale);
        font.Scale = fontScale;

        // Panel with drop shadow
        UIDraw.ShadowedPanel(sb, pixel, bounds, radius,
            UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);

        // Layout
        int pad = 8 * fontScale;
        int cols = Math.Max(1, Columns);
        int rows = (_items.Count + cols - 1) / cols;
        int colW = (bounds.Width - pad * 2) / cols;
        int rowH = (bounds.Height - pad * 2) / Math.Max(1, rows);

        for (int i = 0; i < _items.Count; i++)
        {
            int c = i % cols;
            int r = i / cols;
            int x = bounds.X + pad + c * colW;
            int y = bounds.Y + pad + r * rowH;

            bool selected = (i == SelectedIndex && IsActive);
            var item = _items[i];

            // Selection highlight with glow
            if (selected)
            {
                var hlRect = new Rectangle(x - 2, y - 1, colW, rowH);
                int hlRadius = Math.Max(1, fontScale);
                UIDraw.RoundedRect(sb, pixel, hlRect, hlRadius, UITheme.PurpleSelected);
                UIDraw.GlowBorder(sb, pixel, hlRect, hlRadius, UITheme.PurpleGlow);
            }

            // Item text — centered
            Color textColor = !item.Enabled ? UITheme.TextDisabled :
                              selected ? Color.White : UITheme.TextPrimary;

            string label = item.Label;
            int textW = font.MeasureWidth(label);
            int textX = x + (colW - textW) / 2;
            int textY = y + (rowH - font.CharHeight) / 2;

            UIDraw.ShadowedText(sb, font, label,
                new Vector2(textX, textY),
                textColor, UITheme.TextShadow);
        }
    }
}
