#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Save;
using Starfield2026.ModelLoader.Screens;
using Starfield2026.ModelLoader.Rendering;
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
    private MountainSceneScreen _mountainScene = new();
    private CharacterDatabase _database = new();

    private List<CharacterRecord> _characters = new();
    private int _characterIndex = -1;

    private CharacterSelectOverlay? _charSelect;

    // Map viewer
    private MapViewerScreen _mapViewer = new();
    private List<MapRecord> _maps = new();
    private int _mapIndex = -1;
    private MapSelectOverlay? _mapSelect;
    private bool _inMapMode;
    private bool _inMountainMode;

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

        // Scan shared animation folders
        string sharedRoot = Path.Combine(assetsRoot, "Models", "SharedAnimations");
        SharedAnimationResolver.ScanFolders(sharedRoot, _freeRoam.SharedAnimationFolders);
        ModelLoaderLog.Info($"Shared animation folders: {_freeRoam.SharedAnimationFolders.Count}");
        foreach (var kvp in _freeRoam.SharedAnimationFolders)
            ModelLoaderLog.Info($"  {kvp.Key} -> {kvp.Value}");

// Load trainer party assignments
        string pokemonRoot = Path.Combine(assetsRoot, "Models", "Pokemon");
        string partyJsonPath = Path.Combine(assetsRoot, "trainer_parties.json");
        _freeRoam.PokemonRoot = pokemonRoot;
        _freeRoam.TrainerParties = Helpers.TrainerPartyAssignment.LoadFromJson(partyJsonPath);
        ModelLoaderLog.Info($"Trainer parties: {_freeRoam.TrainerParties.Count} entries from {partyJsonPath}");

        // Load Pokemon gen scale config
        string genScalesPath = Path.Combine(assetsRoot, "pokemon_gen_scales.json");
        Helpers.PokemonSlot.LoadGenScales(genScalesPath);
        ModelLoaderLog.Info($"Pokemon gen scales loaded from {genScalesPath}");

        // Restore animation settings
        string? savedMode = _database.GetSetting("animation_mode");
        if (savedMode != null && Enum.TryParse<AnimationLoadMode>(savedMode, out var mode))
            _freeRoam.LoadMode = mode;
        string? savedTags = _database.GetSetting("fill_tags");
        if (savedTags != null)
            _freeRoam.FillTags = new HashSet<string>(savedTags.Split(',', StringSplitOptions.RemoveEmptyEntries));

        // Scan map models
        string mapsRoot = Path.Combine(assetsRoot, "Models", "Maps");
        ModelLoaderLog.Info($"Scanning maps: {mapsRoot}");
        var mapEntries = MapManifestScanner.Scan(mapsRoot);
        ModelLoaderLog.Info($"Found {mapEntries.Count} map entries");
        int mapId = 0;
        foreach (var (name, category, subfolder, manifestPath) in mapEntries)
            _maps.Add(new MapRecord(++mapId, name, category, subfolder, manifestPath));
        ModelLoaderLog.Info($"Loaded {_maps.Count} maps");

        // Init FreeRoam
        _freeRoam.Initialize(GraphicsDevice);

        // Init MapViewer
        _mapViewer.Initialize(GraphicsDevice);

        // Init Mountain Scene
        _mountainScene.Initialize(GraphicsDevice);
        _mountainScene.SharedAnimationFolders = _freeRoam.SharedAnimationFolders;
        _mountainScene.TrainerParties = _freeRoam.TrainerParties;
        _mountainScene.PokemonRoot = _freeRoam.PokemonRoot;
        _mountainScene.LoadMode = _freeRoam.LoadMode;
        _mountainScene.FillTags = _freeRoam.FillTags;

        string mountainPath = Path.Combine(assetsRoot, "Models", "Maps", "Mountain");
        _mountainScene.LoadScene(mountainPath);

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
            _charSelect.Update(snap, (float)gameTime.ElapsedGameTime.TotalSeconds);
            if (_charSelect.IsFinished)
            {
                bool settingsChanged = _charSelect.AnimationSettingsChanged;

                // Sync animation settings back from overlay
                if (settingsChanged)
                {
                    _freeRoam.LoadMode = _charSelect.LoadMode;
                    _freeRoam.FillTags = _charSelect.FillTags;
                }

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
                else if (settingsChanged)
                {
                    // Mode/tags changed but no new character picked — reload current
                    ModelLoaderLog.Info("[UI] Animation settings changed, reloading current character");
                    LoadCurrentCharacter();
                }

                // Persist animation settings
                _database.SetSetting("animation_mode", _freeRoam.LoadMode.ToString());
                _database.SetSetting("fill_tags", string.Join(",", _freeRoam.FillTags));

                _charSelect = null;
            }
            base.Update(gameTime);
            return;
        }

        // --- Map select overlay ---
        if (_mapSelect != null)
        {
            _mapSelect.Update(snap, (float)gameTime.ElapsedGameTime.TotalSeconds);
            if (_mapSelect.IsFinished)
            {
                if (_mapSelect.SelectedFolder != null)
                {
                    ModelLoaderLog.Info($"Map selected: {_mapSelect.SelectedFolder}");
                    _mapViewer.LoadMap(_mapSelect.SelectedFolder);

                    for (int i = 0; i < _maps.Count; i++)
                    {
                        string folder = Path.GetDirectoryName(_maps[i].ManifestPath) ?? "";
                        if (string.Equals(folder, _mapSelect.SelectedFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            _mapIndex = i;
                            _database.SetSetting("last_map_id", _maps[i].Id.ToString());
                            break;
                        }
                    }
                }

                _mapSelect = null;
            }
            base.Update(gameTime);
            return;
        }

// Escape/Create button = cycle between character, map, and mountain mode
        if (snap.SwitchModePressed)
        {
            if (_inMountainMode)
            {
                _inMountainMode = false;
                _inMapMode = false;
            }
            else if (_inMapMode)
            {
                _inMapMode = false;
                _inMountainMode = true;
                LoadCurrentCharacterToMountain();
            }
            else
            {
                _inMapMode = true;
            }
            ModelLoaderLog.Info($"Switched to {(_inMountainMode ? "Mountain" : _inMapMode ? "Map" : "Character")} mode");
            base.Update(gameTime);
            return;
        }

        // Tab = open select overlay (character or map depending on mode)
        if (snap.PausePressed)
        {
            if (_inMapMode)
                _mapSelect = new MapSelectOverlay(_maps);
            else
                _charSelect = new CharacterSelectOverlay(_characters, _freeRoam.LoadMode, _freeRoam.FillTags);
            base.Update(gameTime);
            return;
        }

        if (_inMountainMode)
            _mountainScene.Update(gameTime, snap);
        else if (_inMapMode)
            _mapViewer.Update(gameTime, snap);
        else
            _freeRoam.Update(gameTime, snap);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_inMountainMode)
        {
            _mountainScene.Draw(GraphicsDevice);

            string status = $"[F1] Cycle Mode  [Tab] Select Character  |  {_mountainScene.StatusText}";
            Window.Title = $"Mountain Scene  |  {status}";
            _hud.Draw(GraphicsDevice, _mountainScene.Position, _mountainScene.Yaw, status);
        }
        else if (_inMapMode)
        {
            _mapViewer.Draw(GraphicsDevice);

            string mapName = _mapIndex >= 0 && _mapIndex < _maps.Count
                ? _maps[_mapIndex].Name : "None";
            string status = $"[F1] Characters  [Tab] Select  |  {mapName} ({_mapIndex + 1}/{_maps.Count})  |  {_mapViewer.StatusText}";
            Window.Title = $"Map Viewer  |  {status}";

            _hud.Draw(GraphicsDevice, Vector3.Zero, 0f, status);
        }
        else
        {
            _freeRoam.Draw(GraphicsDevice);

            string charName = _characterIndex >= 0 && _characterIndex < _characters.Count
                ? _characters[_characterIndex].Name : "None";
            string status = $"[F1] Maps  [Tab] Select  |  {charName} ({_characterIndex + 1}/{_characters.Count})  |  {_freeRoam.StatusText}";
            Window.Title = $"3D Model Loader  |  {status}";

            _hud.Draw(GraphicsDevice, _freeRoam.Position, _freeRoam.Yaw, status);
        }

        // Select overlays
        if (_charSelect != null)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _charSelect.Draw(_spriteBatch, _pixel, _uiFont, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _spriteBatch.End();
        }
        if (_mapSelect != null)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _mapSelect.Draw(_spriteBatch, _pixel, _uiFont, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
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

    private void LoadCurrentCharacterToMountain()
    {
        if (_characterIndex < 0 || _characterIndex >= _characters.Count)
            return;

        var record = _characters[_characterIndex];
        string folder = Path.GetDirectoryName(record.ManifestPath) ?? "";
        _mountainScene.LoadCharacter(folder);
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
