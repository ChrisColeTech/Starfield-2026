using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Rendering;

/// <summary>
/// Renders readable text using 5×7 pixel glyphs drawn with a 1px white texture.
/// Supports A-Z, 0-9, and common punctuation.
/// </summary>
public class PixelFont
{
    private readonly Texture2D _pixel;
    private readonly SpriteBatch _spriteBatch;

    private const int GlyphW = 5;
    private const int GlyphH = 7;
    private const int CharSpacing = 1;
    private const int SpaceWidth = 3;
    
    private int _scale = 2;
    
    public int Scale
    {
        get => _scale;
        set
        {
            _scale = Math.Max(1, value);
            UpdateCharSize();
        }
    }
    
    public int CharWidth { get; private set; }
    public int CharHeight { get; private set; }
    
    private void UpdateCharSize()
    {
        CharWidth = (GlyphW + CharSpacing) * _scale;
        CharHeight = GlyphH * _scale;
    }

    public Dictionary<char, uint[]> CurrentGlyphs { get; set; }

    public static readonly Dictionary<char, uint[]> StandardGlyphs = new()
    {
        ['A'] = new uint[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
        ['B'] = new uint[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110 },
        ['C'] = new uint[] { 0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110 },
        ['D'] = new uint[] { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 },
        ['E'] = new uint[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
        ['F'] = new uint[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000 },
        ['G'] = new uint[] { 0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110 },
        ['H'] = new uint[] { 0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
        ['I'] = new uint[] { 0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
        ['J'] = new uint[] { 0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100 },
        ['K'] = new uint[] { 0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001 },
        ['L'] = new uint[] { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 },
        ['M'] = new uint[] { 0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001 },
        ['N'] = new uint[] { 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001 },
        ['O'] = new uint[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
        ['P'] = new uint[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 },
        ['Q'] = new uint[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101 },
        ['R'] = new uint[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 },
        ['S'] = new uint[] { 0b01110, 0b10001, 0b10000, 0b01110, 0b00001, 0b10001, 0b01110 },
        ['T'] = new uint[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
        ['U'] = new uint[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
        ['V'] = new uint[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
        ['W'] = new uint[] { 0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001 },
        ['X'] = new uint[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001 },
        ['Y'] = new uint[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 },
        ['Z'] = new uint[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111 },
        ['0'] = new uint[] { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 },
        ['1'] = new uint[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
        ['2'] = new uint[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111 },
        ['3'] = new uint[] { 0b01110, 0b10001, 0b00001, 0b00110, 0b00001, 0b10001, 0b01110 },
        ['4'] = new uint[] { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 },
        ['5'] = new uint[] { 0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110 },
        ['6'] = new uint[] { 0b01110, 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 },
        ['7'] = new uint[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 },
        ['8'] = new uint[] { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 },
        ['9'] = new uint[] { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00001, 0b01110 },
        [':'] = new uint[] { 0b00000, 0b00100, 0b00100, 0b00000, 0b00100, 0b00100, 0b00000 },
        ['/'] = new uint[] { 0b00001, 0b00010, 0b00010, 0b00100, 0b01000, 0b01000, 0b10000 },
        ['-'] = new uint[] { 0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000 },
        ['.'] = new uint[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00110, 0b00110 },
        ['!'] = new uint[] { 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00100 },
        ['%'] = new uint[] { 0b11001, 0b11010, 0b00010, 0b00100, 0b01000, 0b01011, 0b10011 },
        ['+'] = new uint[] { 0b00000, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0b00000 },
        ['?'] = new uint[] { 0b01110, 0b10001, 0b00001, 0b00110, 0b00100, 0b00000, 0b00100 },
        ['('] = new uint[] { 0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00100, 0b00010 },
        [')'] = new uint[] { 0b01000, 0b00100, 0b00010, 0b00010, 0b00010, 0b00100, 0b01000 },
        [','] = new uint[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00110, 0b00100, 0b01000 },
        ['\''] = new uint[] { 0b00100, 0b00100, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000 },
        ['<'] = new uint[] { 0b00010, 0b00100, 0b01000, 0b10000, 0b01000, 0b00100, 0b00010 },
        ['>'] = new uint[] { 0b01000, 0b00100, 0b00010, 0b00001, 0b00010, 0b00100, 0b01000 },
        // Lowercase
        ['a'] = new uint[] { 0b00000, 0b00000, 0b01110, 0b00001, 0b01111, 0b10001, 0b01111 },
        ['b'] = new uint[] { 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b10001, 0b11110 },
        ['c'] = new uint[] { 0b00000, 0b00000, 0b01110, 0b10000, 0b10000, 0b10001, 0b01110 },
        ['d'] = new uint[] { 0b00001, 0b00001, 0b01111, 0b10001, 0b10001, 0b10001, 0b01111 },
        ['e'] = new uint[] { 0b00000, 0b00000, 0b01110, 0b10001, 0b11111, 0b10000, 0b01110 },
        ['f'] = new uint[] { 0b00110, 0b01001, 0b01000, 0b11100, 0b01000, 0b01000, 0b01000 },
        ['g'] = new uint[] { 0b00000, 0b01111, 0b10001, 0b10001, 0b01111, 0b00001, 0b01110 },
        ['h'] = new uint[] { 0b10000, 0b10000, 0b10110, 0b11001, 0b10001, 0b10001, 0b10001 },
        ['i'] = new uint[] { 0b00100, 0b00000, 0b01100, 0b00100, 0b00100, 0b00100, 0b01110 },
        ['j'] = new uint[] { 0b00010, 0b00000, 0b00110, 0b00010, 0b00010, 0b10010, 0b01100 },
        ['k'] = new uint[] { 0b10000, 0b10000, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010 },
        ['l'] = new uint[] { 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
        ['m'] = new uint[] { 0b00000, 0b00000, 0b11010, 0b10101, 0b10101, 0b10001, 0b10001 },
        ['n'] = new uint[] { 0b00000, 0b00000, 0b10110, 0b11001, 0b10001, 0b10001, 0b10001 },
        ['o'] = new uint[] { 0b00000, 0b00000, 0b01110, 0b10001, 0b10001, 0b10001, 0b01110 },
        ['p'] = new uint[] { 0b00000, 0b00000, 0b11110, 0b10001, 0b11110, 0b10000, 0b10000 },
        ['q'] = new uint[] { 0b00000, 0b00000, 0b01111, 0b10001, 0b01111, 0b00001, 0b00001 },
        ['r'] = new uint[] { 0b00000, 0b00000, 0b10110, 0b11001, 0b10000, 0b10000, 0b10000 },
        ['s'] = new uint[] { 0b00000, 0b00000, 0b01111, 0b10000, 0b01110, 0b00001, 0b11110 },
        ['t'] = new uint[] { 0b01000, 0b01000, 0b11100, 0b01000, 0b01000, 0b01001, 0b00110 },
        ['u'] = new uint[] { 0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b10011, 0b01101 },
        ['v'] = new uint[] { 0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
        ['w'] = new uint[] { 0b00000, 0b00000, 0b10001, 0b10001, 0b10101, 0b10101, 0b01010 },
        ['x'] = new uint[] { 0b00000, 0b00000, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001 },
        ['y'] = new uint[] { 0b00000, 0b00000, 0b10001, 0b10001, 0b01111, 0b00001, 0b01110 },
        ['z'] = new uint[] { 0b00000, 0b00000, 0b11111, 0b00010, 0b00100, 0b01000, 0b11111 },
    };

    public PixelFont(SpriteBatch spriteBatch, Texture2D pixel)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        CurrentGlyphs = StandardGlyphs;
        UpdateCharSize();
    }
    



    /// <summary>
    /// Draw text at the given position. Text is auto-uppercased.
    /// </summary>
    public void Draw(string text, int x, int y, Color color)
    {
        int startX = x;

        foreach (char c in text)
        {
            if (c == ' ')
            {
                x += SpaceWidth * _scale;
                continue;
            }

            if (CurrentGlyphs.TryGetValue(c, out var glyph))
            {
                DrawGlyph(glyph, x, y, color);
            }

            x += (GlyphW + CharSpacing) * _scale;
        }
    }

    /// <summary>
    /// Measure the pixel width of a string.
    /// </summary>
    public int MeasureWidth(string text)
    {
        int w = 0;
        foreach (char c in text)
        {
            if (c == ' ')
                w += SpaceWidth * _scale;
            else
                w += (GlyphW + CharSpacing) * _scale;
        }
        return w;
    }

    private void DrawGlyph(uint[] rows, int x, int y, Color color)
    {
        for (int row = 0; row < GlyphH && row < rows.Length; row++)
        {
            uint bits = rows[row];
            for (int col = 0; col < GlyphW; col++)
            {
                if ((bits & (1u << (GlyphW - 1 - col))) != 0)
                {
                    _spriteBatch.Draw(_pixel,
                        new Rectangle(
                            x + col * _scale,
                            y + row * _scale,
                            _scale,
                            _scale),
                        color);
                }
            }
        }
    }
}
