using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.UI.Fonts;

/// <summary>
/// Loads and represents a .kermfont custom bitmap font from the old PokemonGameEngine.
///
/// Binary format (little-endian):
///   byte   FontHeight
///   byte   BitsPerPixel
///   int32  NumGlyphs
///   For each glyph:
///     ushort CharCode        (Unicode code point used as dictionary key)
///     byte   CharWidth       (pixel width of this glyph)
///     byte   CharSpace       (horizontal spacing after glyph)
///     byte[] PackedBitmap    (length = ceil(FontHeight * CharWidth * BitsPerPixel / 8))
///
/// Packed bitmap stores color indices MSB-first. For 2 bpp, each byte holds 4 pixels.
/// Color index 0 is typically transparent, 1 is the main color, 2 is the shadow/outline.
/// </summary>
public sealed class KermFont : IDisposable
{
    /// <summary>Height of every glyph in pixels.</summary>
    public int FontHeight { get; }

    /// <summary>Bits per pixel in the packed glyph data (typically 2).</summary>
    public int BitsPerPixel { get; }

    /// <summary>The atlas texture. Pixel format depends on the bake mode.</summary>
    public Texture2D? Atlas { get; private set; }

    /// <summary>Width of the atlas in pixels.</summary>
    public int AtlasWidth { get; }

    /// <summary>Height of the atlas in pixels.</summary>
    public int AtlasHeight { get; }

    /// <summary>The color palette last used to bake the atlas. Null if using indexed mode.</summary>
    public Color[]? CurrentPalette { get; private set; }

    private readonly Dictionary<ushort, KermGlyph> _glyphs;
    private readonly (string OldKey, ushort NewKey)[] _overrides;

    // We keep the raw indexed atlas (one byte per pixel = color index) so we can
    // re-bake with different palettes without re-parsing.
    private readonly byte[] _indexedAtlas;

    private readonly GraphicsDevice _graphicsDevice;
    private bool _disposed;

    private const int SPACING = 1;

