#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Runtime;

public sealed class GameDrawModule
{
    public void Draw(GameRuntimeState s, GraphicsDevice device, GameWindow window)
    {
        if (s.ScreenMode == ScreenMode.Map)
        {
            s.MapScreen.Draw(device);
            string status = $"[F1] Anime Models  [PgUp/PgDn] Switch Map  [Tab] Select Character  |  {s.MapScreen.StatusText}";
            window.Title = $"Map Screen  |  {status}";
            s.Hud.Draw(device, s.MapScreen.Position, s.MapScreen.Yaw, status);
        }
        else if (s.ScreenMode == ScreenMode.AnimeModels)
        {
            s.AnimeScreen.Draw(device);
            string status = $"[F1] Anime World  [PgUp/PgDn] Switch Map  [Tab] Select  |  {s.AnimeScreen.StatusText}";
            window.Title = $"Anime Forest  |  {status}";
            s.Hud.Draw(device, s.AnimeScreen.Position, s.AnimeScreen.Yaw, status);
        }
        else if (s.ScreenMode == ScreenMode.AnimeWorld)
        {
            s.AnimeWorldScreen.Draw(device);
            string status = $"[F1] FreeRoam  [Tab] Select  |  {s.AnimeWorldScreen.StatusText}";
            window.Title = $"Anime World  |  {status}";
            s.Hud.Draw(device, s.AnimeWorldScreen.Position, s.AnimeWorldScreen.Yaw, status);
        }
        else
        {
            s.FreeRoam.Draw(device);
            string name = s.CharacterIndex >= 0 && s.CharacterIndex < s.Characters.Count ? s.Characters[s.CharacterIndex].Name : "None";
            string status = $"[F1] Map  [Tab] Select  |  {name} ({s.CharacterIndex + 1}/{s.Characters.Count})  |  {s.FreeRoam.StatusText}";
            window.Title = $"3D Model Loader  |  {status}";
            s.Hud.Draw(device, s.FreeRoam.Position, s.FreeRoam.Yaw, status);
        }

        if (s.CharSelect != null)
        {
            s.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            s.CharSelect.Draw(s.SpriteBatch, s.Pixel, s.UiFont, device.Viewport.Width, device.Viewport.Height);
            s.SpriteBatch.End();
        }
    }
}
