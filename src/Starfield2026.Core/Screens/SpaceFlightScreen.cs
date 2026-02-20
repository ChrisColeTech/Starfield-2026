using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Controllers;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.Systems;

namespace Starfield2026.Core.Screens;

public class SpaceFlightScreen : IGameScreen
{
    private ShipController _ship = null!;
    private GridRenderer _gridFloor = null!;
    private GridRenderer _gridCeiling = null!;
    private SpaceBackground _background = null!;
    private CubeRenderer _cubeRenderer = null!;
    private CoinCollectibleSystem _coinSystem = null!;
    private ProjectileSystem _projectiles = null!;
    private BossSystem _boss = null!;
    
    public AmmoSystem? Ammo { get; set; }
    public BoostSystem? Boosts { get; set; }
    public CoinCollectibleSystem CoinSystem => _coinSystem;
    public float CurrentSpeed => _ship.CurrentSpeed;
    public event Action? OnLandRequested;
    private Vector2 _cameraOffset;
    
    private const float DeadZoneX = 8f;
    private const float DeadZoneY = 4f;
    private const float CameraFollowSpeed = 8f;
    private const float CameraBehindZ = 18f;
    private const float CameraHeight = 6f;
    private const float LookAheadZ = 30f;
    
    private const float FloorY = -50f;
    private const float CeilingY = 80f;
    private const float MaxX = 100f;
    private const float CoinCollectRadius = 8f;
    
    public void Initialize(GraphicsDevice device)
    {
        _ship = new ShipController();
        _ship.Initialize(new Vector3(0, 4f, 0));
        _cameraOffset = Vector2.Zero;
        
        _gridFloor = new GridRenderer
        {
            Spacing = 4f,
            GridHalfSize = 400,
            PlaneOffset = FloorY,
            Orientation = GridOrientation.Horizontal,
            GridColor = new Color(0, 140, 255, 150),
        };
        _gridFloor.Initialize(device);
        
        _gridCeiling = new GridRenderer
        {
            Spacing = 5f,
            GridHalfSize = 400,
            PlaneOffset = CeilingY,
            Orientation = GridOrientation.Horizontal,
            GridColor = new Color(100, 20, 180, 100),
        };
        _gridCeiling.Initialize(device);
        
        _background = new SpaceBackground(600)
        {
            SpreadRadius = 60f,
            DepthRange = 120f,
        };
        _background.Initialize(device);
        
        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);
        
        _coinSystem = new CoinCollectibleSystem
        {
            DriftSpeed = 0f,
            SpawnInterval = 3f,
        };
        _coinSystem.Initialize(device);
        
        _projectiles = new ProjectileSystem();
        _projectiles.Initialize(device);
        
        _boss = new BossSystem();
        _boss.Initialize(device);
    }
    
    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        _ship.Update(dt, input);
        _ship.ClampToBounds(MaxX, FloorY, CeilingY);
        
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D1))
        {
            _boss.Spawn(_ship.Position, 20);
        }
        
        float relativeX = _ship.Position.X - _cameraOffset.X;
        float relativeY = _ship.Position.Y - _cameraOffset.Y;
        
        if (Math.Abs(relativeX) > DeadZoneX)
        {
            float excess = relativeX - Math.Sign(relativeX) * DeadZoneX;
            _cameraOffset.X += excess * CameraFollowSpeed * dt;
        }
        if (Math.Abs(relativeY) > DeadZoneY)
        {
            float excess = relativeY - Math.Sign(relativeY) * DeadZoneY;
            _cameraOffset.Y += excess * CameraFollowSpeed * dt;
        }
        
        float floorSnX = _ship.Position.X - (_ship.Position.X % _gridFloor.Spacing);
        float floorSnZ = _ship.Position.Z - (_ship.Position.Z % _gridFloor.Spacing);
        _gridFloor.ScrollOffset = new Vector3(floorSnX, 0, floorSnZ);
        
        float ceilSnX = _ship.Position.X - (_ship.Position.X % _gridCeiling.Spacing);
        float ceilSnZ = _ship.Position.Z - (_ship.Position.Z % _gridCeiling.Spacing);
        _gridCeiling.ScrollOffset = new Vector3(ceilSnX, 0, ceilSnZ);
        
        _background.Update(dt, _ship.CurrentSpeed, _ship.Position);
        
        _coinSystem.Update(dt, _ship.Position, CoinCollectRadius, _ship.CurrentSpeed, 80f, 25f, 8f);
        
        if (input.IsKeyHeld(Microsoft.Xna.Framework.Input.Keys.Space) && Ammo != null && Ammo.CanFire(Ammo.SelectedType))
        {
            if (_projectiles.TryFire(
                _ship.Position + new Vector3(0, 0, -2f),
                new Vector3(0, 0, -100f - _ship.CurrentSpeed),
                Ammo.SelectedType))
            {
                Ammo.TryConsumeSelectedAmmo();
            }
        }
        _projectiles.Update(dt);
        
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.LeftShift) && Boosts != null && Boosts.TryActivate())
            _ship.ActivateBoost(10f);
        
        if (_boss.Active)
        {
            _boss.Update(dt);
            
            int hits = _projectiles.CheckCollisionsWithDamage(_boss.Position, _boss.HitRadius, out int damage);
            if (damage > 0)
                _boss.TakeDamage(damage);
        }
        
        if (input.CancelPressed)
            OnLandRequested?.Invoke();
    }
    
    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(3, 3, 15));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
        
        float aspect = device.Viewport.Width / (float)device.Viewport.Height;
        
        var camPos = new Vector3(
            _cameraOffset.X,
            _cameraOffset.Y + CameraHeight,
            _ship.Position.Z + CameraBehindZ);
        
        var camTarget = new Vector3(
            _cameraOffset.X,
            _ship.Position.Y,
            _ship.Position.Z - LookAheadZ);
        
        var view = Matrix.CreateLookAt(camPos, camTarget, Vector3.Up);
        
        float speedFactor = _ship.CurrentSpeed / 80f;
        float fov = MathHelper.PiOver4 + speedFactor * 0.35f;
        var proj = Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.5f, 1500f);
        
        _background.Draw(device, view, proj, _ship.Position, _ship.CurrentSpeed);
        _gridFloor.Draw(device, view, proj);
        _gridCeiling.Draw(device, view, proj);
        _coinSystem.Draw(device, view, proj);
        _projectiles.Draw(device, view, proj);
        _boss.Draw(device, view, proj);
        
        float roll = -_ship.Position.X * 0.015f;
        _cubeRenderer.Draw(device, view, proj, _ship.Position + _ship.BobOffset, roll, 1.8f, new Color(100, 255, 100));
    }
    
    public void OnEnter()
    {
        _coinSystem.ResetSpawnTimer();
        _projectiles.Clear();
        _boss.Active = false;
    }
    
    public void OnExit() { }
}
