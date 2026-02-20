using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.UI.Fonts;

namespace Starfield2026.Core.UI;

/// <summary>
/// A single selectable menu option.
/// </summary>
public class BattleMenuItem
{
    public string Label { get; }
    public bool Enabled { get; set; } = true;
    public Action? OnConfirm { get; set; }

    public BattleMenuItem(string label, Action? onConfirm = null, bool enabled = true)
    {
        Label = label;
        OnConfirm = onConfirm;
        Enabled = enabled;
    }
}

/// <summary>
/// A navigable menu with grid or list layout.
/// Supports arrow key navigation, confirm/cancel, mouse hover and click.
/// </summary>
public class BattleMenuBox
{
    private readonly List<BattleMenuItem> _items = new();

    /// <summary>Number of columns (1 = vertical list, 2 = 2x2 grid, etc.).</summary>
    public int Columns { get; set; } = 2;

    /// <summary>Currently selected item index.</summary>
    public int SelectedIndex { get; set; }

    /// <summary>Whether this menu is visible and accepting input.</summary>
    public bool IsActive { get; set; }

    /// <summary>Use standard (overworld) panel style instead of battle style.</summary>
    public bool UseStandardStyle { get; set; }

    /// <summary>Callback when cancel/back is pressed.</summary>
    public Action? OnCancel { get; set; }

    /// <summary>The bounds used for the last Draw call (for external hit testing).</summary>
    public Rectangle LastBounds { get; private set; }

    public IReadOnlyList<BattleMenuItem> Items => _items;

    public void SetItems(params BattleMenuItem[] items)
    {
        _items.Clear();
        _items.AddRange(items);
        SelectedIndex = 0;
    }

