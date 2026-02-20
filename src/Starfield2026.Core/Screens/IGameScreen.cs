using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;

namespace Starfield2026.Core.Screens;

public interface IGameScreen
{
    void Initialize(GraphicsDevice device);
    void Update(GameTime gameTime, InputSnapshot input);
    void Draw(GraphicsDevice device);
    void OnEnter();
    void OnExit();
}
