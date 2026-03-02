#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Controllers;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Runtime;

public sealed class NavigationRuntime
{
    private readonly PlayerController _player = new();
    private readonly FollowCamera _camera = new();

    public Vector3 Position => _player.Position;
    public float Yaw => _player.Yaw;
    public float SmoothedYaw => _camera.SmoothedYaw;
    public Matrix View => _camera.View;
    public Matrix Projection => _camera.Projection;
    public Vector3 CameraPosition => _camera.Position;
    public PlayerController Player => _player;

    public void Initialize(Vector3 startPosition, float worldHalfSize)
    {
        _player.Initialize(startPosition);
        _player.WorldHalfSize = worldHalfSize;
        _camera.Initialize(startPosition);
    }

    public void ConfigureTerrain(Func<Vector3, float>? sampleHeight, Func<Vector3, float, bool>? passable,
        Func<Vector3, float>? cameraHeightSampler = null)
    {
        _player.TerrainHeightSampler = sampleHeight == null ? null : pos => sampleHeight(pos);
        _player.CollisionCheck = passable;
        _camera.TerrainHeightSampler = cameraHeightSampler ?? sampleHeight;
    }

    public void UpdateMovement(float dt, InputSnapshot input) => _player.Update(dt, input);

    public void UpdateCamera(GraphicsDevice device, float dt, InputSnapshot input, float targetHeight)
    {
        float aspect = device.Viewport.Width / (float)device.Viewport.Height;
        _camera.Update(dt, aspect, _player.Position, _player.Yaw, _player.Speed,
            _player.IsRunning, _player.IsMovingBackward, targetHeight,
            input.CameraYaw, input.CameraPitch, input.CameraZoom);
    }
}
