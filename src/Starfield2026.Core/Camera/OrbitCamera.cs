using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Camera;

public class OrbitCamera
{
    public float Distance { get; set; } = 15f;
    public float MinDistance { get; set; } = 5f;
    public float MaxDistance { get; set; } = 50f;
    public float MinPitch { get; set; } = -1.4f;
    public float MaxPitch { get; set; } = -0.1f;
    public float YawSpeed { get; set; } = 2f;
    public float PitchSpeed { get; set; } = 1f;
    public float ZoomSpeed { get; set; } = 10f;
    public float FollowSpeed { get; set; } = 3f;

    public float Yaw { get; set; }
    public float Pitch { get; set; } = -0.4f;

    public Vector3 Position { get; private set; }
    public Matrix View { get; private set; }
    public Matrix Projection { get; private set; }

    private readonly float _fov;
    private readonly float _nearPlane;
    private readonly float _farPlane;
    private Vector3 _currentTarget;
    private float _currentYaw;
    private float _yawOffset;
    private bool _initialized;

    public OrbitCamera(float fov = MathHelper.PiOver4, float nearPlane = 0.1f, float farPlane = 500f)
    {
        _fov = fov;
        _nearPlane = nearPlane;
        _farPlane = farPlane;
        _currentYaw = 0f;
        _yawOffset = 0f;
        _initialized = false;
    }

    public void Rotate(float yawDelta, float pitchDelta)
    {
        _yawOffset += yawDelta;
        Pitch = MathHelper.Clamp(Pitch + pitchDelta, MinPitch, MaxPitch);
    }

    public void Zoom(float delta)
    {
        Distance = MathHelper.Clamp(Distance + delta, MinDistance, MaxDistance);
    }

    public void SnapToTarget(Vector3 targetPosition)
    {
        _currentTarget = targetPosition;
    }

    public void Update(Vector3 targetPosition, float aspectRatio, float dt, float targetPlayerYaw = 0f)
    {
        if (!_initialized)
        {
            _currentTarget = targetPosition;
            _currentYaw = targetPlayerYaw + MathHelper.Pi + _yawOffset;
            _initialized = true;
        }

        float t = 1f - (float)Math.Exp(-FollowSpeed * dt);
        _currentTarget = Vector3.Lerp(_currentTarget, targetPosition, t);

        float targetYaw = targetPlayerYaw + MathHelper.Pi + _yawOffset;
        float yawDiff = targetYaw - _currentYaw;
        while (yawDiff > MathHelper.Pi) yawDiff -= MathHelper.TwoPi;
        while (yawDiff < -MathHelper.Pi) yawDiff += MathHelper.TwoPi;
        _currentYaw += yawDiff * FollowSpeed * dt;

        Yaw = _currentYaw;

        var lookAt = _currentTarget + Vector3.Up * 2f;

        var offset = new Vector3(
            (float)(Distance * Math.Cos(Pitch) * Math.Sin(_currentYaw)),
            (float)(Distance * -Math.Sin(Pitch)),
            (float)(Distance * Math.Cos(Pitch) * Math.Cos(_currentYaw)));

        Position = lookAt + offset;
        View = Matrix.CreateLookAt(Position, lookAt, Vector3.Up);
        Projection = Matrix.CreatePerspectiveFieldOfView(_fov, aspectRatio, _nearPlane, _farPlane);
    }
}
