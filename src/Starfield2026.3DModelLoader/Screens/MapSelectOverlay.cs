#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Screens;

public record MapRecord(int Id, string Name, string Category, string Subfolder, string ManifestPath);

public class MapSelectOverlay
{
    private enum Level { Category, Subfolder, Items }

    private readonly CategoryGroup[] _categories;
    private Level _level = Level.Category;
    private int _catIndex;
    private int _subIndex;
    private int _itemIndex;
    private int _scrollOffset;
    private bool _finished;

    // Key-repeat acceleration
    private float _holdTimer;
    private float _repeatInterval;
    private int _holdDirection;
    private const float InitialDelay = 0.3f;
    private const float FastRepeat = 0.02f;
    private const float SlowRepeat = 0.12f;
    private const float AccelTime = 1.5f;

    public string? SelectedFolder { get; private set; }
    public bool IsFinished => _finished;

    public MapSelectOverlay(List<MapRecord> maps)
    {
        var catMap = new Dictionary<string, Dictionary<string, List<MapRecord>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in maps)
        {
            if (!catMap.TryGetValue(m.Category, out var subs))
            {
                subs = new Dictionary<string, List<MapRecord>>(StringComparer.OrdinalIgnoreCase);
                catMap[m.Category] = subs;
            }
            string sub = string.IsNullOrEmpty(m.Subfolder) ? "(all)" : m.Subfolder;
            if (!subs.TryGetValue(sub, out var list))
            {
                list = new List<MapRecord>();
                subs[sub] = list;
            }
            list.Add(m);
        }

