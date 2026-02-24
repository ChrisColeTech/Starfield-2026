#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Controllers;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;
namespace Starfield2026.ModelLoader.Screens;

public class FreeRoamScreen
{
    private GraphicsDevice _device = null!;
    private QuadrantGridRenderer _grid = null!;
    private CubeRenderer _cubeRenderer = null!;
    private OverworldCharacter? _character;
    private PlayerController _player = new();

    public AnimationLoadMode LoadMode { get; set; } = AnimationLoadMode.FillMissing;
    public HashSet<string> FillTags { get; set; } = new() { "Jump", "Land" };
    public Dictionary<string, string> SharedAnimationFolders { get; set; } = new();

// Camera state
    private float _camYaw;
    private float _camPitch = -0.15f;
    private float _camDist = 7f;
    private float _camYawOffset;
    private Vector3 _camTarget;
    private float _camSmoothedYaw;
    private float _smoothedCamDist;
    private bool _camInitialized;
    private Vector3 _camTargetVelocity;
    private float _camDistVelocity;
    private float _camYawVelocity;

    private const float CamPositionSmoothTime = 0.2f;
    private const float CamDistSmoothTime = 0.4f;
    private const float CamYawSmoothTime = 0.25f;
    private const float CamYawSpeed = 2f;
    private const float CamPitchSpeed = 1f;
    private const float CamZoomSpeed = 10f;
    private const float CamMinDist = 3f;
    private const float CamMaxDist = 40f;
    private const float CamWalkDist = 7f;
    private const float CamRunDist = 12f;
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
            var animSet = AnimationSetLoader.Load(
                folderPath,
                resolveSharedFolder: (path, skel) =>
                    TrainerGender.IsTrainerFolder(path)
                        ? SharedAnimationResolver.Resolve(path, skel, SharedAnimationFolders)
                        : null,
                loadMode: LoadMode,
                fillTags: FillTags);

            _character ??= new OverworldCharacter();
            _character.Load(_device, animSet);
            StatusText = $"Loaded: {System.IO.Path.GetFileName(folderPath)} ({animSet.ClipsByTag.Count} tags)";
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[FreeRoam] Failed to load character: {ex.Message}");
            _character?.Dispose();
            _character = null;
            StatusText = $"Failed: {System.IO.Path.GetFileName(folderPath)}";
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
        
        if (_player.IsMovingBackward)
        {
            _player.SetFacingCamera(_camSmoothedYaw);
        }

        _character?.Update(dt, _player.IsMoving, _player.IsRunning, _player.IsGrounded);
        UpdateCamera(dt);
    }

private void UpdateCamera(float dt)
    {
        if (!_camInitialized)
        {
            _camTarget = _player.Position;
            _camSmoothedYaw = _player.Yaw + MathHelper.Pi + _camYawOffset;
            _smoothedCamDist = _camDist;
            _camTargetVelocity = Vector3.Zero;
            _camDistVelocity = 0f;
            _camYawVelocity = 0f;
            _camInitialized = true;
        }

        bool playerMoving = _player.Speed > 0.5f;
        bool playerMovingForward = playerMoving && !_player.IsMovingBackward;

        _camTarget = SmoothDamp3(_camTarget, _player.Position, ref _camTargetVelocity, CamPositionSmoothTime, dt);

        if (playerMovingForward)
        {
            float targetYaw = _player.Yaw + MathHelper.Pi + _camYawOffset;
            float yawDiff = targetYaw - _camSmoothedYaw;
            while (yawDiff > MathHelper.Pi) yawDiff -= MathHelper.TwoPi;
            while (yawDiff < -MathHelper.Pi) yawDiff += MathHelper.TwoPi;
            _camSmoothedYaw = SmoothDampAngle(_camSmoothedYaw, _camSmoothedYaw + yawDiff, ref _camYawVelocity, CamYawSmoothTime, dt);
        }

        float runDistOffset = _player.IsRunning ? (CamRunDist - CamWalkDist) : 0f;
        float targetDist = _camDist + runDistOffset;
        targetDist = MathHelper.Clamp(targetDist, CamMinDist, CamMaxDist);
        _smoothedCamDist = SmoothDamp(_smoothedCamDist, targetDist, ref _camDistVelocity, CamDistSmoothTime, dt);

        _camYaw = _camSmoothedYaw;

        var lookAt = _camTarget + Vector3.Up * 1.5f;

        var offset = new Vector3(
            (float)(_smoothedCamDist * Math.Cos(_camPitch) * Math.Sin(_camSmoothedYaw)),
            (float)(_smoothedCamDist * -Math.Sin(_camPitch)),
            (float)(_smoothedCamDist * Math.Cos(_camPitch) * Math.Cos(_camSmoothedYaw)));

        _camPosition = lookAt + offset;

        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        _view = Matrix.CreateLookAt(_camPosition, lookAt, Vector3.Up);
        _projection = Matrix.CreatePerspectiveFieldOfView(Fov, aspect, NearPlane, FarPlane);
    }

    private static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float dt)
    {
        float omega = 2f / smoothTime;
        float x = omega * dt;
        float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        float change = current - target;
        float temp = (velocity + omega * change) * dt;
        velocity = (velocity - omega * temp) * exp;
        return target + (change + temp) * exp;
    }

    private static Vector3 SmoothDamp3(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime, float dt)
    {
        return new Vector3(
            SmoothDamp(current.X, target.X, ref velocity.X, smoothTime, dt),
            SmoothDamp(current.Y, target.Y, ref velocity.Y, smoothTime, dt),
            SmoothDamp(current.Z, target.Z, ref velocity.Z, smoothTime, dt));
    }

    private static float SmoothDampAngle(float current, float target, ref float velocity, float smoothTime, float dt)
    {
        float diff = target - current;
        while (diff > MathHelper.Pi) diff -= MathHelper.TwoPi;
        while (diff < -MathHelper.Pi) diff += MathHelper.TwoPi;
        return current + SmoothDamp(0f, diff, ref velocity, smoothTime, dt);
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
            var cubePos = new Vector3(pos.X, pos.Y + 0.75f, pos.Z);
            _cubeRenderer.Draw(device, _view, _projection, cubePos, yaw, 1.5f, new Color(0, 220, 255));
        }
    }
}
