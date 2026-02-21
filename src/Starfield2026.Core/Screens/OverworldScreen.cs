using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Camera;
using Starfield2026.Core.Controllers;
using Starfield2026.Core.Input;
using Starfield2026.Core.Maps;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.Systems;

namespace Starfield2026.Core.Screens;

public class OverworldScreen : IGameScreen
{
    private GraphicsDevice _device = null!;
    private readonly OrbitCamera _camera = new();
    private readonly PlayerController _player = new();
    private readonly WorldController _world = new();
    private readonly EncounterController _encounters = new();
    
    private GridRenderer _groundGrid = null!;
    private OverworldBackground _background = null!;
    private CubeRenderer _cubeRenderer = null!;
    private MapRenderer3D _mapRenderer = null!;
    private CoinCollectibleSystem _coinSystem = null!;
    
    public AmmoSystem? Ammo { get; set; }
    public BoostSystem? Boosts { get; set; }
    public CoinCollectibleSystem CoinSystem => _coinSystem;
    public string? CurrentMapId => _world.CurrentMapId;
    public Vector3 Position => _player.Position;

    public event Action? OnLaunchRequested;
    public event Action? OnRandomEncounter;
    public event Action<WarpConnection>? OnMapTransition;

    public void Initialize(GraphicsDevice device)
    {
        _device = device;
        
        _camera.Distance = 18f;
        _camera.Pitch = -0.3f;
        _camera.Yaw = MathHelper.Pi;
        _camera.MinDistance = 8f;
        _camera.MaxDistance = 35f;
        _camera.FollowSpeed = 2.5f;
        
        _player.Initialize(new Vector3(0, 0.825f, 0));
        _camera.SnapToTarget(_player.Position);
        
        _world.OnMapTransition += warp => OnMapTransition?.Invoke(warp);
        _encounters.OnEncounter += () => OnRandomEncounter?.Invoke();
        
        _groundGrid = new GridRenderer
        {
            Spacing = 2f,
            GridHalfSize = 60,
            PlaneOffset = 0f,
            GridColor = new Color(40, 180, 80, 150),
        };
        _groundGrid.Initialize(device);
        
        _background = new OverworldBackground(400)
        {
            SpreadRadius = 80f,
            DepthRange = 150f,
        };
        _background.Initialize(device);
        
        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);
        
        _mapRenderer = new MapRenderer3D(100);
        _mapRenderer.Initialize(device);
        
        _coinSystem = new CoinCollectibleSystem
        {
            DriftSpeed = 0f,
            SpawnInterval = 2.5f,
        };
        _coinSystem.Initialize(device);
    }
    
    public void LoadMap(string mapId, Vector3? spawnPosition = null)
    {
        _world.LoadMap(mapId, 40, 40); // Spawn X/Y are deprecated, MapController finds ID
        _world.SetMapBounds(_player);
        
        if (_world.CurrentMap != null)
        {
            if (spawnPosition.HasValue)
            {
                _player.SetPosition(spawnPosition.Value, _player.Yaw);
                _camera.SnapToTarget(_player.Position);
            }
            else
            {
                // Fallback logical grid center
                float scale = 2f;
                float worldX = (40 - _world.CurrentMap.Width / 2f) * scale;
                float worldZ = (40 - _world.CurrentMap.Height / 2f) * scale;
                _player.SetPosition(new Vector3(worldX, 0.825f, worldZ), _player.Yaw);
                _camera.SnapToTarget(_player.Position);
            }
        }
    }
    
    public void SetPlayerPosition(float worldX, float worldZ)
    {
        _player.SetPosition(new Vector3(worldX, 0.825f, worldZ), _player.Yaw);
        _camera.SnapToTarget(_player.Position);
    }
    
    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        if (input.CameraYaw != 0)
            _camera.Rotate(input.CameraYaw * _camera.YawSpeed * dt, 0);
        if (input.CameraPitch != 0)
            _camera.Rotate(0, input.CameraPitch * _camera.PitchSpeed * dt);
        if (input.CameraZoom != 0)
            _camera.Zoom(input.CameraZoom * _camera.ZoomSpeed * dt);
        
        _player.Boosts = Boosts;
        
        _player.Update(dt, input, _world.CurrentMap);
        
        _world.Update(dt, _player.Position);
        _encounters.Update(dt, _player.IsMoving, _world.CurrentMap, _player.Position);
        
        float targetDist = _player.IsRunning ? 22f : 18f;
        _camera.Distance = MathHelper.Lerp(_camera.Distance, targetDist, 1f - (float)Math.Exp(-5f * dt));
        
        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        _camera.Update(_player.Position, aspect, dt, _player.Yaw);
        
        _background.Update(dt, _player.Speed, _player.Position);
        
        _coinSystem.Update(dt, _player.Position, 3f, _player.Speed, 30f, 25f, 4f, 60f, _world.CurrentMap);
        
        // CoinCollector will handle GetAndResetNewlyCollected() globally in Starfield2026Game
        
        float snap = _groundGrid.Spacing;
        float sx = (float)Math.Floor(_player.Position.X / snap) * snap;
        float sz = (float)Math.Floor(_player.Position.Z / snap) * snap;
        _groundGrid.ScrollOffset = new Vector3(sx, 0, sz);
        
        if (input.ConfirmPressed)
            OnLaunchRequested?.Invoke();
    }
    
    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(15, 20, 45));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
        
        var view = _camera.View;
        var proj = _camera.Projection;
        
        var bgCenter = new Vector3(_player.Position.X, 0f, _player.Position.Z);
        _background.Draw(device, view, proj, bgCenter, _player.IsRunning ? 8f : (_player.Speed > 0.1f ? 4f : 0f));
        
        if (_world.CurrentMap != null)
        {
            if (_world.CurrentMap.UseWireframeGrid)
            {
                _groundGrid.Draw(device, view, proj);
                _mapRenderer.Draw(device, view, proj, _world.CurrentMap, _player.Position, skipTileId: 5);
            }
            else
            {
                _mapRenderer.Draw(device, view, proj, _world.CurrentMap, _player.Position);
            }
        }
        else
        {
            _groundGrid.Draw(device, view, proj);
        }
        
        _coinSystem.Draw(device, view, proj);
        
        // Draw drop shadow directly on the absolute ground plane
        float groundY = 0.05f;
        _cubeRenderer.Draw(device, view, proj,
            new Vector3(_player.Position.X, groundY, _player.Position.Z),
            _player.Yaw, new Vector3(1.5f, 0.05f, 1.5f), Color.Black * 0.4f);
        
        float bob = _player.IsMoving ? (float)Math.Sin(Environment.TickCount64 / 150.0) * 0.08f : 0f;
        float hoverBob = _player.IsHovering ? _player.HoverBobOffset : 0f;
        _cubeRenderer.Draw(device, view, proj,
            _player.Position + new Vector3(0, bob + hoverBob, 0),
            _player.Yaw, 1.5f, new Color(0, 220, 255));
    }
    
    public void OnEnter()
    {
        _coinSystem.ResetSpawnTimer();
    }
    
    public void OnExit() { }
}
