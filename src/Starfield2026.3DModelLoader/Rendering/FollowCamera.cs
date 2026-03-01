#nullable enable
using System;
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.Rendering;

public class FollowCamera
{
    private float _yaw;
    private float _pitch = -0.15f;
    private float _dist = 7f;
    private float _yawOffset;
    private Vector3 _target;
    private float _smoothedYaw;
    private float _smoothedDist;
    private bool _initialized;
    private Vector3 _targetVelocity;
    private float _distVelocity;
    private float _yawVelocity;

    private const float PositionSmoothTime = 0.2f;
    private const float DistSmoothTime = 0.4f;
    private const float YawSmoothTime = 0.25f;
    private const float YawSpeed = 2f;
    private const float PitchSpeed = 1f;
    private const float ZoomSpeed = 10f;
    private const float MinDist = 3f;
    private const float MaxDist = 40f;
    private const float WalkDist = 7f;
    private const float RunDist = 12f;
    private const float DeployDistBase = 3f;
    private const float DeployDistPerUnit = 1.0f;
    private const float MinPitch = -1.4f;
    private const float MaxPitch = -0.1f;
    private const float Fov = MathHelper.PiOver4;
    private const float NearPlane = 0.1f;
    private const float FarPlane = 500f;

    public Matrix View { get; private set; } = Matrix.CreateLookAt(new Vector3(0, 5, 10), Vector3.Zero, Vector3.Up);
    public Matrix Projection { get; private set; } = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 16f / 9f, 0.1f, 500f);
    public Vector3 Position { get; private set; } = new Vector3(0, 5, 10);
    public float SmoothedYaw => _smoothedYaw;

    public void Initialize(Vector3 target)
    {
        _target = target;
        _initialized = false;
    }

    public void Update(float dt, float viewportAspect,
        Vector3 targetPos, float targetYaw, float targetSpeed,
        bool isRunning, bool isMovingBackward, float pokemonHeight,
        float inputYaw, float inputPitch, float inputZoom)
    {
        if (inputYaw != 0)
            _yawOffset += inputYaw * YawSpeed * dt;
        if (inputPitch != 0)
            _pitch = MathHelper.Clamp(_pitch + inputPitch * PitchSpeed * dt, MinPitch, MaxPitch);
        if (inputZoom != 0)
            _dist = MathHelper.Clamp(_dist + inputZoom * ZoomSpeed * dt, MinDist, MaxDist);

        if (!_initialized)
        {
            _target = targetPos;
            _smoothedYaw = targetYaw + MathHelper.Pi + _yawOffset;
            _smoothedDist = _dist;
            _targetVelocity = Vector3.Zero;
            _distVelocity = 0f;
            _yawVelocity = 0f;
            _initialized = true;
        }

        bool playerMoving = targetSpeed > 0.5f;
        bool playerMovingForward = playerMoving && !isMovingBackward;

        _target = SmoothDamp3(_target, targetPos, ref _targetVelocity, PositionSmoothTime, dt);

        if (playerMovingForward)
        {
            float desiredYaw = targetYaw + MathHelper.Pi + _yawOffset;
            float yawDiff = desiredYaw - _smoothedYaw;
            while (yawDiff > MathHelper.Pi) yawDiff -= MathHelper.TwoPi;
            while (yawDiff < -MathHelper.Pi) yawDiff += MathHelper.TwoPi;
            _smoothedYaw = SmoothDampAngle(_smoothedYaw, _smoothedYaw + yawDiff, ref _yawVelocity, YawSmoothTime, dt);
        }

        float runDistOffset = isRunning ? (RunDist - WalkDist) : 0f;
        float deployOffset = pokemonHeight > 0.1f
            ? DeployDistBase + Math.Min(pokemonHeight, 4f) * DeployDistPerUnit
            : 0f;
        float desiredDist = _dist + runDistOffset + deployOffset;
        desiredDist = MathHelper.Clamp(desiredDist, MinDist, MaxDist);
        _smoothedDist = SmoothDamp(_smoothedDist, desiredDist, ref _distVelocity, DistSmoothTime, dt);

        _yaw = _smoothedYaw;

        // Raise lookAt when pokemon is out to center the view between trainer and pokemon
        float lookAtHeight = 1.5f + (pokemonHeight > 0.1f ? Math.Min(pokemonHeight, 4f) * 0.3f : 0f);
        var lookAt = _target + Vector3.Up * lookAtHeight;

        var offset = new Vector3(
            (float)(_smoothedDist * Math.Cos(_pitch) * Math.Sin(_smoothedYaw)),
            (float)(_smoothedDist * -Math.Sin(_pitch)),
            (float)(_smoothedDist * Math.Cos(_pitch) * Math.Cos(_smoothedYaw)));

        Position = lookAt + offset;

        View = Matrix.CreateLookAt(Position, lookAt, Vector3.Up);
        Projection = Matrix.CreatePerspectiveFieldOfView(Fov, viewportAspect, NearPlane, FarPlane);
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
}