    /// <summary>
    /// Load a .kermfont file and build the atlas texture.
    /// </summary>
    /// <param name="graphicsDevice">MonoGame graphics device for texture creation.</param>
    /// <param name="fontPath">Absolute path to the .kermfont file.</param>
    /// <param name="atlasWidth">Atlas width in pixels (must be power of 2).</param>
    /// <param name="atlasHeight">Atlas height in pixels (must be power of 2).</param>
    /// <param name="overrides">
    /// String-to-codepoint overrides for special sequences like ("male", 0x246D).
    /// Pass null or empty for no overrides.
    /// </param>
    /// <param name="palette">
    /// Initial color palette for baking. Index 0 = transparent, 1 = main, 2 = shadow, etc.
    /// If null, a default white-on-black palette is used.
    /// </param>
    public KermFont(
        GraphicsDevice graphicsDevice,
        string fontPath,
        int atlasWidth = 1024,
        int atlasHeight = 1024,
        (string OldKey, ushort NewKey)[]? overrides = null,
        Color[]? palette = null)
    {
        _graphicsDevice = graphicsDevice;
        AtlasWidth = atlasWidth;
        AtlasHeight = atlasHeight;
        _overrides = overrides ?? Array.Empty<(string, ushort)>();

        using var stream = File.OpenRead(fontPath);
        using var r = new BinaryReader(stream);

        // --- Header ---
        FontHeight = r.ReadByte();
        if (FontHeight > atlasHeight)
            throw new InvalidDataException(
                $"FontHeight ({FontHeight}) exceeds atlas height ({atlasHeight}).");

        BitsPerPixel = r.ReadByte();
        int numGlyphs = r.ReadInt32(); // little-endian

        // --- Read all packed glyphs ---
        var packedGlyphs = new List<(ushort Key, byte CharWidth, byte CharSpace, byte[] Packed)>(numGlyphs);
        for (int i = 0; i < numGlyphs; i++)
        {
            ushort charCode = r.ReadUInt16();
            byte charWidth = r.ReadByte();
            byte charSpace = r.ReadByte();

            int numBits = FontHeight * charWidth * BitsPerPixel;
            int numBytes = (numBits / 8) + ((numBits % 8) != 0 ? 1 : 0);
            byte[] packed = numBytes > 0 ? r.ReadBytes(numBytes) : Array.Empty<byte>();

            packedGlyphs.Add((charCode, charWidth, charSpace, packed));
        }

        // --- Build indexed atlas (one byte per pixel = color index) ---
        _indexedAtlas = new byte[atlasWidth * atlasHeight];
        _glyphs = new Dictionary<ushort, KermGlyph>(numGlyphs);

        int posX = 0;
        int posY = 0;

        foreach (var (key, charWidth, charSpace, packed) in packedGlyphs)
        {
            if (charWidth > atlasWidth)
                throw new InvalidDataException(
                    $"Glyph 0x{key:X4} width ({charWidth}) exceeds atlas width ({atlasWidth}).");

            // Wrap to next row if needed
            if (posX >= atlasWidth || posX + charWidth > atlasWidth)
            {
                posX = 0;
                posY += FontHeight + SPACING;
                if (posY + FontHeight > atlasHeight)
                    throw new InvalidDataException(
                        $"Atlas too small: ran out of vertical space at glyph 0x{key:X4}.");
            }

            // Unpack the glyph bitmap into the indexed atlas
            if (charWidth > 0)
            {
                UnpackGlyph(packed, charWidth, posX, posY);
            }

            // Compute UV coordinates (normalized 0..1)
            float u0 = (float)posX / atlasWidth;
            float v0 = (float)posY / atlasHeight;
            float u1 = (float)(posX + charWidth) / atlasWidth;
            float v1 = (float)(posY + FontHeight) / atlasHeight;

            var glyph = new KermGlyph(charWidth, charSpace, u0, v0, u1, v1);
            _glyphs[key] = glyph;

            posX += charWidth + SPACING;
        }

        // --- Bake to RGBA texture ---
        palette ??= DefaultPalette();
        BakeAtlas(palette);
    }

    /// <summary>
    /// Unpack a single glyph's packed bitmap data into the indexed atlas.
    /// Bits are read MSB-first. For BitsPerPixel=2, each byte holds 4 pixels.
    /// Iteration order: row-major (Y outer, X inner), matching the old engine exactly.
    /// </summary>
    private void UnpackGlyph(byte[] packed, int charWidth, int atlasX, int atlasY)
    {
        int bpp = BitsPerPixel;
        int curBit = 0;
        int curByte = 0;

        for (int py = 0; py < FontHeight; py++)
        {
            for (int px = 0; px < charWidth; px++)
            {
                // Extract color index: read bpp bits starting at curBit within packed[curByte], MSB-first
                int colorIndex = (packed[curByte] >> (8 - bpp - curBit)) % (1 << bpp);

                int destX = atlasX + px;
                int destY = atlasY + py;
                _indexedAtlas[destX + (destY * AtlasWidth)] = (byte)colorIndex;

                curBit = (curBit + bpp) % 8;
                if (curBit == 0)
                {
                    curByte++;
                }
            }
        }
    }

    /// <summary>
    /// Bake the indexed atlas into an RGBA Texture2D using the given palette.
    /// This replaces the existing Atlas texture.
    /// Can be called multiple times to change colors at runtime.
    /// </summary>
    /// <param name="palette">
    /// Color palette where index 0 is typically transparent, 1 is main color, 2 is shadow.
    /// Indices beyond the palette length are treated as transparent.
    /// </param>
    public void BakeAtlas(Color[] palette)
    {
        CurrentPalette = palette;

        int pixelCount = AtlasWidth * AtlasHeight;
        var rgba = new Color[pixelCount];

        for (int i = 0; i < pixelCount; i++)
        {
            byte idx = _indexedAtlas[i];
            rgba[i] = idx < palette.Length ? palette[idx] : Color.Transparent;
        }

        if (Atlas == null)
        {
            Atlas = new Texture2D(_graphicsDevice, AtlasWidth, AtlasHeight, false, SurfaceFormat.Color);
        }
        Atlas.SetData(rgba);
    }

