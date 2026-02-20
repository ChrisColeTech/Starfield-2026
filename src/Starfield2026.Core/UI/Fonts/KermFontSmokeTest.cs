using System;
using System.Collections.Generic;
using System.IO;

namespace Starfield2026.Core.UI.Fonts;

/// <summary>
/// Headless smoke test that parses .kermfont files without needing a GraphicsDevice.
/// Validates that the binary format parsing is correct by reading the header and all
/// glyph data, then printing summary information.
///
/// Usage: call KermFontSmokeTest.Run() from anywhere, or run as a standalone console test.
/// </summary>
public static class KermFontSmokeTest
{
    /// <summary>
    /// Parse a .kermfont file without creating any GPU textures, returning diagnostic info.
    /// This exercises the exact same binary parsing logic as KermFont but skips Texture2D creation.
    /// </summary>
    public static string ParseAndDescribe(string fontPath)
    {
        using var stream = File.OpenRead(fontPath);
        using var r = new BinaryReader(stream);

        byte fontHeight = r.ReadByte();
        byte bitsPerPixel = r.ReadByte();
        int numGlyphs = r.ReadInt32();

        var lines = new List<string>
        {
            $"File: {Path.GetFileName(fontPath)}",
            $"  Size: {new FileInfo(fontPath).Length} bytes",
            $"  FontHeight: {fontHeight}",
            $"  BitsPerPixel: {bitsPerPixel}",
            $"  NumGlyphs: {numGlyphs}",
        };

        int totalBitmapBytes = 0;
        ushort minCode = ushort.MaxValue;
        ushort maxCode = ushort.MinValue;
        int maxWidth = 0;
        int zeroWidthCount = 0;
        var sampleGlyphs = new List<string>();

        for (int i = 0; i < numGlyphs; i++)
        {
            ushort charCode = r.ReadUInt16();
            byte charWidth = r.ReadByte();
            byte charSpace = r.ReadByte();

            int numBits = fontHeight * charWidth * bitsPerPixel;
            int numBytes = (numBits / 8) + ((numBits % 8) != 0 ? 1 : 0);
            if (numBytes > 0)
                r.ReadBytes(numBytes); // skip the bitmap data

            totalBitmapBytes += numBytes;

            if (charCode < minCode) minCode = charCode;
            if (charCode > maxCode) maxCode = charCode;
            if (charWidth > maxWidth) maxWidth = charWidth;
            if (charWidth == 0) zeroWidthCount++;

            // Collect a few sample glyphs for display
            if (i < 5 || charCode == '?' || charCode == 'A' || charCode == 'a' || charCode == '0')
            {
                char display = charCode >= 0x20 && charCode < 0x7F ? (char)charCode : '?';
                sampleGlyphs.Add($"    0x{charCode:X4} '{display}' width={charWidth} space={charSpace} bitmapBytes={numBytes}");
            }
        }

        lines.Add($"  CharCode range: 0x{minCode:X4} - 0x{maxCode:X4}");
        lines.Add($"  Max glyph width: {maxWidth}");
        lines.Add($"  Zero-width glyphs: {zeroWidthCount}");
        lines.Add($"  Total bitmap data: {totalBitmapBytes} bytes");
        lines.Add($"  Bytes remaining in file: {stream.Length - stream.Position}");
        lines.Add("  Sample glyphs:");
        foreach (var s in sampleGlyphs)
            lines.Add(s);

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Run the smoke test on Default.kermfont and Battle.kermfont.
    /// Returns the combined diagnostic output as a string.
    /// </summary>
    public static string Run()
    {
        string fontsDir = @"D:\Projects\PokemonGameEngine\PokemonGameEngine\Assets\Fonts";
        var results = new List<string>();

        foreach (string name in new[] { "Default.kermfont", "Battle.kermfont" })
        {
            string path = Path.Combine(fontsDir, name);
            if (File.Exists(path))
            {
                results.Add(ParseAndDescribe(path));
                results.Add("");
            }
            else
            {
                results.Add($"NOT FOUND: {path}");
                results.Add("");
            }
        }

        return string.Join(Environment.NewLine, results);
    }
}
