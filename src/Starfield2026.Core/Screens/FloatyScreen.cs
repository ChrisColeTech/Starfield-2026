using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Starfield2026.Core.Camera;
using Starfield2026.Core.Controllers;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.Systems;
using Starfield2026.Core.Systems.Coins;

namespace Starfield2026.Core.Screens;

public class FloatyScreen : IGameScreen
{
    private GraphicsDevice _device = null!;
    private ChaseCamera _camera = null!;
    private FloatyController _floaty = null!;
    private QuadrantGridRenderer _groundGrid = null!;
    private DrivingBackground _background = null!;
    private CubeRenderer _cubeRenderer = null!;
    private CoinCollectibleSystem _coinSystem = null!;
    private ProjectileSystem _projectiles = null!;
    public AmmoSystem? Ammo { get; set; }
    public BoostSystem? Boosts { get; set; }
    public EnemySystem? Enemies { get; set; }
    public Color PlayerTint { get; set; } = new Color(0, 220, 255);
    public CoinCollectibleSystem CoinSystem => _coinSystem;
    public float CurrentSpeed => _floaty.Speed;
    public Vector3 Position => _floaty.Position;
    public float Yaw => _floaty.Yaw;
    public float FuelPercent => _floaty.FuelPercent;
    public bool FuelOverheated => _floaty.FuelOverheated;

    private float _camDistance = 16f;
    private float _baseDistance = 16f;
    private float _distanceVelocity;

    public void Initialize(GraphicsDevice device)
    {
        _device = device;
        _camera = new ChaseCamera
        {
            Distance = 16f,
            Height = 5f,
            LookAheadDistance = 20f,
        };

        _floaty = new FloatyController();
        _floaty.Boosts = Boosts;
        _floaty.Initialize(new Vector3(0, 4f, 0));

        _groundGrid = new QuadrantGridRenderer
        {
            Spacing = 3f,
            GridHalfSize = 300,
            PlaneOffset = 0f,
        };
        _groundGrid.Initialize(device);

        _background = new DrivingBackground(300)
        {
            SpreadRadius = 80f,
            DepthRange = 150f,
        };
        _background.Initialize(device);

        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);

        _coinSystem = new CoinCollectibleSystem
        {
            DriftSpeed = 0f,
        };
        _coinSystem.Initialize(device, new InfiniteRunnerCoinSpawner
        {
            SpawnInterval = 2f,
            CorridorWidth = 15f,
        });

        _projectiles = new ProjectileSystem { FireRate = 0.15f };
        _projectiles.Initialize(device);

        _camDistance = _baseDistance;
    }

    public void SetPosition(Vector3 position)
    {
        _floaty.SetPosition(position, _floaty.Yaw);
    }

    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _floaty.Update(dt, input);

        _background.Update(dt, _floaty.Speed, _floaty.Position);

        _coinSystem.Update(dt, _floaty.Position, 5f, _floaty.Speed);

        // Space = shoot
        if (input.FireHeld && Ammo != null && Ammo.CanFire(Ammo.SelectedType))
        {
            float projSpeed = 150f + Math.Abs(_floaty.Speed);
            if (_projectiles.TryFire(
                _floaty.Position + _floaty.Forward * 2f,
                _floaty.Forward * projSpeed,
                Ammo.SelectedType))
            {
                Ammo.TryConsumeSelectedAmmo();
            }
        }
        _projectiles.Update(dt);

        Enemies?.Update(dt, _floaty.Position, _projectiles);

        // Shift = boost
        if (input.IsKeyJustPressed(Keys.LeftShift))
            _floaty.ActivateBoost();

        // Camera — pull out when moving, ease back in when stopped
        float speedRatio = Math.Clamp(Math.Abs(_floaty.Speed) / 30f, 0f, 1f);
        float targetDist = _baseDistance + speedRatio * 10f;
        float blend = 1f - (float)Math.Exp(-(speedRatio > 0.1f ? 3f : 1f) * dt);
        _camDistance += (targetDist - _camDistance) * blend;
        _camera.Distance = _camDistance;

        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        _camera.Update(_floaty.Position, _floaty.Yaw, aspect, 0f, dt);

        float snap = _groundGrid.Spacing;
        float gridSnX = _floaty.Position.X - (_floaty.Position.X % snap);
        float gridSnZ = _floaty.Position.Z - (_floaty.Position.Z % snap);
        _groundGrid.ScrollOffset = new Vector3(gridSnX, 0, gridSnZ);
    }

    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(10, 15, 25));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;

        var view = _camera.View;
        var proj = _camera.Projection;

        _background.Draw(device, view, proj, _floaty.Position, _floaty.Speed);
        _groundGrid.Draw(device, view, proj);
        _coinSystem.Draw(device, view, proj);
        _projectiles.Draw(device, view, proj);
        Enemies?.Draw(device, view, proj);

        // Shadow on ground — grows and fades with height
        float height = _floaty.Position.Y;
        float shadowScale = 1.5f + height * 0.15f;
        float shadowAlpha = Math.Clamp(1f - height * 0.008f, 0.1f, 0.4f);
        _cubeRenderer.Draw(device, view, proj,
            new Vector3(_floaty.Position.X, 0.05f, _floaty.Position.Z),
            _floaty.Yaw, new Vector3(shadowScale, 0.05f, shadowScale), Color.Black * shadowAlpha);

        // Player cube
        _cubeRenderer.Draw(device, view, proj,
            _floaty.Position + _floaty.RumbleOffset,
            _floaty.Yaw, 1.8f, PlayerTint);
    }

    public void OnEnter()
    {
        _projectiles.Clear();
        Enemies?.Clear();
    }

    public void OnExit() { }
}
