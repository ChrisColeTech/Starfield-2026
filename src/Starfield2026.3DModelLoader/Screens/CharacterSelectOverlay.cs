#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;
using Starfield2026.ModelLoader.Save;

namespace Starfield2026.ModelLoader.Screens;

/// <summary>
/// Full-screen character select with animation mode controls.
/// Left column: categories / item list.
/// Right column: animation mode + fill tag settings.
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

    // Key-repeat acceleration
    private float _holdTimer;
    private float _repeatInterval;
    private int _holdDirection;
    private const float InitialDelay = 0.3f;
    private const float FastRepeat = 0.02f;
    private const float SlowRepeat = 0.12f;
    private const float AccelTime = 1.5f;

    // Animation settings (read/written by overlay, owned by FreeRoamScreen)
    private AnimationLoadMode _loadMode;
    private HashSet<string> _fillTags;

    public string? SelectedFolder { get; private set; }
    public bool IsFinished => _finished;
    public bool AnimationSettingsChanged { get; private set; }
    public AnimationLoadMode LoadMode => _loadMode;
    public HashSet<string> FillTags => _fillTags;

    public CharacterSelectOverlay(List<CharacterRecord> characters, AnimationLoadMode loadMode, HashSet<string> fillTags)
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

        _loadMode = loadMode;
        _fillTags = new HashSet<string>(fillTags);
    }

    public void Update(InputSnapshot input, float dt)
    {
        if (_finished) return;

        // Animation mode hotkeys
        if (input.IsKeyJustPressed(Keys.D1)) SetMode(AnimationLoadMode.Own);
        if (input.IsKeyJustPressed(Keys.D2)) SetMode(AnimationLoadMode.FillMissing);
        if (input.IsKeyJustPressed(Keys.D3)) SetMode(AnimationLoadMode.SharedOnly);

        // --- Navigation ---
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
                var ch = cat.Characters[_itemIndex];
                SelectedFolder = System.IO.Path.GetDirectoryName(ch.ManifestPath);
                _finished = true;
            }
        }
    }

    private void SetMode(AnimationLoadMode mode)
    {
        if (_loadMode != mode)
        {
            _loadMode = mode;
            AnimationSettingsChanged = true;
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, PixelFont font, int screenW, int screenH)
    {
        int scale = Math.Max(2, screenW / 400);
        font.Scale = scale;
        int lineH = font.CharHeight + 4 * scale;
        int pad = 6 * scale;

        // Full-screen dim
        sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * 0.80f);

        // Layout: two columns with margin
        int margin = 12 * scale;
        int totalW = screenW - margin * 2;
        int totalH = screenH - margin * 2;
        int leftW = (int)(totalW * 0.55f);
        int rightW = totalW - leftW - pad;
        int leftX = margin;
        int rightX = margin + leftW + pad;
        int topY = margin;

        // ===== LEFT COLUMN: character list =====
        sb.Draw(pixel, new Rectangle(leftX, topY, leftW, totalH), new Color(20, 22, 32, 240));
        DrawBorder(sb, pixel, leftX, topY, leftW, totalH, new Color(120, 60, 220));

        int lx = leftX + pad;
        int ly = topY + pad;
        int lw = leftW - pad * 2;
        int lh = totalH - pad * 2;

        if (_level == Level.Category)
            DrawCategories(sb, pixel, font, lx, ly, lw, lh, lineH, scale);
        else
            DrawItems(sb, pixel, font, lx, ly, lw, lh, lineH, scale);

        // ===== RIGHT COLUMN: animation settings =====
        sb.Draw(pixel, new Rectangle(rightX, topY, rightW, totalH), new Color(20, 22, 32, 240));
        DrawBorder(sb, pixel, rightX, topY, rightW, totalH, new Color(80, 80, 140));

        DrawSettingsPanel(sb, pixel, font, rightX + pad, topY + pad, rightW - pad * 2, totalH - pad * 2, lineH, scale);
    }

    private void DrawSettingsPanel(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int lineH, int scale)
    {
        font.Scale = scale;
        font.Draw("ANIMATION", x, y, new Color(200, 200, 240));
        y += lineH;

        font.Scale = Math.Max(1, scale - 1);
        int smallLineH = font.CharHeight + 3 * scale;

        // --- Mode section ---
        y += smallLineH / 2;
        font.Draw("MODE", x, y, new Color(140, 140, 180));
        y += smallLineH;

        string[] modeLabels = { "[1] Own", "[2] Fill Missing", "[3] Shared Only" };
        for (int i = 0; i < modeLabels.Length; i++)
        {
            bool active = (int)_loadMode == i;
            DrawModeOption(sb, pixel, font, x, y, w, smallLineH, modeLabels[i], active);
            y += smallLineH;
        }

        // --- Body type info for selected character ---
        y += smallLineH;
        font.Draw("SELECTED", x, y, new Color(140, 140, 180));
        y += smallLineH;

        if (_level == Level.Items && _categories.Length > 0)
        {
            var cat = _categories[_catIndex];
            if (_itemIndex < cat.Characters.Length)
            {
                var ch = cat.Characters[_itemIndex];
                string? chFolder = System.IO.Path.GetDirectoryName(ch.ManifestPath);
                var bodyType = chFolder != null ? TrainerGender.Classify(chFolder) : TrainerGender.BodyType.Unknown;
                font.Draw(ch.Name, x, y, Color.White);
                y += smallLineH;
                font.Draw($"Body: {bodyType}", x, y, new Color(180, 180, 200));
            }
        }
        else
        {
            font.Draw("(pick a character)", x, y, Color.Gray);
        }

        font.Scale = scale;
    }

    private static void DrawModeOption(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, string label, bool active)
    {
        if (active)
        {
            sb.Draw(pixel, new Rectangle(x, y, w, h - 1), new Color(40, 100, 40, 140));
            font.Draw(label, x + 2, y + 1, Color.LightGreen);
        }
        else
        {
            font.Draw(label, x + 2, y + 1, new Color(160, 160, 170));
        }
    }

    private void DrawCategories(SpriteBatch sb, Texture2D pixel, PixelFont font,
        int x, int y, int w, int h, int lineH, int scale)
    {
        font.Draw("SELECT CATEGORY", x, y, Color.White);
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
        font.Draw("Up/Down  Enter  Esc:Back", x, y, Color.Gray);
        font.Scale = scale;
        y += lineH;

        int visibleRows = Math.Max(1, (h - lineH * 2) / lineH);

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

            var ch = cat.Characters[idx];
            string itemLabel = GetShortPath(ch.ManifestPath, ch.Name);
            font.Draw(itemLabel, x + 4 * scale, ry + 2 * scale,
                sel ? Color.White : new Color(200, 200, 210));
        }

        if (cat.Characters.Length > visibleRows)
        {
            string info = $"{_itemIndex + 1}/{cat.Characters.Length}";
            int infoW = font.MeasureWidth(info);
            font.Draw(info, x + w - infoW, y - lineH, Color.Gray);
        }
    }

    private static List<string> WordWrap(string text, int maxWidth, PixelFont font)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        string current = "";

        foreach (var word in words)
        {
            string test = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureWidth(test) > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = test;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    private static string GetShortPath(string manifestPath, string fallbackName)
    {
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
        sb.Draw(pixel, new Rectangle(x, y, w, t), color);
        sb.Draw(pixel, new Rectangle(x, y + h - t, w, t), color);
        sb.Draw(pixel, new Rectangle(x, y, t, h), color);
        sb.Draw(pixel, new Rectangle(x + w - t, y, t, h), color);
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
