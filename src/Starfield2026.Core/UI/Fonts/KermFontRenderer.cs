using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.UI.Fonts;

/// <summary>
/// Renders text using a KermFont atlas via MonoGame's SpriteBatch.
///
/// Each visible glyph is drawn as a textured quad from the font atlas.
/// The atlas is pre-baked with a color palette, so the SpriteBatch draws
/// directly in RGBA -- no custom shader needed.
///
/// For palette changes (e.g., switching from white text to red text),
/// call <see cref="KermFont.BakeAtlas"/> with a new palette before drawing.
/// </summary>
public sealed class KermFontRenderer
{
    private readonly KermFont _font;

    public KermFontRenderer(KermFont font)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
    }

    /// <summary>
    /// Draw a complete string at the given position.
    /// </summary>
    /// <param name="spriteBatch">An active SpriteBatch (Begin must have been called).</param>
    /// <param name="text">The text to render.</param>
    /// <param name="position">Top-left pixel position for the text.</param>
    /// <param name="scale">Integer pixel scale (1 = native, 2 = double size, etc.).</param>
    /// <param name="tint">
    /// Optional tint color multiplied with the atlas pixels. Use Color.White for no tinting.
    /// </param>
    public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, int scale = 1, Color? tint = null)
    {
        if (string.IsNullOrEmpty(text) || _font.Atlas == null)
            return;

        DrawString(spriteBatch, text, position, scale, tint, maxVisibleChars: -1);
    }

    /// <summary>
    /// Draw a string with a maximum number of visible characters (for typewriter effects).
    /// </summary>
    /// <param name="spriteBatch">An active SpriteBatch (Begin must have been called).</param>
    /// <param name="text">The text to render.</param>
    /// <param name="position">Top-left pixel position for the text.</param>
    /// <param name="scale">Integer pixel scale.</param>
    /// <param name="tint">Optional tint color. Use Color.White for no tinting.</param>
    /// <param name="maxVisibleChars">
    /// Maximum number of visible glyphs to render. Pass -1 to render all.
    /// </param>
    public void DrawString(
        SpriteBatch spriteBatch,
        string text,
        Vector2 position,
        int scale,
        Color? tint,
        int maxVisibleChars)
    {
        if (string.IsNullOrEmpty(text) || _font.Atlas == null)
            return;

        Color color = tint ?? Color.White;
        Texture2D atlas = _font.Atlas;

        int atlasW = _font.AtlasWidth;
        int atlasH = _font.AtlasHeight;
        int fontHeight = _font.FontHeight;

        int index = 0;
        int cursorX = 0;
        int cursorY = 0;
        int rendered = 0;

        while (index < text.Length)
        {
            if (maxVisibleChars >= 0 && rendered >= maxVisibleChars)
                break;

            var glyph = _font.GetGlyph(text, ref index, ref cursorX, ref cursorY, out _);
            if (glyph == null)
                continue;

            if (glyph.CharWidth == 0)
            {
                rendered++;
                continue;
            }

            // Source rectangle in the atlas (pixel coords)
            int srcX = (int)(glyph.U0 * atlasW);
            int srcY = (int)(glyph.V0 * atlasH);
            int srcW = (int)((glyph.U1 - glyph.U0) * atlasW);
            int srcH = (int)((glyph.V1 - glyph.V0) * atlasH);
            var sourceRect = new Rectangle(srcX, srcY, srcW, srcH);

            // The cursor position at this point has already been advanced past the glyph.
            // We need the position BEFORE the advance. The glyph was placed at:
            //   cursorX - (glyph.CharWidth + glyph.CharSpace)
            int glyphX = cursorX - glyph.CharWidth - glyph.CharSpace;
            int glyphY = cursorY;

            // Destination rectangle on screen
            var destRect = new Rectangle(
                (int)position.X + (glyphX * scale),
                (int)position.Y + (glyphY * scale),
                srcW * scale,
                fontHeight * scale);

            spriteBatch.Draw(atlas, destRect, sourceRect, color);
            rendered++;
        }
    }

    /// <summary>
    /// Draw a string centered horizontally within a given width.
    /// </summary>
    public void DrawStringCentered(
        SpriteBatch spriteBatch,
        string text,
        Vector2 position,
        int containerWidth,
        int scale = 1,
        Color? tint = null)
    {
        var size = _font.MeasureString(text);
        int textWidth = size.X * scale;
        int offsetX = (containerWidth - textWidth) / 2;
        DrawString(spriteBatch, text, position + new Vector2(offsetX, 0), scale, tint);
    }

    /// <summary>
    /// Draw a string right-aligned to a given X coordinate.
    /// </summary>
    public void DrawStringRight(
        SpriteBatch spriteBatch,
        string text,
        Vector2 rightEdge,
        int scale = 1,
        Color? tint = null)
    {
        var size = _font.MeasureString(text);
        int textWidth = size.X * scale;
        DrawString(spriteBatch, text, rightEdge - new Vector2(textWidth, 0), scale, tint);
    }
}
