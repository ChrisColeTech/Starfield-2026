using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Input;
using Starfield2026.Core.Managers;
using Starfield2026.Core.Maps;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.Save;
using Starfield2026.Core.Screens;
using Starfield2026.Core.Systems;
using Starfield2026.Core.UI;

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
        InitializeScreens();
        
        base.Initialize();
    }
    
    private void InitializeDatabase()
    {
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
        _overworld = new OverworldScreen { Ammo = _ammo };
        _driving = new DrivingScreen { Ammo = _ammo, Boosts = _boosts };
        _spaceflight = new SpaceFlightScreen { Ammo = _ammo, Boosts = _boosts };
        
        _overworld.Initialize(GraphicsDevice);
        _driving.Initialize(GraphicsDevice);
        _spaceflight.Initialize(GraphicsDevice);
        
        _overworld.OnLaunchRequested += () => _screens.TransitionTo(_driving, "driving");
        _overworld.OnMapTransition += warp => HandleMapTransition(warp);
        _driving.OnExitDrivingRequested += () => _screens.TransitionTo(_spaceflight, "space");
        _spaceflight.OnLandRequested += () => _screens.TransitionTo(_overworld, "overworld");
        
        _screens = new ScreenManager(screenName => _state.SetScreen(screenName));
        
        var initial = _state.CurrentScreen switch
        {
            "overworld" => (IGameScreen)_overworld,
            "driving" => _driving,
            _ => _spaceflight
        };
        _screens.SetInitialScreen(initial);
        
        _coinCollector = new CoinCollector(_state, _ammo, _boosts);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        
        var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        
        _hud = new HUDRenderer();
        _hud.Initialize(_spriteBatch, pixel);
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update();
        var input = _input.Current;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Z))
            _ammo.ToggleProjectileType();
        
        if (input.CancelPressed && !_screens.IsTransitioning)
        {
            var next = _screens.ActiveScreen switch
            {
                OverworldScreen => (IGameScreen)_driving,
                DrivingScreen => _spaceflight,
                SpaceFlightScreen => _overworld,
                _ => _overworld
            };
            var name = next == _overworld ? "overworld" : next == _driving ? "driving" : "space";
            _screens.TransitionTo(next, name);
        }
        
        _screens.Update(dt, screen =>
        {
            screen.Update(gameTime, input);
            _coinCollector.CollectFromScreen(screen);
        });
        
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _screens.ActiveScreen.Draw(GraphicsDevice);
        
        _spriteBatch.Begin();
        
        string screenType = _screens.ActiveScreen switch
        {
            OverworldScreen => "overworld",
            DrivingScreen => "driving",
            _ => "space"
        };
        float? speed = _screens.ActiveScreen switch
        {
            DrivingScreen => _driving.CurrentSpeed,
            SpaceFlightScreen => _spaceflight.CurrentSpeed,
            _ => null
        };
        _hud.Draw(GraphicsDevice, _state, _ammo, _boosts, screenType, speed);
        _hud.DrawTransition(GraphicsDevice, _screens.GetTransitionAlpha());
        
        _spriteBatch.End();
        base.Draw(gameTime);
    }
    
    private void HandleMapTransition(WarpConnection warp)
    {
        _overworld.LoadMap(warp.TargetMapId, warp.TargetX, warp.TargetY);
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
