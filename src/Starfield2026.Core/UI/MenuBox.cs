using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI;

/// <summary>
/// Navigable menu with grid layout and keyboard/mouse input.
/// Supports multi-column layout, disabled items, and confirm/cancel callbacks.
/// </summary>
public class MenuBox
{
    private readonly List<MenuItem> _items = new();

    /// <summary>Number of columns in the grid layout.</summary>
    public int Columns { get; set; } = 2;

    /// <summary>Currently selected item index.</summary>
    public int SelectedIndex { get; set; }

    /// <summary>True when the menu is accepting input.</summary>
    public bool IsActive { get; set; }

    /// <summary>Callback when the user presses cancel.</summary>
    public Action? OnCancel { get; set; }

    /// <summary>Read-only access to menu items.</summary>
    public IReadOnlyList<MenuItem> Items => _items;

    /// <summary>Set the menu options.</summary>
    public void SetItems(params MenuItem[] items)
    {
        _items.Clear();
        _items.AddRange(items);
        SelectedIndex = 0;

        // Snap to first enabled item
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Enabled) { SelectedIndex = i; break; }
        }
    }

    /// <summary>
    /// Handle input navigation and selection.
    /// </summary>
    public void Update(InputSnapshot input)
    {
        if (!IsActive || _items.Count == 0) return;

        int rows = (_items.Count + Columns - 1) / Columns;
        int col = SelectedIndex % Columns;
        int row = SelectedIndex / Columns;

        // Navigation
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Left))
            col = Math.Max(0, col - 1);
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Right))
            col = Math.Min(Columns - 1, col + 1);
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Up))
            row = Math.Max(0, row - 1);
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Down))
            row = Math.Min(rows - 1, row + 1);

        int newIndex = row * Columns + col;
        if (newIndex >= 0 && newIndex < _items.Count)
            SelectedIndex = newIndex;

        // Confirm
        if (input.ConfirmPressed && _items[SelectedIndex].Enabled)
        {
            _items[SelectedIndex].OnConfirm?.Invoke();
        }

        // Cancel
        if (input.CancelPressed)
        {
            OnCancel?.Invoke();
        }
    }

    /// <summary>
    /// Draw the menu box with selection highlight.
    /// All layout is proportional to the bounds — no magic pixel numbers.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, PixelFont uiFont, Texture2D pixel, Rectangle bounds, int fontScale = 1)
    {
        if (_items.Count == 0) return;

        uiFont.Scale = fontScale;

        // Background
        spriteBatch.Draw(pixel, bounds, UITheme.MenuBackground);

        // Border
        int b = Math.Max(1, fontScale / 2);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, b), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - b, bounds.Width, b), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, b, bounds.Height), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - b, bounds.Y, b, bounds.Height), Color.White * 0.6f);

        // Layout from bounds — proportional, never overflows
        int pad = bounds.Height / 10;
        int rows = (_items.Count + Columns - 1) / Columns;
        int itemW = (bounds.Width - pad * 2) / Columns;
        int itemH = (bounds.Height - pad * 2) / Math.Max(1, rows);

        for (int i = 0; i < _items.Count; i++)
        {
            int col = i % Columns;
            int row = i / Columns;
            int ix = bounds.X + pad + col * itemW;
            int iy = bounds.Y + pad + row * itemH;

            var item = _items[i];
            Color textColor = item.Enabled ? Color.White : Color.Gray * 0.5f;

            // Selection highlight
            if (i == SelectedIndex && IsActive)
            {
                spriteBatch.Draw(pixel, new Rectangle(ix, iy, itemW, itemH), Color.White * 0.15f);

                // Selector arrow — left of item, vertically centered
                int arrowSize = uiFont.CharHeight / 2;
                UIStyle.DrawRightArrow(spriteBatch, pixel,
                    new Vector2(ix - arrowSize - 2, iy + (itemH - arrowSize) / 2),
                    arrowSize, Color.White);
            }

            // Item label — vertically centered in row
            int textX = ix + pad;
            int textY = iy + (itemH - uiFont.CharHeight) / 2;
            UIStyle.DrawShadowedText(spriteBatch, uiFont, item.Label,
                new Vector2(textX, textY), textColor, Color.Black * 0.5f);
        }
    }
}
