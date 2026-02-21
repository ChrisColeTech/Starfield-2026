using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI;

/// <summary>
/// Battle menu panel — header text + selectable button grid.
/// Supports non-uniform column weights, separators, and the fight-menu layout.
/// </summary>
public class BattlePanel
{
    private readonly List<MenuItem> _items = new();
    private string _headerText = "";
    private int _columns = 2;
    private int _selectedIndex;
    private float[]? _columnWeights;

    public bool IsActive { get; set; }
    public int SeparatorAfterColumn { get; set; } = -1;
    public Action? OnCancel { get; set; }

    // ── Public API ──

    /// <summary>Show a generic menu with header and items.</summary>
    public void ShowMenu(string header, int columns, params MenuItem[] items)
    {
        _headerText = header;
        _columns = columns;
        _columnWeights = null;
        _items.Clear();
        _items.AddRange(items);
        _selectedIndex = 0;
        IsActive = true;
        SeparatorAfterColumn = -1;
    }

    /// <summary>Show the fight menu (2×4 grid: moves left, actions right).</summary>
    public void ShowFightMenu(
        string[] moveLabels, bool[] moveEnabled, Action<int> onMoveSelected,
        Action onBack)
    {
        _headerText = "What will you do?";
        IsActive = true;
        _columns = 4;
        _columnWeights = new[] { 0.30f, 0.30f, 0.20f, 0.20f };
        SeparatorAfterColumn = 1;
        _items.Clear();

        // Row 0: Move0, Move1, Back, Mega
        AddMoveOrEmpty(moveLabels, moveEnabled, onMoveSelected, 0);
        AddMoveOrEmpty(moveLabels, moveEnabled, onMoveSelected, 1);
        _items.Add(new MenuItem("Back", onBack));
        _items.Add(new MenuItem("Mega", () => { }));

        // Row 1: Move2, Move3, Power, (empty)
        AddMoveOrEmpty(moveLabels, moveEnabled, onMoveSelected, 2);
        AddMoveOrEmpty(moveLabels, moveEnabled, onMoveSelected, 3);
        _items.Add(new MenuItem("Power", () => { }));
        _items.Add(new MenuItem("", enabled: false));

        _selectedIndex = 0;
        OnCancel = onBack;
        SkipSeparators(1);
    }

    private void AddMoveOrEmpty(string[] labels, bool[] enabled, Action<int> onSelect, int idx)
    {
        if (idx < labels.Length)
            _items.Add(new MenuItem(labels[idx], () => onSelect(idx), enabled[idx]));
        else
            _items.Add(new MenuItem("---", enabled: false));
    }

    public void Clear()
    {
        _items.Clear();
        _headerText = "";
        IsActive = false;
        OnCancel = null;
        SeparatorAfterColumn = -1;
        _columnWeights = null;
    }

    // ── Navigation ──

    public void Update(InputSnapshot input)
    {
        if (!IsActive || _items.Count == 0) return;

        int cols = Math.Max(1, _columns);
        int rows = (_items.Count + cols - 1) / cols;
        int col = _selectedIndex % cols;
        int row = _selectedIndex / cols;

        if (input.Up) row--;
        if (input.Down) row++;
        if (input.Left) col--;
        if (input.Right) col++;

        row = Math.Clamp(row, 0, rows - 1);
        col = Math.Clamp(col, 0, cols - 1);

        int newIndex = row * cols + col;
        if (newIndex >= 0 && newIndex < _items.Count)
        {
            var candidate = _items[newIndex];
            if (candidate.Enabled || string.IsNullOrEmpty(candidate.Label))
                _selectedIndex = newIndex;
        }

        if (input.Confirm && _selectedIndex >= 0 && _selectedIndex < _items.Count)
        {
            var item = _items[_selectedIndex];
            if (item.Enabled)
                item.OnConfirm?.Invoke();
        }

        if (input.Cancel)
            OnCancel?.Invoke();
    }

