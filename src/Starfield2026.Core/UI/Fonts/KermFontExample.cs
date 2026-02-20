using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.UI.Fonts;

/// <summary>
/// Example showing how to load and render .kermfont files in MonoGame.
/// This is NOT meant to be used directly -- copy the patterns into your game code.
///
/// Usage overview:
///
///   // In LoadContent():
///   _defaultFont = new KermFont(
///       GraphicsDevice,
///       @"D:\Projects\PokemonGameEngine\PokemonGameEngine\Assets\Fonts\Default.kermfont",
///       atlasWidth: 1024,
///       atlasHeight: 1024,
///       overrides: new (string, ushort)[]
///       {
///           ("\u2642", 0x246D),  // male symbol
///           ("\u2640", 0x246E),  // female symbol
///           ("[PK]",   0x2486),
///           ("[MN]",   0x2487),
///       },
///       palette: KermFontPalettes.WhiteInner
///   );
///   _fontRenderer = new KermFontRenderer(_defaultFont);
///
///   // In Draw():
///   _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
///   _fontRenderer.DrawString(_spriteBatch, "Hello, world!", new Vector2(10, 10));
///   _spriteBatch.End();
///
///   // Typewriter effect (show first N characters):
///   _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
///   _fontRenderer.DrawString(_spriteBatch, longText, new Vector2(10, 50),
///       scale: 2, tint: null, maxVisibleChars: _revealedCount);
///   _spriteBatch.End();
///
///   // Changing palette at runtime (e.g., for a red-highlighted menu item):
///   _defaultFont.BakeAtlas(KermFontPalettes.RedOuter);
///   _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
///   _fontRenderer.DrawString(_spriteBatch, "CRITICAL HIT!", new Vector2(10, 90), scale: 2);
///   _spriteBatch.End();
///   _defaultFont.BakeAtlas(KermFontPalettes.WhiteInner);  // restore
///
///   // In UnloadContent():
///   _defaultFont.Dispose();
/// </summary>
public static class KermFontExample
{
    /// <summary>
    /// Self-contained example that loads both Default and Battle fonts and
    /// draws sample text. Call this from your Game.Draw() for a quick test.
    ///
    /// This method is intentionally all-in-one for demonstration.
    /// In real code, you would separate load/draw/dispose across the Game lifecycle.
    /// </summary>
    public static void RunDemo(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        // --- Load fonts (normally done once in LoadContent) ---

        var overrides = new (string, ushort)[]
        {
            ("\u2642", 0x246D),  // male sign
            ("\u2640", 0x246E),  // female sign
            ("[PK]",   0x2486),
            ("[MN]",   0x2487),
        };

        string fontsDir = @"D:\Projects\PokemonGameEngine\PokemonGameEngine\Assets\Fonts";

        using var defaultFont = new KermFont(
            graphicsDevice,
            System.IO.Path.Combine(fontsDir, "Default.kermfont"),
            atlasWidth: 1024,
            atlasHeight: 1024,
            overrides: overrides,
            palette: KermFontPalettes.WhiteInner);

        using var battleFont = new KermFont(
            graphicsDevice,
            System.IO.Path.Combine(fontsDir, "Battle.kermfont"),
            atlasWidth: 1024,
            atlasHeight: 1024,
            overrides: overrides,
            palette: KermFontPalettes.WhiteInner);

        var defaultRenderer = new KermFontRenderer(defaultFont);
        var battleRenderer = new KermFontRenderer(battleFont);

        // --- Measure text ---
        Point size1 = defaultFont.MeasureString("Hello World!");
        // size1 gives you the pixel dimensions at scale=1

        // --- Draw ---
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp); // PointClamp for crisp pixels

        // Default font, white, at native scale
        defaultRenderer.DrawString(spriteBatch, "Default Font - Hello World!", new Vector2(10, 10));

        // Default font at 2x scale
        defaultRenderer.DrawString(spriteBatch, "2x Scale Text", new Vector2(10, 40), scale: 2);

        // Battle font at native scale
        battleRenderer.DrawString(spriteBatch, "Battle Font - PIKACHU used THUNDERBOLT!", new Vector2(10, 80));

        // Typewriter: only show 12 characters
        defaultRenderer.DrawString(spriteBatch, "This reveals slowly...", new Vector2(10, 110),
            scale: 1, tint: null, maxVisibleChars: 12);

        spriteBatch.End();

        // --- Palette swap example ---
        // Switch to red palette, draw, then switch back
        defaultFont.BakeAtlas(KermFontPalettes.RedOuter);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        defaultRenderer.DrawString(spriteBatch, "Red text!", new Vector2(10, 140), scale: 2);
        spriteBatch.End();

        // Restore white
        defaultFont.BakeAtlas(KermFontPalettes.WhiteInner);

        // --- Yellow text ---
        defaultFont.BakeAtlas(KermFontPalettes.YellowOuter);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        defaultRenderer.DrawString(spriteBatch, "Yellow highlighted!", new Vector2(10, 180), scale: 2);
        spriteBatch.End();

        defaultFont.BakeAtlas(KermFontPalettes.WhiteInner);
    }
}
