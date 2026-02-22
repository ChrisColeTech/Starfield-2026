#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Controllers;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;
using Starfield2026.ModelLoader.Skeletal;

namespace Starfield2026.ModelLoader.Screens;

public class FreeRoamScreen
{
    private GraphicsDevice _device = null!;
    private QuadrantGridRenderer _grid = null!;
    private CubeRenderer _cubeRenderer = null!;
    private OverworldCharacter? _character;
    private PlayerController _player = new();

    // Camera state
    private float _camYaw;
    private float _camPitch = -0.15f;
    private float _camDist = 12f;
    private float _camYawOffset;
    private Vector3 _camTarget;
    private float _camSmoothedYaw;
    private bool _camInitialized;

    private const float CamFollowSpeed = 3f;
    private const float CamYawSpeed = 2f;
    private const float CamPitchSpeed = 1f;
    private const float CamZoomSpeed = 10f;
    private const float CamMinDist = 3f;
    private const float CamMaxDist = 40f;
    private const float CamMinPitch = -1.4f;
    private const float CamMaxPitch = -0.1f;
    private const float Fov = MathHelper.PiOver4;
    private const float NearPlane = 0.1f;
    private const float FarPlane = 500f;

    private Matrix _view;
    private Matrix _projection;
    private Vector3 _camPosition;

    public Vector3 Position => _player.Position;
    public float Yaw => _player.Yaw;
    public string StatusText { get; private set; } = "No model loaded";

    public void Initialize(GraphicsDevice device)
    {
        _device = device;

        _grid = new QuadrantGridRenderer
        {
            Spacing = 2f,
            GridHalfSize = 250,
            PlaneOffset = 0f,
        };
        _grid.Initialize(device);

        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);

        _player.Initialize(new Vector3(0, 0f, 0));
        _player.WorldHalfSize = 500f;
        _camTarget = _player.Position;
    }

    public void LoadCharacter(string folderPath)
    {
        try
        {
            ModelLoaderLog.Info($"[FreeRoam] LoadCharacter: {folderPath}");
            _character?.Dispose();
            _character = new OverworldCharacter();
            _character.Load(_device, folderPath);
            StatusText = $"Loaded: {System.IO.Path.GetFileName(folderPath)}";
            ModelLoaderLog.Info($"[FreeRoam] Character loaded successfully: {StatusText}");
        }
        catch (Exception ex)
        {
            _character?.Dispose();
            _character = null;
            StatusText = $"Load failed: {ex.Message}";
            ModelLoaderLog.Error($"[FreeRoam] Character load failed: {folderPath}", ex);
        }
    }

    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (input.CameraYaw != 0)
            _camYawOffset += input.CameraYaw * CamYawSpeed * dt;
        if (input.CameraPitch != 0)
            _camPitch = MathHelper.Clamp(_camPitch + input.CameraPitch * CamPitchSpeed * dt, CamMinPitch, CamMaxPitch);
        if (input.CameraZoom != 0)
            _camDist = MathHelper.Clamp(_camDist + input.CameraZoom * CamZoomSpeed * dt, CamMinDist, CamMaxDist);

        _player.Update(dt, input);
        _character?.Update(dt, _player.IsMoving, _player.IsRunning, _player.IsGrounded);
        UpdateCamera(dt);
    }

    private void UpdateCamera(float dt)
    {
        if (!_camInitialized)
        {
            _camTarget = _player.Position;
            _camSmoothedYaw = _player.Yaw + MathHelper.Pi + _camYawOffset;
            _camInitialized = true;
        }

        bool playerMoving = _player.Speed > 0.5f;

        float t = 1f - (float)Math.Exp(-CamFollowSpeed * dt);
        _camTarget = Vector3.Lerp(_camTarget, _player.Position, t);

        if (playerMoving)
        {
            float targetYaw = _player.Yaw + MathHelper.Pi + _camYawOffset;
            float yawDiff = targetYaw - _camSmoothedYaw;
            while (yawDiff > MathHelper.Pi) yawDiff -= MathHelper.TwoPi;
            while (yawDiff < -MathHelper.Pi) yawDiff += MathHelper.TwoPi;
            _camSmoothedYaw += yawDiff * CamFollowSpeed * dt;
        }

        _camYaw = _camSmoothedYaw;

        var lookAt = _camTarget + Vector3.Up * 1.5f;

        var offset = new Vector3(
            (float)(_camDist * Math.Cos(_camPitch) * Math.Sin(_camSmoothedYaw)),
            (float)(_camDist * -Math.Sin(_camPitch)),
            (float)(_camDist * Math.Cos(_camPitch) * Math.Cos(_camSmoothedYaw)));

        _camPosition = lookAt + offset;

        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        _view = Matrix.CreateLookAt(_camPosition, lookAt, Vector3.Up);
        _projection = Matrix.CreatePerspectiveFieldOfView(Fov, aspect, NearPlane, FarPlane);
    }

    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(20, 25, 50));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
        device.SamplerStates[0] = SamplerState.AnisotropicClamp;

        _grid.Draw(device, _view, _projection);

        var pos = _player.Position;
        float yaw = _player.Yaw;

        // Shadow
        _cubeRenderer.Draw(device, _view, _projection,
            new Vector3(pos.X, 0.05f, pos.Z),
            yaw, new Vector3(1.5f, 0.05f, 1.5f), Color.Black * 0.4f);

        // Character or fallback cube
        if (_character is { IsLoaded: true })
        {
            _character.Draw(device, _view, _projection, pos, yaw);
        }
        else
        {
            _cubeRenderer.Draw(device, _view, _projection, pos, yaw, 1.5f, new Color(0, 220, 255));
        }
    }
}
