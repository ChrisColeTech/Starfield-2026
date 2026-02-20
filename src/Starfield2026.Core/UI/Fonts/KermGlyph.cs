namespace Starfield2026.Core.UI.Fonts;

/// <summary>
/// Per-glyph metadata for a character in a KermFont atlas.
/// Stores the pixel width, spacing, and UV coordinates into the atlas texture.
/// </summary>
public sealed class KermGlyph
{
    /// <summary>Width of this glyph in pixels.</summary>
    public int CharWidth { get; }

    /// <summary>Horizontal spacing (in pixels) added after this glyph before the next one.</summary>
    public int CharSpace { get; }

    /// <summary>Left edge UV coordinate in the atlas (0..1).</summary>
    public float U0 { get; }

    /// <summary>Top edge UV coordinate in the atlas (0..1).</summary>
    public float V0 { get; }

    /// <summary>Right edge UV coordinate in the atlas (0..1).</summary>
    public float U1 { get; }

    /// <summary>Bottom edge UV coordinate in the atlas (0..1).</summary>
    public float V1 { get; }

    public KermGlyph(int charWidth, int charSpace, float u0, float v0, float u1, float v1)
    {
        CharWidth = charWidth;
        CharSpace = charSpace;
        U0 = u0;
        V0 = v0;
        U1 = u1;
        V1 = v1;
    }
}
