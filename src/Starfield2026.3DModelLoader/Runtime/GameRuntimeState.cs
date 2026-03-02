#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;
using Starfield2026.ModelLoader.Save;
using Starfield2026.ModelLoader.Screens;
using Starfield2026.ModelLoader.UI;

namespace Starfield2026.ModelLoader.Runtime;

public sealed class GameRuntimeState
{
    public SpriteBatch SpriteBatch = null!;
    public Texture2D Pixel = null!;
    public PixelFont UiFont = null!;
    public readonly MinimapHUD Hud = new();
    public readonly InputManager Input = new();
    public readonly FreeRoamScreen FreeRoam = new();
    public readonly MapEditorMapScreen MapScreen = new();
    public readonly AnimeModelsScreen AnimeScreen = new();
    public readonly AnimeWorldScreen AnimeWorldScreen = new();
    public readonly CharacterDatabase Database = new();
    public TileModelCache? SharedTileCache;

    public List<CharacterRecord> Characters = new();
    public int CharacterIndex = -1;
    public CharacterSelectOverlay? CharSelect;
    public ScreenMode ScreenMode;
    public bool Initialized;
    public string? PendingAssetsRoot;
    public string? MapsFolder;
}