    /// <summary>Skip the separator column when navigating right.</summary>
    public void SkipSeparators(int separatorCol)
    {
        if (_columns <= 0) return;
        int col = _selectedIndex % _columns;
        if (col == separatorCol && col + 1 < _columns)
            _selectedIndex++;
    }

    // ── Drawing ──

    public void Draw(SpriteBatch sb, PixelFont font, Texture2D pixel,
        Rectangle bounds, int fontScale)
    {
        if (!IsActive) return;

        int pad = 3 * fontScale;
        int radius = Math.Max(2, fontScale * 2);
        int shadowOff = Math.Max(1, fontScale);

        // Panel with drop shadow
        UIDraw.ShadowedPanel(sb, pixel, bounds, radius,
            UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);

        // Header text
        font.Scale = fontScale;
        int headerH = 0;
        if (_headerText.Length > 0)
        {
            UIDraw.ShadowedText(sb, font, _headerText,
                new Vector2(bounds.X + pad, bounds.Y + pad),
                UITheme.TextPrimary, UITheme.TextShadow);
            headerH = font.CharHeight + pad;
        }

        if (_items.Count == 0) return;

        // Button grid
        int gridTop = bounds.Y + headerH + pad;
        int gridH = bounds.Bottom - gridTop - pad;
        int gridW = bounds.Width - pad * 2;

        int cols = Math.Max(1, _columns);
        int rows = (_items.Count + cols - 1) / cols;
        int cellH = gridH / Math.Max(1, rows);

        // Pre-compute column X offsets and widths (supports non-uniform weights)
        int[] colX = new int[cols];
        int[] colW = new int[cols];
        if (_columnWeights != null && _columnWeights.Length == cols)
        {
            float total = 0f;
            for (int ci = 0; ci < cols; ci++) total += _columnWeights[ci];
            int accum = 0;
            for (int ci = 0; ci < cols; ci++)
            {
                colX[ci] = accum;
                colW[ci] = (int)(gridW * (_columnWeights[ci] / total));
                accum += colW[ci];
            }
            colW[cols - 1] += gridW - accum;
        }
        else
        {
            int uniformW = gridW / cols;
            for (int ci = 0; ci < cols; ci++)
            {
                colX[ci] = ci * uniformW;
                colW[ci] = uniformW;
            }
        }

        // Draw separator column
        if (SeparatorAfterColumn >= 0 && SeparatorAfterColumn < cols)
        {
            int sepCol = SeparatorAfterColumn;
            int sepX = bounds.X + pad + colX[sepCol] + colW[sepCol];
            sb.Draw(pixel, new Rectangle(sepX, gridTop, 1, gridH), UITheme.PurpleMuted);
        }

        // Draw items
        for (int i = 0; i < _items.Count; i++)
        {
            int c = i % cols;
            int r = i / cols;
            int x = bounds.X + pad + colX[c];
            int y = gridTop + r * cellH;
            int cellW = colW[c];

            var item = _items[i];

            if (item.IsSeparator)
            {
                int lineY = y + cellH / 2;
                sb.Draw(pixel, new Rectangle(bounds.X + pad, lineY, bounds.Width - pad * 2, 1), UITheme.PurpleMuted);
                continue;
            }

            bool selected = i == _selectedIndex;

            if (selected)
            {
                var hlRect = new Rectangle(x + 1, y + 1, cellW - 2, cellH - 2);
                int hlRadius = Math.Max(1, fontScale);
                UIDraw.RoundedRect(sb, pixel, hlRect, hlRadius, UITheme.PurpleSelected);
                UIDraw.GlowBorder(sb, pixel, hlRect, hlRadius, UITheme.PurpleGlow);
            }

            font.Scale = fontScale;
            Color textColor = !item.Enabled ? UITheme.TextDisabled :
                              selected ? Color.White : UITheme.TextPrimary;

            string label = item.Label;
            int textW = font.MeasureWidth(label);
            int textX = x + (cellW - textW) / 2;
            int textY = y + (cellH - font.CharHeight) / 2;
            UIDraw.ShadowedText(sb, font, label,
                new Vector2(textX, textY),
                textColor, UITheme.TextShadow);
        }
    }
}
