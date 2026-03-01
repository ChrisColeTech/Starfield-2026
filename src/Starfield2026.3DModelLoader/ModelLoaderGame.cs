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
    private MapScene3DScreen _mapScene = new();
    private CharacterDatabase _database = new();

    private List<CharacterRecord> _characters = new();
    private int _characterIndex = -1;

    private CharacterSelectOverlay? _charSelect;

    private bool _inMap3DMode;

    private const string LastModeSettingKey = "last_mode";

    private static string WindowConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "window.json");

    private WindowStateHelper.WindowConfig? _pendingRestore;

    public ModelLoaderGame()
    {
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
        Exiting += Game_Exiting;

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
        PersistCurrentMode();
        WindowStateHelper.Save(WindowConfigPath, Window);
    }

    protected override void Initialize()
    {
        base.Initialize();

        // Init database
        string dbPath = Path.Combine(AppContext.BaseDirectory, "modelloader.db");
        _database.Initialize(dbPath);
        ModelLoaderLog.Info($"Database initialized: {dbPath}");

        // Scan for models
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

        // Init FreeRoam
        _freeRoam.Initialize(GraphicsDevice);

        // Init Map3D Scene
        _mapScene.Initialize(GraphicsDevice);
        _mapScene.SharedAnimationFolders = _freeRoam.SharedAnimationFolders;
        _mapScene.TrainerParties = _freeRoam.TrainerParties;
        _mapScene.PokemonRoot = _freeRoam.PokemonRoot;
        _mapScene.LoadMode = _freeRoam.LoadMode;
        _mapScene.FillTags = _freeRoam.FillTags;

        // Load map from TileRegistry.cs + first .g.cs found in Generated/
        TryLoadMap3D(assetsRoot);

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

        RestoreLastMode();
    }

    private void TryLoadMap3D(string assetsRoot)
    {
        // Find the TileRegistry.cs (source of truth)
        string coreRoot = Path.GetFullPath(Path.Combine(assetsRoot, "..", "Starfield2026.Core"));
        string registryPath = Path.Combine(coreRoot, "Maps", "TileRegistry.cs");

        // Find first .g.cs map — prefer MountainTrail which has ModelId tiles
        string generatedDir = Path.Combine(coreRoot, "Maps", "Generated");
        string? mapFile = null;
        if (Directory.Exists(generatedDir))
        {
            // Prefer MountainTrail if it exists
            string preferred = Path.Combine(generatedDir, "MountainTrail.g.cs");
            if (File.Exists(preferred))
            {
                mapFile = preferred;
            }
            else
            {
                foreach (var f in Directory.GetFiles(generatedDir, "*.g.cs"))
                {
                    mapFile = f;
                    break;
                }
            }
        }

        string fbxModelsFolder = Path.Combine(assetsRoot, "Models", "Maps", "Mountain", "Models");

        if (File.Exists(registryPath) && mapFile != null)
        {
            try
            {
                ModelLoaderLog.Info($"[Map3D] Loading registry: {registryPath}");
                ModelLoaderLog.Info($"[Map3D] Loading map: {mapFile}");
                _mapScene.LoadMap(registryPath, mapFile, fbxModelsFolder);
            }
            catch (Exception ex)
            {
                ModelLoaderLog.Info($"[Map3D] Failed to load map: {ex.Message}");
            }
        }
        else
        {
            ModelLoaderLog.Info($"[Map3D] Registry or map not found (registry={File.Exists(registryPath)}, generated dir={Directory.Exists(generatedDir)})");
        }
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _uiFont = new PixelFont(_spriteBatch, _pixel);

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

                if (settingsChanged)
                {
                    _freeRoam.LoadMode = _charSelect.LoadMode;
                    _freeRoam.FillTags = _charSelect.FillTags;
                }

                if (_charSelect.SelectedFolder != null)
                {
                    ModelLoaderLog.Info($"Character selected: {_charSelect.SelectedFolder}");

                    if (_inMap3DMode)
                        _mapScene.LoadCharacter(_charSelect.SelectedFolder);
                    else
                        _freeRoam.LoadCharacter(_charSelect.SelectedFolder);

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
                    ModelLoaderLog.Info("[UI] Animation settings changed, reloading current character");
                    LoadCurrentCharacter();
                }

                _database.SetSetting("animation_mode", _freeRoam.LoadMode.ToString());
                _database.SetSetting("fill_tags", string.Join(",", _freeRoam.FillTags));

                _charSelect = null;
            }
            base.Update(gameTime);
            return;
        }

        // F1 = cycle between FreeRoam and Map3D
        if (snap.SwitchModePressed)
        {
            _inMap3DMode = !_inMap3DMode;

            // Load character into whichever mode we're switching to
            if (_inMap3DMode)
                LoadCurrentCharacterToMap3D();

            PersistCurrentMode();
            ModelLoaderLog.Info($"Switched to {(_inMap3DMode ? "Map3D" : "FreeRoam")} mode");
            base.Update(gameTime);
            return;
        }

        // Tab = open character select overlay
        if (snap.PausePressed)
        {
            _charSelect = new CharacterSelectOverlay(_characters, _freeRoam.LoadMode, _freeRoam.FillTags);
            base.Update(gameTime);
            return;
        }

        if (_inMap3DMode)
            _mapScene.Update(gameTime, snap);
        else
            _freeRoam.Update(gameTime, snap);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_inMap3DMode)
        {
            _mapScene.Draw(GraphicsDevice);

            string status = $"[F1] FreeRoam  [Tab] Select Character  |  {_mapScene.StatusText}";
            Window.Title = $"Map 3D  |  {status}";
            _hud.Draw(GraphicsDevice, _mapScene.Position, _mapScene.Yaw, status);
        }
        else
        {
            _freeRoam.Draw(GraphicsDevice);

            string charName = _characterIndex >= 0 && _characterIndex < _characters.Count
                ? _characters[_characterIndex].Name : "None";
            string status = $"[F1] Map3D  [Tab] Select  |  {charName} ({_characterIndex + 1}/{_characters.Count})  |  {_freeRoam.StatusText}";
            Window.Title = $"3D Model Loader  |  {status}";

            _hud.Draw(GraphicsDevice, _freeRoam.Position, _freeRoam.Yaw, status);
        }

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

    private void LoadCurrentCharacterToMap3D()
    {
        if (_characterIndex < 0 || _characterIndex >= _characters.Count)
            return;

        var record = _characters[_characterIndex];
        string folder = Path.GetDirectoryName(record.ManifestPath) ?? "";
        _mapScene.LoadCharacter(folder);
    }

    private void RestoreLastMode()
    {
        string? mode = _database.GetSetting(LastModeSettingKey);
        if (string.IsNullOrWhiteSpace(mode))
            return;

        switch (mode.ToLowerInvariant())
        {
            case "map3d":
                _inMap3DMode = true;
                LoadCurrentCharacterToMap3D();
                break;
            default:
                _inMap3DMode = false;
                break;
        }
    }

    private void PersistCurrentMode()
    {
        string mode = _inMap3DMode ? "map3d" : "freeroam";
        _database.SetSetting(LastModeSettingKey, mode);
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
