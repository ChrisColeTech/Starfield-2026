#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;
using Starfield2026.ModelLoader.Save;

namespace Starfield2026.ModelLoader.Screens;

/// <summary>
/// Simple two-level character select overlay.
/// Level 1: category cards. Level 2: scrollable item list.
/// </summary>
public class CharacterSelectOverlay
{
    private enum Level { Category, Items }

    private readonly CategoryGroup[] _categories;
    private Level _level = Level.Category;
    private int _catIndex;
    private int _itemIndex;
    private int _scrollOffset;
    private bool _finished;

    // Key-repeat acceleration for held Up/Down
    private float _holdTimer;
    private float _repeatInterval;
    private int _holdDirection; // -1=up, 1=down, 0=none
    private const float InitialDelay = 0.3f;
    private const float FastRepeat = 0.02f;
    private const float SlowRepeat = 0.12f;
    private const float AccelTime = 1.5f; // seconds to reach max speed

    /// <summary>Set when user picks a character. Null if cancelled.</summary>
    public string? SelectedFolder { get; private set; }
    public bool IsFinished => _finished;

    public CharacterSelectOverlay(List<CharacterRecord> characters)
    {
        var groups = new Dictionary<string, List<CharacterRecord>>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in characters)
        {
            if (!groups.TryGetValue(c.Category, out var list))
            {
                list = new List<CharacterRecord>();
                groups[c.Category] = list;
            }
            list.Add(c);
        }

