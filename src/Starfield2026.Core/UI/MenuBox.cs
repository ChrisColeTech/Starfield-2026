using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;

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
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds)
    {
        if (_items.Count == 0) return;

        // Background
        spriteBatch.Draw(pixel, bounds, Color.Black * 0.85f);

        // Border
        int b = 2;
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, b), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - b, bounds.Width, b), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, b, bounds.Height), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - b, bounds.Y, b, bounds.Height), Color.White * 0.6f);

        int padding = 10;
        int itemW = (bounds.Width - padding * 2) / Columns;
        int itemH = 24;

        for (int i = 0; i < _items.Count; i++)
        {
            int col = i % Columns;
            int row = i / Columns;
            int ix = bounds.X + padding + col * itemW;
            int iy = bounds.Y + padding + row * itemH;

            var item = _items[i];
            Color textColor = item.Enabled ? Color.White : Color.Gray * 0.5f;

            // Selection highlight
            if (i == SelectedIndex && IsActive)
            {
                spriteBatch.Draw(pixel, new Rectangle(ix - 2, iy, itemW - 4, itemH - 2), Color.Cyan * 0.25f);

                // Selector arrow
                spriteBatch.Draw(pixel, new Rectangle(ix - 8, iy + 4, 5, 5), Color.Cyan);
            }

            // Item label (pixel-block text)
            DrawPixelText(spriteBatch, pixel, item.Label, ix + 4, iy + 4, textColor);
        }
    }

    private static void DrawPixelText(SpriteBatch sb, Texture2D pixel, string text, int x, int y, Color color)
    {
        int charW = 7, charH = 12, spacing = 1;
        foreach (char c in text)
        {
            if (c == ' ')
            {
                x += charW + spacing;
                continue;
            }
            sb.Draw(pixel, new Rectangle(x, y, charW - 1, charH - 1), color * 0.9f);
            x += charW + spacing;
        }
    }
}
