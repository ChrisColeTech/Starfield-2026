#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Save;
using Starfield2026.ModelLoader.Screens;
using Starfield2026.ModelLoader.Rendering;
using Starfield2026.ModelLoader.Skeletal;
using Starfield2026.ModelLoader.UI;

namespace Starfield2026.ModelLoader;

public class ModelLoaderGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private PixelFont _uiFont = null!;
    private MinimapHUD _hud = new();
    private InputManager _input = new();
    private FreeRoamScreen _freeRoam = new();
    private CharacterDatabase _database = new();

    private List<CharacterRecord> _characters = new();
    private int _characterIndex = -1;

    private CharacterSelectOverlay? _charSelect;

    private static string WindowConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "window.json");

    private WindowStateHelper.WindowConfig? _pendingRestore;

    public ModelLoaderGame()
    {
        ModelLoaderLog.Initialize();

        // Load saved window config (logical pixels), scale by DPI for physical back buffer
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
        Exiting += Game_Exiting;

        // Defer position restore to first Update (SDL window not ready yet)
        _pendingRestore = cfg;
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        int w = Window.ClientBounds.Width;
        int h = Window.ClientBounds.Height;
        if (w > 0 && h > 0)
        {
            _graphics.PreferredBackBufferWidth = w;
            _graphics.PreferredBackBufferHeight = h;
            _graphics.ApplyChanges();
            WindowStateHelper.Save(WindowConfigPath, Window);
        }
    }

    private void Game_Exiting(object? sender, ExitingEventArgs e)
    {
        WindowStateHelper.Save(WindowConfigPath, Window);
    }

    protected override void Initialize()
    {
        base.Initialize();

        // Init database
        string dbPath = Path.Combine(AppContext.BaseDirectory, "modelloader.db");
        _database.Initialize(dbPath);
        ModelLoaderLog.Info($"Database initialized: {dbPath}");

        // Scan for models — look in Assets folder (not bin, since Models are excluded from copy)
        string assetsRoot = FindAssetsRoot();
        string modelsRoot = Path.Combine(assetsRoot, "Models");
        ModelLoaderLog.Info($"Assets root: {assetsRoot}");
        ModelLoaderLog.Info($"Scanning models: {modelsRoot}");
        var entries = ManifestScanner.Scan(modelsRoot);
        ModelLoaderLog.Info($"Found {entries.Count} model entries");
        int dbCount = _database.GetCharacterCount();
        if (dbCount != entries.Count)
        {
            ModelLoaderLog.Info($"DB count ({dbCount}) != scan count ({entries.Count}), rebuilding");
            _database.RebuildCharacters(entries);
        }

        _characters = _database.GetAllCharacters();
        ModelLoaderLog.Info($"Loaded {_characters.Count} characters from database");
        foreach (var c in _characters)
            ModelLoaderLog.Info($"  [{c.Category}] {c.Name}: {c.ManifestPath}");

        // Init FreeRoam
        _freeRoam.Initialize(GraphicsDevice);

        // Restore last selected character, or fall back to first
        if (_characters.Count > 0)
        {
            _characterIndex = 0;
            string? lastCharId = _database.GetSetting("last_character_id");
            if (lastCharId != null && int.TryParse(lastCharId, out int savedId))
            {
                for (int i = 0; i < _characters.Count; i++)
                {
                    if (_characters[i].Id == savedId)
                    {
                        _characterIndex = i;
                        break;
                    }
                }
            }
            LoadCurrentCharacter();
        }
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _uiFont = new PixelFont(_spriteBatch, _pixel);

        // Try to load a SpriteFont for the minimap HUD
        SpriteFont? spriteFont = null;
        try { spriteFont = Content.Load<SpriteFont>("DefaultFont"); } catch { }
        _hud.Initialize(_spriteBatch, _pixel, spriteFont);
    }

    protected override void Update(GameTime gameTime)
    {
        // Apply deferred window position restore on first frame
        if (_pendingRestore != null)
        {
            WindowStateHelper.Restore(Window, _graphics, _pendingRestore);
            _pendingRestore = null;
            // Subscribe AFTER restore so deferred SDL events don't overwrite
            Window.ClientSizeChanged += OnClientSizeChanged;
        }

        _input.Update();
        var snap = _input.Current;

        // --- Character select overlay ---
        if (_charSelect != null)
        {
            _charSelect.Update(snap);
            if (_charSelect.IsFinished)
            {
                if (_charSelect.SelectedFolder != null)
                {
                    ModelLoaderLog.Info($"Character selected: {_charSelect.SelectedFolder}");
                    _freeRoam.LoadCharacter(_charSelect.SelectedFolder);

                    // Update character index to match selection
                    for (int i = 0; i < _characters.Count; i++)
                    {
                        string folder = Path.GetDirectoryName(_characters[i].ManifestPath) ?? "";
                        if (string.Equals(folder, _charSelect.SelectedFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            _characterIndex = i;
                            _database.SetSetting("last_character_id", _characters[i].Id.ToString());
                            break;
                        }
                    }
                }
                _charSelect = null;
            }
            base.Update(gameTime);
            return;
        }

        // Escape = quit (when no overlay is active)
        if (snap.CancelPressed)
        {
            Exit();
            return;
        }

        // Tab = open character select
        if (snap.PausePressed)
        {
            _charSelect = new CharacterSelectOverlay(_characters);
            base.Update(gameTime);
            return;
        }

        _freeRoam.Update(gameTime, snap);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _freeRoam.Draw(GraphicsDevice);

        // HUD overlay (minimap + status)
        string charName = _characterIndex >= 0 && _characterIndex < _characters.Count
            ? _characters[_characterIndex].Name : "None";
        string status = $"[Tab] Select  |  {charName} ({_characterIndex + 1}/{_characters.Count})  |  {_freeRoam.StatusText}";
        Window.Title = $"3D Model Loader  |  {status}";

        _hud.Draw(GraphicsDevice, _freeRoam.Position, _freeRoam.Yaw, status);

        // Character select overlay
        if (_charSelect != null)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _charSelect.Draw(_spriteBatch, _pixel, _uiFont, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }

    private void LoadCurrentCharacter()
    {
        if (_characterIndex < 0 || _characterIndex >= _characters.Count)
            return;

        var record = _characters[_characterIndex];
        string folder = Path.GetDirectoryName(record.ManifestPath) ?? "";
        _freeRoam.LoadCharacter(folder);
    }

    private static string FindAssetsRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            string maybe = Path.Combine(current, "Starfield2026.Assets");
            if (Directory.Exists(maybe)) return maybe;
            current = Path.GetDirectoryName(current);
        }
        return Path.Combine(AppContext.BaseDirectory, "Assets");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _database.Dispose();
            _pixel?.Dispose();
            ModelLoaderLog.Shutdown();
        }
        base.Dispose(disposing);
    }
}