        _categories = groups
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CategoryGroup(g.Key, g.Value.ToArray()))
            .ToArray();
    }

    public void Update(InputSnapshot input, float dt)
    {
        if (_finished) return;

        if (input.Cancel)
        {
            if (_level == Level.Items)
            {
                _level = Level.Category;
                _itemIndex = 0;
                _scrollOffset = 0;
            }
            else
            {
                SelectedFolder = null;
                _finished = true;
            }
            return;
        }

        if (_level == Level.Category)
        {
            if (input.Up && _catIndex > 0) _catIndex--;
            if (input.Down && _catIndex < _categories.Length - 1) _catIndex++;
            if (input.Left && _catIndex > 0) _catIndex--;
            if (input.Right && _catIndex < _categories.Length - 1) _catIndex++;

            if (input.Confirm && _categories.Length > 0)
            {
                _level = Level.Items;
                _itemIndex = 0;
                _scrollOffset = 0;
            }
        }
        else
        {
            var cat = _categories[_catIndex];
            int count = cat.Characters.Length;

            // Determine held direction from raw keyboard state
            bool upHeld = input.IsKeyHeld(Microsoft.Xna.Framework.Input.Keys.Up) || input.IsKeyHeld(Microsoft.Xna.Framework.Input.Keys.W);
            bool downHeld = input.IsKeyHeld(Microsoft.Xna.Framework.Input.Keys.Down) || input.IsKeyHeld(Microsoft.Xna.Framework.Input.Keys.S);

            int dir = downHeld ? 1 : upHeld ? -1 : 0;

            if (dir != _holdDirection || dir == 0)
            {
                // Direction changed or released — reset
                _holdDirection = dir;
                _holdTimer = 0f;
                _repeatInterval = SlowRepeat;

                // Apply initial press
                if (input.Up && _itemIndex > 0) _itemIndex--;
                if (input.Down && _itemIndex < count - 1) _itemIndex++;
            }
            else
            {
                // Same direction held — accumulate time and accelerate
                _holdTimer += dt;
                if (_holdTimer > InitialDelay)
                {
                    float holdTime = _holdTimer - InitialDelay;
                    float t = Math.Min(holdTime / AccelTime, 1f);
                    _repeatInterval = MathHelper.Lerp(SlowRepeat, FastRepeat, t);

                    _repeatInterval -= dt;
                    if (_repeatInterval <= 0f)
                    {
                        // Fire repeat(s)
                        int steps = Math.Max(1, (int)(-_repeatInterval / Math.Max(FastRepeat, 0.001f)) + 1);
                        for (int s = 0; s < steps; s++)
                        {
                            _itemIndex += dir;
                            _itemIndex = Math.Clamp(_itemIndex, 0, count - 1);
                        }
                        _repeatInterval = MathHelper.Lerp(SlowRepeat, FastRepeat, t);
                    }
                }
            }

            if (input.Confirm && count > 0)
            {
                var ch = cat.Characters[_itemIndex];
                SelectedFolder = System.IO.Path.GetDirectoryName(ch.ManifestPath);
                _finished = true;
            }
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, PixelFont font, int screenW, int screenH)
    {
        int scale = Math.Max(2, screenW / 400);
        font.Scale = scale;
        int lineH = font.CharHeight + 4 * scale;

        // Dim backdrop
        sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * 0.75f);

        int pad = 8 * scale;
        int panelW = Math.Min(screenW - pad * 4, 320 * scale);
        int panelH = Math.Min(screenH - pad * 4, 280 * scale);
        int panelX = (screenW - panelW) / 2;
        int panelY = (screenH - panelH) / 2;

        // Panel background
        sb.Draw(pixel, new Rectangle(panelX, panelY, panelW, panelH), new Color(20, 22, 32, 230));
        // Border
        DrawBorder(sb, pixel, panelX, panelY, panelW, panelH, new Color(120, 60, 220));

        int cx = panelX + pad;
        int cy = panelY + pad;
        int contentW = panelW - pad * 2;

        if (_level == Level.Category)
            DrawCategories(sb, pixel, font, cx, cy, contentW, panelH - pad * 2, lineH, scale);
        else
            DrawItems(sb, pixel, font, cx, cy, contentW, panelH - pad * 2, lineH, scale);
    }

    private void DrawCategories(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int lineH, int scale)
    {
        font.Draw("SELECT CATEGORY", x, y, Color.White);
        y += lineH;

        font.Scale = Math.Max(1, scale - 1);
        font.Draw("Up/Down: Navigate  Enter: Select  Esc: Close", x, y, Color.Gray);
        font.Scale = scale;
        y += lineH;

        for (int i = 0; i < _categories.Length; i++)
        {
            int ry = y + i * lineH;
            if (ry + lineH > y + h - lineH) break;

            bool sel = i == _catIndex;
            if (sel)
                sb.Draw(pixel, new Rectangle(x, ry, w, lineH - 2), new Color(120, 60, 220, 100));

            string label = $"{_categories[i].Label}  ({_categories[i].Characters.Length})";
            font.Draw(label, x + 4 * scale, ry + 2 * scale,
                sel ? Color.White : new Color(200, 200, 210));
        }
    }

    private void DrawItems(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int lineH, int scale)
    {
        var cat = _categories[_catIndex];

        font.Draw(cat.Label.ToUpperInvariant(), x, y, Color.White);
        y += lineH;

        font.Scale = Math.Max(1, scale - 1);
        font.Draw("Up/Down: Navigate  Enter: Select  Esc: Back", x, y, Color.Gray);
        font.Scale = scale;
        y += lineH;

        int visibleRows = Math.Max(1, (h - lineH * 2) / lineH);

        // Keep selection visible
        if (_itemIndex < _scrollOffset) _scrollOffset = _itemIndex;
        if (_itemIndex >= _scrollOffset + visibleRows) _scrollOffset = _itemIndex - visibleRows + 1;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, cat.Characters.Length - visibleRows));

        for (int vi = 0; vi < visibleRows && _scrollOffset + vi < cat.Characters.Length; vi++)
        {
            int idx = _scrollOffset + vi;
            int ry = y + vi * lineH;
            bool sel = idx == _itemIndex;

            if (sel)
                sb.Draw(pixel, new Rectangle(x, ry, w, lineH - 2), new Color(120, 60, 220, 100));

            string itemLabel = GetShortPath(cat.Characters[idx].ManifestPath, cat.Characters[idx].Name);
            font.Draw(itemLabel, x + 4 * scale, ry + 2 * scale,
                sel ? Color.White : new Color(200, 200, 210));
        }

        // Scroll indicator
        if (cat.Characters.Length > visibleRows)
        {
            string info = $"{_itemIndex + 1}/{cat.Characters.Length}";
            int infoW = font.MeasureWidth(info);
            font.Draw(info, x + w - infoW, y - lineH, Color.Gray);
        }
    }

    private static string GetShortPath(string manifestPath, string fallbackName)
    {
        // Show two parent folders above manifest.json: e.g. "field/tr0003_00/tr0003_00"
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(manifestPath);
            if (dir == null) return fallbackName;
            string folder1 = System.IO.Path.GetFileName(dir);
            string? parent = System.IO.Path.GetDirectoryName(dir);
            if (parent == null) return $"{folder1}/{fallbackName}";
            string folder2 = System.IO.Path.GetFileName(parent);
            return $"{folder2}/{folder1}/{fallbackName}";
        }
        catch { return fallbackName; }
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel,
        int x, int y, int w, int h, Color color)
    {
        int t = 2;
        sb.Draw(pixel, new Rectangle(x, y, w, t), color);           // top
        sb.Draw(pixel, new Rectangle(x, y + h - t, w, t), color);   // bottom
        sb.Draw(pixel, new Rectangle(x, y, t, h), color);           // left
        sb.Draw(pixel, new Rectangle(x + w - t, y, t, h), color);   // right
    }

    private sealed class CategoryGroup
    {
        public readonly string Label;
        public readonly CharacterRecord[] Characters;
        public CategoryGroup(string label, CharacterRecord[] characters)
        {
            Label = label;
            Characters = characters;
        }
    }
}
