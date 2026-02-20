using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Camera;
using Starfield2026.Core.Controllers;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.Systems;

namespace Starfield2026.Core.Screens;

public class DrivingScreen : IGameScreen
{
    private GraphicsDevice _device = null!;
    private ChaseCamera _camera = null!;
    private VehicleController _vehicle = null!;
    private GridRenderer _groundGrid = null!;
    private DrivingBackground _background = null!;
    private CubeRenderer _cubeRenderer = null!;
    private CoinCollectibleSystem _coinSystem = null!;
    private ProjectileSystem _projectiles = null!;
    
    public AmmoSystem? Ammo { get; set; }
    public BoostSystem? Boosts { get; set; }
    public CoinCollectibleSystem CoinSystem => _coinSystem;
    public float CurrentSpeed => _vehicle.Speed;
    public event Action? OnExitDrivingRequested;
    
    public void ActivateBoost(float duration = 10f)
    {
        _vehicle.ActivateBoost(duration);
    }
    
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
        
        _vehicle = new VehicleController();
        _vehicle.Initialize(new Vector3(0, 0.75f, 0));
        
        _groundGrid = new GridRenderer
        {
            Spacing = 3f,
            GridHalfSize = 300,
            PlaneOffset = 0f,
            Orientation = GridOrientation.Horizontal,
            GridColor = new Color(80, 120, 80, 180),
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
            SpawnInterval = 2f,
        };
        _coinSystem.Initialize(device);
        
        _projectiles = new ProjectileSystem { FireRate = 0.15f };
        _projectiles.Initialize(device);
        
        _camDistance = _baseDistance;
    }
    
    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        _vehicle.Update(dt, input);
        
        _background.Update(dt, _vehicle.Speed, _vehicle.Position);
        
        _coinSystem.Update(dt, _vehicle.Position, 5f, _vehicle.Speed, 60f, 15f, 0f);
        
        if (input.FireHeld && Ammo != null && Ammo.CanFire(Ammo.SelectedType))
        {
            float projSpeed = 150f + Math.Abs(_vehicle.Speed);
            if (_projectiles.TryFire(
                _vehicle.Position + _vehicle.Forward * 2f,
                _vehicle.Forward * projSpeed,
                Ammo.SelectedType))
            {
                Ammo.TryConsumeSelectedAmmo();
            }
        }
        _projectiles.Update(dt);
        
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.LeftShift) && Boosts != null && Boosts.TryActivate())
            _vehicle.ActivateBoost(10f);
        
        float speedRatio = Math.Abs(_vehicle.Speed) / 100f;
        float targetDist = _baseDistance;
        
        float distDiff = targetDist - _camDistance;
        _distanceVelocity += distDiff * 3.5f * dt;
        _distanceVelocity *= 0.85f;
        _camDistance += _distanceVelocity * dt;
        
        _camera.Distance = _camDistance;
        
        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        _camera.Update(_vehicle.Position, _vehicle.Yaw, aspect, 0f, dt);
        
        float snap = _groundGrid.Spacing;
        float gridSnX = _vehicle.Position.X - (_vehicle.Position.X % snap);
        float gridSnZ = _vehicle.Position.Z - (_vehicle.Position.Z % snap);
        _groundGrid.ScrollOffset = new Vector3(gridSnX, 0, gridSnZ);
        
        if (input.ConfirmPressed)
            OnExitDrivingRequested?.Invoke();
    }
    
    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(10, 15, 25));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
        
        var view = _camera.View;
        var proj = _camera.Projection;
        
        _background.Draw(device, view, proj, _vehicle.Position, _vehicle.Speed);
        _groundGrid.Draw(device, view, proj);
        _coinSystem.Draw(device, view, proj);
        _projectiles.Draw(device, view, proj);
        
        _cubeRenderer.Draw(device, view, proj, 
            _vehicle.Position + _vehicle.RumbleOffset, 
            _vehicle.Yaw, 1.8f, new Color(255, 100, 50));
    }
    
    public void OnEnter()
    {
        _coinSystem.ResetSpawnTimer();
        _projectiles.Clear();
    }
    
    public void OnExit() { }
}
