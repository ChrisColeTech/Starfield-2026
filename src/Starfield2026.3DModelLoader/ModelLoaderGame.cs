#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Runtime;
using Starfield2026.ModelLoader.UI;

namespace Starfield2026.ModelLoader;

public enum ScreenMode { FreeRoam, Map, AnimeModels, AnimeWorld }

public class ModelLoaderGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameRuntimeState _state = new();
    private readonly GameRuntimeCoordinator _coordinator = new();
    private readonly GameUpdateModule _updateModule = new();
    private readonly GameDrawModule _drawModule = new();
    private WindowStateHelper.WindowConfig? _pendingRestore;
    private static string WindowConfigPath => Path.Combine(AppContext.BaseDirectory, "window.json");

    public ModelLoaderGame()
    {
        long startMem = GameRuntimeUtil.GetMemoryMB();
        long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        ModelLoaderLog.Initialize();
        var cfg = WindowStateHelper.Load(WindowConfigPath);
        float dpiScale = WindowStateHelper.GetDpiScale();
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = (int)(cfg.Width * dpiScale),
            PreferredBackBufferHeight = (int)(cfg.Height * dpiScale),
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferMultiSampling = true,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Starfield 3D Model Loader";
        Window.AllowUserResizing = true;
        Exiting += (_, _) => WindowStateHelper.Save(WindowConfigPath, Window);
        _pendingRestore = cfg;
        GameRuntimeUtil.LogTiming("ModelLoaderGame.ctor", startMem, startTicks);
    }

    protected override void Initialize()
    {
        base.Initialize();
        _state.FreeRoam.Initialize(GraphicsDevice);
        _state.MapScreen.Initialize(GraphicsDevice);
        _state.AnimeScreen.Initialize(GraphicsDevice);
        _state.AnimeWorldScreen.Initialize(GraphicsDevice);
        _state.Database.Initialize(Path.Combine(AppContext.BaseDirectory, "modelloader.db"));
        _state.PendingAssetsRoot = GameRuntimeUtil.FindAssetsRoot();
    }

    protected override void LoadContent()
    {
        _state.SpriteBatch = new SpriteBatch(GraphicsDevice);
        _state.Pixel = new Texture2D(GraphicsDevice, 1, 1);
        _state.Pixel.SetData(new[] { Color.White });
        _state.UiFont = new Rendering.PixelFont(_state.SpriteBatch, _state.Pixel);
        SpriteFont? sf = null;
        try { sf = Content.Load<SpriteFont>("DefaultFont"); } catch { }
        _state.Hud.Initialize(_state.SpriteBatch, _state.Pixel, sf);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_pendingRestore != null)
        {
            WindowStateHelper.Restore(Window, _graphics, _pendingRestore);
            _pendingRestore = null;
            Window.ClientSizeChanged += (_, _) => WindowStateHelper.Save(WindowConfigPath, Window);
        }

        if (!_state.Initialized)
        {
            _coordinator.DeferredInitialize(_state, GraphicsDevice);
            if (!_state.Initialized)
                return;
        }

        _updateModule.Update(_state, _coordinator, GraphicsDevice, gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _drawModule.Draw(_state, GraphicsDevice, Window);
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _state.MapScreen.Dispose();
            _state.AnimeScreen.Dispose();
            _state.AnimeWorldScreen.Dispose();
            _state.SharedTileCache?.Dispose();
            _state.Database.Dispose();
            _state.Pixel?.Dispose();
            ModelLoaderLog.Shutdown();
        }
        base.Dispose(disposing);
    }
}
