using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.UI.Fonts;

namespace Starfield2026.Core.UI;

/// <summary>
/// A full-screen overlay that sits on top of the normal game state.
/// Pushed onto a stack in Game1; the topmost overlay receives all input.
/// </summary>
public interface IScreenOverlay
{
    /// <summary>True when the overlay has completed its exit fade and should be popped.</summary>
    bool IsFinished { get; }

    /// <summary>Process input and advance animation/transition timers.</summary>
    void Update(float deltaTime, InputSnapshot input);

    /// <summary>Draw the full-screen overlay.</summary>
    void Draw(SpriteBatch spriteBatch, Texture2D pixel,
              KermFontRenderer? fontRenderer, KermFont? font,
              SpriteFont fallbackFont, int screenWidth, int screenHeight, int fontScale = 3);
}
