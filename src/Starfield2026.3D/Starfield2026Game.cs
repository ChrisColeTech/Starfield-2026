#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Managers;
using Starfield2026.Core.Maps;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.Save;
using Starfield2026.ModelLoader.Skeletal;
using Starfield2026.Core.Screens;
using Starfield2026.Core.Screens.Battle;
using Starfield2026.Core.Systems;
using Starfield2026.Core.UI;
using Starfield2026.Core.UI.Screens;

namespace Starfield2026._3D;

public class Starfield2026Game : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    private InputManager _input = null!;
    private GameDatabase _database = null!;
    private GameState _state = null!;
    private AmmoSystem _ammo = null!;
    private BoostSystem _boosts = null!;
    private ScreenManager _screens = null!;
    private CoinCollector _coinCollector = null!;
    private HUDRenderer _hud = null!;

    private OverworldScreen _overworld = null!;
    private DrivingScreen _driving = null!;
    private SpaceFlightScreen _spaceflight = null!;

    private enum GameMode { Exploration, Battle }
    private GameMode _mode = GameMode.Exploration;
    private const bool DebugStartInBattle = false;

    private BattleScreen3D _battleScreen = new();
    private FreeRoamScreen _freeRoam = null!;
    private IScreenOverlay? _pauseOverlay;
    private Texture2D _pixel = null!;
    private PixelFont _uiFont = null!;

    private static readonly string GameLogFile = Path.Combine(AppContext.BaseDirectory, "game_loop.log");
    private bool _gameLoopLogged;
    private static void GameLog(string msg) =>
        File.AppendAllText(GameLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

    private string FindAssetsRoot()
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

    public Starfield2026Game()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
        _graphics.PreferMultiSampling = true;
        _graphics.SynchronizeWithVerticalRetrace = true;

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Starfield 2026";
    }

    protected override void Initialize()
    {
        _input = new InputManager();

        InitializeDatabase();

        // Sync character manifests with DB (only rescans if count changed)
        string modelsRoot = Path.Combine(AppContext.BaseDirectory, "Models");
        var scannedEntries = ManifestScanner.Scan(modelsRoot);
        int dbCharCount = _database.GetCharacterCount();
        if (dbCharCount != scannedEntries.Count)
            _database.RebuildCharacters(scannedEntries);

        InitializeScreens();

        base.Initialize();
    }

    private void InitializeDatabase()
    {
        // Force evaluation of map singletons so they register in MapCatalog
        Starfield2026.Core.Maps.MapCatalog.LoadAllMaps();

        _database = new GameDatabase();
        string dbPath = Path.Combine(AppContext.BaseDirectory, "starfield2026.db");
        _database.Initialize(dbPath);

        var profile = _database.LoadProfile();

        _state = new GameState();
        _state.Initialize(_database, profile);

        _ammo = new AmmoSystem();
        _ammo.Initialize(_state.GoldAmmo, _state.RedAmmo);
        _ammo.Changed += a => { _state.SetAmmo(a.GoldAmmo, a.RedAmmo); };

        _boosts = new BoostSystem();
        _boosts.SetBoosts(_state.BoostCount);
        _boosts.Changed += b => { _state.SetBoostCount(b.BoostCount); };
    }

    private void InitializeScreens()
    {
        _overworld = new OverworldScreen { Ammo = _ammo, Boosts = _boosts };
        _driving = new DrivingScreen { Ammo = _ammo, Boosts = _boosts };
        _spaceflight = new SpaceFlightScreen { Ammo = _ammo, Boosts = _boosts };

        _freeRoam = new FreeRoamScreen();

        _overworld.Initialize(GraphicsDevice);
        _driving.Initialize(GraphicsDevice);
        _spaceflight.Initialize(GraphicsDevice);
        _freeRoam.Initialize(GraphicsDevice);

        // Load saved character into test screen
        if (_state.CharacterId.HasValue)
        {
            var savedChar = _database.GetCharacter(_state.CharacterId.Value);
            if (savedChar != null)
                _freeRoam.LoadCharacter(Path.GetDirectoryName(savedChar.ManifestPath)!);
        }

        // Restore saved position if resuming on model test screen
        if (_state.CurrentScreen == "freeroam")
            _freeRoam.SetPosition(_state.PlayerPosition);

        _overworld.OnLaunchRequested += () => _screens.TransitionTo(_driving, "driving");
        _overworld.OnMapTransition += warp => HandleMapTransition(warp);
        _driving.OnExitDrivingRequested += () => _screens.TransitionTo(_spaceflight, "space");
        _spaceflight.OnLandRequested += () => _screens.TransitionTo(_overworld, "overworld");

        _screens = new ScreenManager(screenName =>
        {
            _state.SetScreen(screenName);
            _state.Save();
        });

        var initial = _state.CurrentScreen switch
        {
            "overworld" => (IGameScreen)_overworld,
            "driving" => _driving,
            "freeroam" => _freeRoam,
            _ => _spaceflight
        };
        _screens.SetInitialScreen(initial);

        _coinCollector = new CoinCollector(_state, _ammo, _boosts);

        _overworld.LoadMap(_state.CurrentMapId ?? "overworld_grid", _state.PlayerPosition);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _hud = new HUDRenderer();
        _hud.Initialize(_spriteBatch, _pixel);

        _uiFont = new PixelFont(_spriteBatch, _pixel);

        _battleScreen.Initialize(_spriteBatch, _pixel, null, null, _uiFont);
        string assets = FindAssetsRoot();
        _battleScreen.LoadBattleModels(GraphicsDevice, Path.Combine(assets, "BattleBG"));
        _battleScreen.SetPartyAndInventory(
            Starfield2026.Core.Pokemon.Party.CreateTestParty(),
            Starfield2026.Core.Items.PlayerInventory.CreateTestInventory());

        _battleScreen.OnBattleExit = () =>
        {
            // Restore previous screen from State
            var nextScreen = _state.CurrentScreen switch
            {
                "driving" => (IGameScreen)_driving,
                "space" => _spaceflight,
                _ => _overworld
            };
            var nextName = _state.CurrentScreen ?? "overworld";

            _screens.TransitionTo(nextScreen, nextName, () =>
            {
                _mode = GameMode.Exploration;
                _battleScreen.CleanupBattle();
            });
        };

#pragma warning disable CS0162 // Unreachable code (toggle DebugStartInBattle for testing)
        if (DebugStartInBattle)
        {
            _mode = GameMode.Battle;
            _battleScreen.EnterBattle();
        }
#pragma warning restore CS0162
    }

    protected override void Update(GameTime gameTime)
    {
        if (!_gameLoopLogged)
        {
            GameLog($"GAME STATE: _mode={_mode}, ActiveScreen={_screens.ActiveScreen?.GetType().Name}, DebugStartInBattle={DebugStartInBattle}");
            GameLog($"  _overworld null={_overworld is null}");
            GameLog($"  _pauseOverlay null={_pauseOverlay is null}");
            _gameLoopLogged = true;
        }

        _input.Update();
        var input = _input.Current;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // ── Pause overlay ──
        if (_pauseOverlay != null)
        {
            _pauseOverlay.Update(dt, input);
            if (_pauseOverlay.IsFinished)
            {
                if (_pauseOverlay is PauseScreen ps && ps.RequestCharacterSelect)
                {
                    var chars = _database.GetAllCharacters();
                    if (chars.Count > 0)
                    {
                        var folders = chars.Select(c => Path.GetDirectoryName(c.ManifestPath)!).ToArray();
                        var names = chars.Select(c => c.Name).ToArray();
                        var categories = chars.Select(c => c.Category).ToArray();
                        _pauseOverlay = new CharacterSelectScreen(folders, names, categories);
                        base.Update(gameTime);
                        return;
                    }
                }
                else if (_pauseOverlay is CharacterSelectScreen cs && cs.SelectedFolder != null)
                {
                    // Find the character ID by matching the selected folder
                    var allChars = _database.GetAllCharacters();
                    var match = allChars.FirstOrDefault(c =>
                        string.Equals(Path.GetDirectoryName(c.ManifestPath), cs.SelectedFolder, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        _state.SetCharacterId(match.Id);
                        _freeRoam.LoadCharacter(cs.SelectedFolder);
                    }
                }
                _pauseOverlay = null;
            }
            base.Update(gameTime);
            return;
        }

        if (_mode == GameMode.Battle)
        {
            _battleScreen.Update(dt, input, gameTime.TotalGameTime.TotalSeconds);
        }

        if (_mode != GameMode.Battle)
        {
            // Tab opens pause menu
            if (input.PausePressed)
            {
                _pauseOverlay = new PauseScreen();
                base.Update(gameTime);
                return;
            }

            if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Z))
                _ammo.ToggleProjectileType();

            if (input.CancelPressed && !_screens.IsTransitioning)
            {
                var (next, name) = _screens.ActiveScreen switch
                {
                    OverworldScreen => ((IGameScreen)_driving, "driving"),
                    DrivingScreen => ((IGameScreen)_spaceflight, "space"),
                    SpaceFlightScreen => ((IGameScreen)_freeRoam, "freeroam"),
                    FreeRoamScreen => ((IGameScreen)_overworld, "overworld"),
                    _ => ((IGameScreen)_overworld, "overworld"),
                };
                _screens.TransitionTo(next, name);
            }
        }

        _screens.Update(dt, screen =>
        {
            if (_mode != GameMode.Battle)
            {
                screen.Update(gameTime, input);
                _coinCollector.CollectFromScreen(screen);

                if (screen is OverworldScreen overworld)
                {
                    _state.PlayerPosition = overworld.Position;
                }
                else if (screen is FreeRoamScreen modelTest)
                {
                    _state.PlayerPosition = modelTest.Position;
                }
            }
        });

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_mode == GameMode.Battle)
        {
            _battleScreen.Draw3DScene(GraphicsDevice);
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            _battleScreen.DrawUI(Starfield2026.Core.UI.UITheme.GetFontScale(GraphicsDevice.Viewport.Width), GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _spriteBatch.End();
        }
        else
        {
            _screens.ActiveScreen.Draw(GraphicsDevice);
        }

        _spriteBatch.Begin();

        string screenType = _screens.ActiveScreen switch
        {
            OverworldScreen => "overworld",
            DrivingScreen => "driving",
            FreeRoamScreen => "freeroam",
            _ => "space"
        };
        float? speed = _screens.ActiveScreen switch
        {
            DrivingScreen => _driving.CurrentSpeed,
            SpaceFlightScreen => _spaceflight.CurrentSpeed,
            _ => null
        };
        int overworldBoosts = _screens.ActiveScreen == _overworld ? (_overworld.Boosts?.BoostCount ?? 0) : 0;
        Vector3? playerWorldPos = screenType switch
        {
            "freeroam" => _freeRoam.Position,
            "overworld" => _overworld.Position,
            _ => null
        };
        float playerYaw = screenType switch
        {
            "freeroam" => _freeRoam.Yaw,
            "overworld" => _overworld.Yaw,
            _ => 0f
        };
        if (_mode != GameMode.Battle)
        {
            _hud.Draw(GraphicsDevice, _state, _ammo, _boosts, screenType, speed, overworldBoosts, playerWorldPos, playerYaw);
        }
        _hud.DrawTransition(GraphicsDevice, _screens.GetTransitionAlpha());

        // Pause overlay on top
        if (_pauseOverlay != null)
        {
            int sw = GraphicsDevice.Viewport.Width;
            int sh = GraphicsDevice.Viewport.Height;
            int fs = UITheme.GetFontScale(sw);
            _pauseOverlay.Draw(_spriteBatch, _pixel, _uiFont, sw, sh, fs);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void HandleMapTransition(WarpConnection warp)
    {
        float scale = 2f;
        float worldX = 0f;
        float worldZ = 0f;

        if (Starfield2026.Core.Maps.MapCatalog.TryGetMap(warp.TargetMapId, out var targetMap) && targetMap != null)
        {
            worldX = (warp.TargetX - targetMap.Width / 2f) * scale;
            worldZ = (warp.TargetY - targetMap.Height / 2f) * scale;
        }
        else
        {
            // Fallback assumption just in case
            worldX = (warp.TargetX - 40) * scale;
            worldZ = (warp.TargetY - 40) * scale;
        }

        var newPos = new Vector3(worldX, 0.825f, worldZ);

        _overworld.LoadMap(warp.TargetMapId, newPos);
        _state.SetMap(warp.TargetMapId);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _state.Save();
            _database?.Dispose();
        }
        base.Dispose(disposing);
    }
}
