using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI;

/// <summary>
/// Reusable popup modal that can display:
///   - A list of selectable options (menu mode)
///   - A quantity picker with up/down (quantity mode)
/// Renders as a dark slate panel with purple glow, positioned relative to an anchor rect.
/// </summary>
public class PopupModal
{
    private string _title = "";
    private string[] _options = Array.Empty<string>();
    private int _selectedIndex;
    private Action<int>? _onConfirm;
    private Action? _onCancel;

    // Quantity mode
    private bool _isQuantityMode;
    private int _quantity = 1;
    private int _quantityMax = 1;

    // Positioning
    private Rectangle _anchor;

    public bool IsOpen { get; private set; }

    // ── Menu Mode ──

    /// <summary>Open as a selectable list of options.</summary>
    public void ShowMenu(string title, string[] options, Rectangle anchor,
        Action<int> onConfirm, Action onCancel)
    {
        _title = title;
        _options = options;
        _anchor = anchor;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _selectedIndex = 0;
        _isQuantityMode = false;
        IsOpen = true;
    }

    // ── Quantity Mode ──

    /// <summary>Open as a quantity picker (1 to max).</summary>
    public void ShowQuantity(string title, int max, Rectangle anchor,
        Action<int> onConfirm, Action onCancel)
    {
        _title = title;
        _quantityMax = Math.Max(1, max);
        _quantity = 1;
        _anchor = anchor;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _options = Array.Empty<string>();
        _isQuantityMode = true;
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        _onConfirm = null;
        _onCancel = null;
    }

    // ── Input ──

    public void Update(InputSnapshot input)
    {
        if (!IsOpen) return;

        if (input.Cancel) { var cb = _onCancel; Close(); cb?.Invoke(); return; }

        if (_isQuantityMode)
        {
            // _selectedIndex: 0 = quantity row, 1 = OK, 2 = Cancel
            if (_selectedIndex == 0)
            {
                if (input.Left) _quantity = Math.Max(1, _quantity - 1);
                if (input.Right) _quantity = Math.Min(_quantityMax, _quantity + 1);
                if (input.Up) _quantity = Math.Min(_quantityMax, _quantity + 1);
                if (input.Down) _selectedIndex = 1;
                if (input.Confirm) { var cb = _onConfirm; int q = _quantity; Close(); cb?.Invoke(q); }
            }
            else
            {
                if (input.Up) _selectedIndex--;
                if (input.Down && _selectedIndex < 2) _selectedIndex++;
                if (input.Confirm)
                {
                    if (_selectedIndex == 1) { var cb = _onConfirm; int q = _quantity; Close(); cb?.Invoke(q); }
                    else { var cb = _onCancel; Close(); cb?.Invoke(); }
                }
            }
        }
        else
        {
            if (input.Up) _selectedIndex = Math.Max(0, _selectedIndex - 1);
            if (input.Down) _selectedIndex = Math.Min(_options.Length - 1, _selectedIndex + 1);
            if (input.Confirm) { var cb = _onConfirm; int idx = _selectedIndex; Close(); cb?.Invoke(idx); }
        }
    }

    // ── Drawing ──

    public void Draw(SpriteBatch sb, Texture2D pixel, PixelFont font, int fontScale, int screenW, int screenH)
    {
        if (!IsOpen) return;

        int scale = fontScale;
        font.Scale = scale;

        int pad = 6 * scale;
        int rowH = font.CharHeight + 4 * scale;
        int radius = Math.Max(2, scale * 2);
        int shadowOff = Math.Max(2, scale * 2);

        // Measure content
        int titleH = _title.Length > 0 ? font.CharHeight + pad : 0;
        int contentRows = _isQuantityMode ? 3 : _options.Length; // quantity: number + OK + Cancel
        int popupH = pad + titleH + rowH * contentRows + pad;

        int maxLabelW = 0;
        if (_isQuantityMode)
        {
            maxLabelW = font.MeasureWidth($"< x{_quantityMax} >");
            int okW = font.MeasureWidth("OK");
            int cancelW = font.MeasureWidth("Cancel");
            maxLabelW = Math.Max(maxLabelW, Math.Max(okW, cancelW));
        }
        else
        {
            foreach (var opt in _options)
            {
                int w = font.MeasureWidth(opt);
                if (w > maxLabelW) maxLabelW = w;
            }
        }
        if (_title.Length > 0)
        {
            int tw = font.MeasureWidth(_title);
            if (tw > maxLabelW) maxLabelW = tw;
        }
        int popupW = maxLabelW + pad * 6;

        // Position: to the right of anchor, vertically centered
        int popupX = _anchor.Right + pad;
        int popupY = _anchor.Y + (_anchor.Height - popupH) / 2;

        // Clamp to screen
        if (popupX + popupW > screenW - pad) popupX = _anchor.X - popupW - pad;
        if (popupY + popupH > screenH - pad) popupY = screenH - pad - popupH;
        if (popupY < pad) popupY = pad;

        var popupRect = new Rectangle(popupX, popupY, popupW, popupH);

        // Background + glow
        UIDraw.ShadowedPanel(sb, pixel, popupRect, radius,
            UITheme.SlatePanelBg, shadowOff, Color.Black * 0.5f);
        UIDraw.GlowBorder(sb, pixel, popupRect, radius, UITheme.PurpleGlow);

        int cy = popupY + pad;

        // Title
        if (_title.Length > 0)
        {
            int tw = font.MeasureWidth(_title);
            UIDraw.ShadowedText(sb, font, _title,
                new Vector2(popupX + (popupW - tw) / 2, cy),
                UITheme.TextSecondary, UITheme.TextShadow);
            cy += titleH;
        }

        if (_isQuantityMode)
        {
            // Row 0: < x1 >
            string qLabel = $"< x{_quantity} >";
            DrawRow(sb, pixel, font, popupX, cy, popupW, rowH, pad, radius, scale,
                qLabel, _selectedIndex == 0);
            cy += rowH;

            // Row 1: OK
            DrawRow(sb, pixel, font, popupX, cy, popupW, rowH, pad, radius, scale,
                "OK", _selectedIndex == 1);
            cy += rowH;

            // Row 2: Cancel
            DrawRow(sb, pixel, font, popupX, cy, popupW, rowH, pad, radius, scale,
                "Cancel", _selectedIndex == 2);
        }
        else
        {
            for (int i = 0; i < _options.Length; i++)
            {
                bool sel = i == _selectedIndex;
                DrawRow(sb, pixel, font, popupX, cy, popupW, rowH, pad, radius, scale,
                    _options[i], sel);
                cy += rowH;
            }
        }
    }

    private static void DrawRow(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int popupX, int y, int popupW, int rowH, int pad, int radius, int scale,
        string label, bool selected)
    {
        if (selected)
        {
            var hlRect = new Rectangle(popupX + pad, y, popupW - pad * 2, rowH);
            UIDraw.RoundedRect(sb, pixel, hlRect, scale, UITheme.PurpleSelected);
        }

        int lw = font.MeasureWidth(label);
        int tx = popupX + (popupW - lw) / 2;
        int ty = y + (rowH - font.CharHeight) / 2;
        UIDraw.ShadowedText(sb, font, label,
            new Vector2(tx, ty),
            selected ? Color.White : UITheme.TextPrimary, UITheme.TextShadow);
    }
}
