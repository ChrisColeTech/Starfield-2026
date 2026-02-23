using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Systems;

namespace Starfield2026.Core.Screens;

public interface IGameScreen
{
    void Initialize(GraphicsDevice device);
    void Update(GameTime gameTime, InputSnapshot input);
    void Draw(GraphicsDevice device);
    void OnEnter();
    void OnExit();
    CoinCollectibleSystem? CoinSystem => null;
    Color PlayerTint { get; set; }
}