    /// <summary>
    /// Handle navigation and selection input.
    /// </summary>
    /// <param name="left">Left pressed this frame.</param>
    /// <param name="right">Right pressed this frame.</param>
    /// <param name="up">Up pressed this frame.</param>
    /// <param name="down">Down pressed this frame.</param>
    /// <param name="confirm">Confirm pressed this frame.</param>
    /// <param name="cancel">Cancel pressed this frame.</param>
    /// <param name="mousePosition">Current mouse position (screen coords).</param>
    /// <param name="mouseClicked">Mouse left button clicked this frame.</param>
    public void Update(bool left, bool right, bool up, bool down,
                       bool confirm, bool cancel,
                       Point mousePosition, bool mouseClicked)
    {
        if (!IsActive || _items.Count == 0)
            return;

        int rows = (_items.Count + Columns - 1) / Columns;
        int col = SelectedIndex % Columns;
        int row = SelectedIndex / Columns;

        // Arrow navigation
        if (left && col > 0)
            SelectedIndex--;
        if (right && col < Columns - 1 && SelectedIndex + 1 < _items.Count)
            SelectedIndex++;
        if (up && row > 0)
            SelectedIndex -= Columns;
        if (down && row < rows - 1 && SelectedIndex + Columns < _items.Count)
            SelectedIndex += Columns;

        // Mouse hover → update selection (only if mouse is inside the panel)
        if (mouseClicked && !LastBounds.IsEmpty && LastBounds.Contains(mousePosition))
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (GetItemRect(i).Contains(mousePosition))
                {
                    SelectedIndex = i;
                    ConfirmSelection();
                    break;
                }
            }
        }
        else if (!LastBounds.IsEmpty && LastBounds.Contains(mousePosition))
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (GetItemRect(i).Contains(mousePosition))
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }

        // Keyboard confirm
        if (confirm)
            ConfirmSelection();

        // Cancel
        if (cancel)
            OnCancel?.Invoke();
    }

    /// <summary>
    /// Draw the menu with bordered panel, item labels, and selection highlight.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Rectangle bounds)
    {
        if (!IsActive || _items.Count == 0)
            return;

        LastBounds = bounds;

        // Bordered panel
        if (UseStandardStyle)
            UIStyle.DrawStandardPanel(spriteBatch, pixel, bounds);
        else
            UIStyle.DrawBattlePanel(spriteBatch, pixel, bounds);

        int rows = (_items.Count + Columns - 1) / Columns;
        int itemW = bounds.Width / Columns;
        int itemH = bounds.Height / rows;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            int col = i % Columns;
            int row = i / Columns;
            int ix = bounds.X + col * itemW;
            int iy = bounds.Y + row * itemH;
            var itemRect = new Rectangle(ix, iy, itemW, itemH);

            bool selected = i == SelectedIndex;

            // Text colors based on state and style
            Color textColor, outlineColor;
            if (!item.Enabled)
            {
                textColor = UIStyle.TextDisabled;
                outlineColor = UIStyle.TextDisabledOutline;
            }
            else if (selected)
            {
                textColor = UIStyle.TextSelected;
                outlineColor = UIStyle.TextSelectedOutline;
            }
            else if (UseStandardStyle)
            {
                textColor = UIStyle.TextDarkGray;
                outlineColor = UIStyle.TextDarkGrayOutline;
            }
            else
            {
                textColor = UIStyle.TextNormal;
                outlineColor = UIStyle.TextNormalOutline;
            }

            var textSize = font.MeasureString(item.Label);
            int textX = ix + 30;
            int textY = iy + (int)((itemH - textSize.Y) / 2);

            // Draw arrow cursor for selected item
            if (selected)
            {
                int arrowSize = 15;
                int arrowX = ix + 10;
                int arrowY = textY + (int)(textSize.Y / 2) - arrowSize / 2;
                UIStyle.DrawArrowRight(spriteBatch, pixel, arrowX, arrowY, arrowSize, textColor);
            }

            UIStyle.DrawShadowedText(spriteBatch, font, item.Label,
                new Vector2(textX, textY),
                textColor, outlineColor);
        }
    }

    /// <summary>
    /// Draw the menu with KermFont renderer instead of SpriteFont.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, KermFontRenderer fontRenderer, KermFont font, Texture2D pixel, Rectangle bounds, int fontScale = 1)
    {
        if (!IsActive || _items.Count == 0)
            return;

        LastBounds = bounds;

        if (UseStandardStyle)
            UIStyle.DrawStandardPanel(spriteBatch, pixel, bounds);
        else
            UIStyle.DrawBattlePanel(spriteBatch, pixel, bounds);

        int rows = (_items.Count + Columns - 1) / Columns;
        int itemW = bounds.Width / Columns;
        int itemH = bounds.Height / rows;
        int fontHeight = font.FontHeight * fontScale;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            int col = i % Columns;
            int row = i / Columns;
            int ix = bounds.X + col * itemW;
            int iy = bounds.Y + row * itemH;

            bool selected = i == SelectedIndex;

            // For kermfont, the palette is baked. Use Color.White for normal,
            // tint for selected/disabled.
            Color tint;
            if (!item.Enabled)
                tint = new Color(133, 133, 141);
            else if (selected)
                tint = new Color(255, 224, 22);
            else
                tint = Color.White;

            int arrowSize = fontScale * 3;
            int textX = ix + fontScale * 6;
            int textY = iy + (itemH - fontHeight) / 2;

            if (selected)
            {
                int arrowX = ix + fontScale * 2;
                int arrowY = textY + fontHeight / 2 - arrowSize / 2;
                UIStyle.DrawArrowRight(spriteBatch, pixel, arrowX, arrowY, arrowSize, tint);
            }

            fontRenderer.DrawString(spriteBatch, item.Label, new Vector2(textX, textY), fontScale, tint);
        }
    }

    private void ConfirmSelection()
    {
        if (SelectedIndex >= 0 && SelectedIndex < _items.Count && _items[SelectedIndex].Enabled)
            _items[SelectedIndex].OnConfirm?.Invoke();
    }

    private Rectangle GetItemRect(int index)
    {
        if (LastBounds.IsEmpty || _items.Count == 0)
            return Rectangle.Empty;

        int rows = (_items.Count + Columns - 1) / Columns;
        int itemW = LastBounds.Width / Columns;
        int itemH = LastBounds.Height / rows;
        int col = index % Columns;
        int row = index / Columns;
        return new Rectangle(LastBounds.X + col * itemW, LastBounds.Y + row * itemH, itemW, itemH);
    }

}