        _categories = catMap
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CategoryGroup(
                g.Key,
                g.Value.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new SubfolderGroup(s.Key, s.Value.ToArray()))
                    .ToArray()))
            .ToArray();
    }

    public void Update(InputSnapshot input, float dt)
    {
        if (_finished) return;

        if (input.Cancel)
        {
            if (_level == Level.Items)
            {
                _level = Level.Subfolder;
                _itemIndex = 0;
                _scrollOffset = 0;
            }
            else if (_level == Level.Subfolder)
            {
                _level = Level.Category;
                _subIndex = 0;
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

            if (input.Confirm && _categories.Length > 0)
            {
                var cat = _categories[_catIndex];
                if (cat.Subfolders.Length == 1)
                {
                    _subIndex = 0;
                    _level = Level.Items;
                }
                else
                {
                    _subIndex = 0;
                    _level = Level.Subfolder;
                }
                _itemIndex = 0;
                _scrollOffset = 0;
            }
        }
        else if (_level == Level.Subfolder)
        {
            var cat = _categories[_catIndex];
            if (input.Up && _subIndex > 0) _subIndex--;
            if (input.Down && _subIndex < cat.Subfolders.Length - 1) _subIndex++;

            if (input.Confirm && cat.Subfolders.Length > 0)
            {
                _level = Level.Items;
                _itemIndex = 0;
                _scrollOffset = 0;
            }
        }
        else
        {
            var maps = _categories[_catIndex].Subfolders[_subIndex].Maps;
            int count = maps.Length;

            bool upHeld = input.IsKeyHeld(Keys.Up) || input.IsKeyHeld(Keys.W);
            bool downHeld = input.IsKeyHeld(Keys.Down) || input.IsKeyHeld(Keys.S);
            int dir = downHeld ? 1 : upHeld ? -1 : 0;

            if (dir != _holdDirection || dir == 0)
            {
                _holdDirection = dir;
                _holdTimer = 0f;
                _repeatInterval = SlowRepeat;
                if (input.Up && _itemIndex > 0) _itemIndex--;
                if (input.Down && _itemIndex < count - 1) _itemIndex++;
            }
            else
            {
                _holdTimer += dt;
                if (_holdTimer > InitialDelay)
                {
                    float holdTime = _holdTimer - InitialDelay;
                    float t = Math.Min(holdTime / AccelTime, 1f);
                    _repeatInterval = MathHelper.Lerp(SlowRepeat, FastRepeat, t);
                    _repeatInterval -= dt;
                    if (_repeatInterval <= 0f)
                    {
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
                var m = maps[_itemIndex];
                SelectedFolder = System.IO.Path.GetDirectoryName(m.ManifestPath);
                _finished = true;
            }
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, PixelFont font, int screenW, int screenH)
    {
        int scale = Math.Max(2, screenW / 400);
        font.Scale = scale;
        int lineH = font.CharHeight + 4 * scale;
        int pad = 6 * scale;

        sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * 0.80f);

        int margin = 12 * scale;
        int totalW = screenW - margin * 2;
        int totalH = screenH - margin * 2;
        int leftW = (int)(totalW * 0.55f);
        int rightW = totalW - leftW - pad;
        int leftX = margin;
        int rightX = margin + leftW + pad;
        int topY = margin;

        sb.Draw(pixel, new Rectangle(leftX, topY, leftW, totalH), new Color(20, 22, 32, 240));
        DrawBorder(sb, pixel, leftX, topY, leftW, totalH, new Color(60, 180, 120));

        int lx = leftX + pad;
        int ly = topY + pad;
        int lw = leftW - pad * 2;
        int lh = totalH - pad * 2;

        if (_level == Level.Category)
            DrawCategories(sb, pixel, font, lx, ly, lw, lh, lineH, scale);
        else if (_level == Level.Subfolder)
            DrawSubfolders(sb, pixel, font, lx, ly, lw, lh, lineH, scale);
        else
            DrawItems(sb, pixel, font, lx, ly, lw, lh, lineH, scale);

        sb.Draw(pixel, new Rectangle(rightX, topY, rightW, totalH), new Color(20, 22, 32, 240));
        DrawBorder(sb, pixel, rightX, topY, rightW, totalH, new Color(80, 80, 140));

        DrawInfoPanel(sb, pixel, font, rightX + pad, topY + pad, rightW - pad * 2, totalH - pad * 2, lineH, scale);
    }

    private void DrawInfoPanel(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int lineH, int scale)
    {
        font.Scale = scale;
        font.Draw("MAP INFO", x, y, new Color(200, 200, 240));
        y += lineH;

        font.Scale = Math.Max(1, scale - 1);
        int smallLineH = font.CharHeight + 3 * scale;

        y += smallLineH / 2;

        if (_level == Level.Items && _categories.Length > 0)
        {
            var maps = _categories[_catIndex].Subfolders[_subIndex].Maps;
            if (_itemIndex < maps.Length)
            {
                var m = maps[_itemIndex];
                font.Draw(m.Name, x, y, Color.White);
                y += smallLineH;
                font.Draw($"Source: {m.Category}", x, y, new Color(180, 180, 200));
                y += smallLineH;
                if (!string.IsNullOrEmpty(m.Subfolder) && m.Subfolder != "(all)")
                {
                    font.Draw($"Group: {m.Subfolder}", x, y, new Color(180, 180, 200));
                    y += smallLineH;
                }
            }
        }
        else
        {
            font.Draw("(pick a map)", x, y, Color.Gray);
        }

        font.Scale = scale;
    }

    private void DrawCategories(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int lineH, int scale)
    {
        font.Draw("SELECT MAP SOURCE", x, y, Color.White);
        y += lineH;

        font.Scale = Math.Max(1, scale - 1);
        font.Draw("Up/Down  Enter  Esc", x, y, Color.Gray);
        font.Scale = scale;
        y += lineH;

        for (int i = 0; i < _categories.Length; i++)
        {
            int ry = y + i * lineH;
            if (ry + lineH > y + h - lineH) break;

            bool sel = i == _catIndex;
            if (sel)
                sb.Draw(pixel, new Rectangle(x, ry, w, lineH - 2), new Color(60, 180, 120, 100));

            int totalMaps = 0;
            foreach (var sub in _categories[i].Subfolders)
                totalMaps += sub.Maps.Length;
            string label = $"{_categories[i].Label}  ({totalMaps})";
            font.Draw(label, x + 4 * scale, ry + 2 * scale,
                sel ? Color.White : new Color(200, 200, 210));
        }
    }

    private void DrawSubfolders(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int lineH, int scale)
    {
        var cat = _categories[_catIndex];
        font.Draw(cat.Label.ToUpperInvariant(), x, y, Color.White);
        y += lineH;

        font.Scale = Math.Max(1, scale - 1);
        font.Draw("Up/Down  Enter  Esc:Back", x, y, Color.Gray);
        font.Scale = scale;
        y += lineH;

        for (int i = 0; i < cat.Subfolders.Length; i++)
        {
            int ry = y + i * lineH;
            if (ry + lineH > y + h - lineH) break;

            bool sel = i == _subIndex;
            if (sel)
                sb.Draw(pixel, new Rectangle(x, ry, w, lineH - 2), new Color(60, 180, 120, 100));

            var sub = cat.Subfolders[i];
            string label = $"{sub.Label}  ({sub.Maps.Length})";
            font.Draw(label, x + 4 * scale, ry + 2 * scale,
                sel ? Color.White : new Color(200, 200, 210));
        }
    }

    private void DrawItems(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int lineH, int scale)
    {
        var cat = _categories[_catIndex];
        var sub = cat.Subfolders[_subIndex];

        string header = cat.Subfolders.Length > 1
            ? $"{cat.Label}/{sub.Label}".ToUpperInvariant()
            : cat.Label.ToUpperInvariant();
        font.Draw(header, x, y, Color.White);
        y += lineH;

        font.Scale = Math.Max(1, scale - 1);
        font.Draw("Up/Down  Enter  Esc:Back", x, y, Color.Gray);
        font.Scale = scale;
        y += lineH;

        int visibleRows = Math.Max(1, (h - lineH * 2) / lineH);

        if (_itemIndex < _scrollOffset) _scrollOffset = _itemIndex;
        if (_itemIndex >= _scrollOffset + visibleRows) _scrollOffset = _itemIndex - visibleRows + 1;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, sub.Maps.Length - visibleRows));

        for (int vi = 0; vi < visibleRows && _scrollOffset + vi < sub.Maps.Length; vi++)
        {
            int idx = _scrollOffset + vi;
            int ry = y + vi * lineH;
            bool sel = idx == _itemIndex;

            if (sel)
                sb.Draw(pixel, new Rectangle(x, ry, w, lineH - 2), new Color(60, 180, 120, 100));

            var m = sub.Maps[idx];
            font.Draw(m.Name, x + 4 * scale, ry + 2 * scale,
                sel ? Color.White : new Color(200, 200, 210));
        }

        if (sub.Maps.Length > visibleRows)
        {
            string info = $"{_itemIndex + 1}/{sub.Maps.Length}";
            int infoW = font.MeasureWidth(info);
            font.Draw(info, x + w - infoW, y - lineH, Color.Gray);
        }
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel,
        int x, int y, int w, int h, Color color)
    {
        int t = 2;
        sb.Draw(pixel, new Rectangle(x, y, w, t), color);
        sb.Draw(pixel, new Rectangle(x, y + h - t, w, t), color);
        sb.Draw(pixel, new Rectangle(x, y, t, h), color);
        sb.Draw(pixel, new Rectangle(x + w - t, y, t, h), color);
    }

    private sealed class SubfolderGroup
    {
        public readonly string Label;
        public readonly MapRecord[] Maps;
        public SubfolderGroup(string label, MapRecord[] maps)
        {
            Label = label;
            Maps = maps;
        }
    }

    private sealed class CategoryGroup
    {
        public readonly string Label;
        public readonly SubfolderGroup[] Subfolders;
        public CategoryGroup(string label, SubfolderGroup[] subfolders)
        {
            Label = label;
            Subfolders = subfolders;
        }
    }
}