    /// <summary>
    /// Look up a glyph, handling override sequences and control characters.
    /// Returns null for control characters (newline, carriage return, form feed).
    /// </summary>
    /// <param name="text">The full text string.</param>
    /// <param name="index">
    /// Current read position in the string. Advanced past the consumed character(s).
    /// </param>
    /// <param name="cursorX">Current cursor X position (pixels). Updated by glyph width + spacing.</param>
    /// <param name="cursorY">Current cursor Y position (pixels). Updated on newlines.</param>
    /// <param name="consumed">The substring that was consumed (for diagnostics or typewriter).</param>
    /// <returns>The glyph to render, or null for whitespace/control characters.</returns>
    public KermGlyph? GetGlyph(string text, ref int index, ref int cursorX, ref int cursorY, out string? consumed)
    {
        char c = text[index];

        // Carriage return: skip entirely
        if (c == '\r')
        {
            index++;
            consumed = null;
            return null;
        }

        // Newline or vertical tab: move cursor to next line
        if (c == '\n' || c == '\v')
        {
            index++;
            cursorX = 0;
            cursorY += FontHeight + 1;
            consumed = c.ToString();
            return null;
        }

        // Form feed: reset cursor to origin
        if (c == '\f')
        {
            index++;
            cursorX = 0;
            cursorY = 0;
            consumed = "\f";
            return null;
        }

        // Check override sequences (e.g., "[PK]" -> codepoint 0x2486)
        for (int i = 0; i < _overrides.Length; i++)
        {
            var (oldKey, newKey) = _overrides[i];
            int ol = oldKey.Length;
            if (index + ol <= text.Length && text.Substring(index, ol) == oldKey)
            {
                index += ol;
                var glyph = _glyphs[newKey];
                consumed = oldKey;
                cursorX += glyph.CharWidth + glyph.CharSpace;
                return glyph;
            }
        }

        // Standard character lookup
        index++;
        if (!_glyphs.TryGetValue(c, out var result))
        {
            // Fallback to '?' if the character is not in the font
            result = _glyphs.GetValueOrDefault((ushort)'?');
        }

        if (result != null)
        {
            consumed = c.ToString();
            cursorX += result.CharWidth + result.CharSpace;
            return result;
        }

        consumed = c.ToString();
        return null;
    }

    /// <summary>
    /// Measure the pixel size needed to render a string.
    /// </summary>
    public Point MeasureString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Point.Zero;

        int index = 0;
        int cursorX = 0, cursorY = 0;
        int maxX = 0, maxY = 0;

        while (index < text.Length)
        {
            GetGlyph(text, ref index, ref cursorX, ref cursorY, out _);
            if (cursorX > maxX) maxX = cursorX;
            if (cursorY > maxY) maxY = cursorY;
        }

        maxY += FontHeight;
        return new Point(maxX, maxY);
    }

    /// <summary>
    /// Count how many visible (renderable) characters are in the string.
    /// </summary>
    public int CountVisibleChars(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int index = 0;
        int cursorX = 0, cursorY = 0;
        int count = 0;

        while (index < text.Length)
        {
            var g = GetGlyph(text, ref index, ref cursorX, ref cursorY, out _);
            if (g != null)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Returns a default palette suitable for white text with gray shadow on transparent background.
    /// </summary>
    public static Color[] DefaultPalette()
    {
        return new[]
        {
            Color.Transparent,              // 0 = background (transparent)
            new Color(239, 239, 239, 255),  // 1 = main text (white)
            new Color(132, 132, 132, 255),  // 2 = shadow/outline (gray)
            new Color(60, 60, 60, 255),     // 3 = extra (if 2bpp allows 4 indices)
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Atlas?.Dispose();
            Atlas = null;
            _disposed = true;
        }
    }
}
