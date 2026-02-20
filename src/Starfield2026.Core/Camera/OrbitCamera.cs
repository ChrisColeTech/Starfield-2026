using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Camera;

public class OrbitCamera
{
    public float Distance { get; set; } = 15f;
    public float MinDistance { get; set; } = 5f;
    public float MaxDistance { get; set; } = 50f;
    public float Pitch { get; set; } = -0.3f;
    public float Yaw { get; set; }
    public float YawSpeed { get; set; } = 2f;
    public float PitchSpeed { get; set; } = 1f;
    public float ZoomSpeed { get; set; } = 10f;
    public float FollowSpeed { get; set; } = 4f;
    
    public Vector3 Position { get; private set; }
    public Matrix View { get; private set; }
    public Matrix Projection { get; private set; }
    
    private Vector3 _target;
    private float _currentYaw;
    private float _yawOffset;
    private bool _initialized;
    
    public OrbitCamera()
    {
        _target = Vector3.Zero;
        _currentYaw = 0f;
        _yawOffset = 0f;
        _initialized = false;
    }
    
    public void Rotate(float yawDelta, float pitchDelta)
    {
        _yawOffset += yawDelta;
        Pitch = MathHelper.Clamp(Pitch + pitchDelta, -1.2f, -0.15f);
    }
    
    public void Zoom(float delta)
    {
        Distance = MathHelper.Clamp(Distance + delta, MinDistance, MaxDistance);
    }
    
    public void SnapToTarget(Vector3 targetPosition)
    {
        _target = targetPosition;
    }
    
    public void Update(Vector3 targetPosition, float aspectRatio, float dt, float playerYaw = 0f)
    {
        if (!_initialized)
        {
            _target = targetPosition;
            _currentYaw = playerYaw + MathHelper.Pi + _yawOffset;
            _initialized = true;
        }
        
        float blend = 1f - (float)Math.Exp(-FollowSpeed * dt);
        _target = Vector3.Lerp(_target, targetPosition, blend);
        
        float targetYaw = playerYaw + MathHelper.Pi + _yawOffset;
        float yawDiff = targetYaw - _currentYaw;
        while (yawDiff > MathHelper.Pi) yawDiff -= MathHelper.TwoPi;
        while (yawDiff < -MathHelper.Pi) yawDiff += MathHelper.TwoPi;
        _currentYaw += yawDiff * FollowSpeed * dt;
        
        Yaw = _currentYaw;
        
        Vector3 lookAt = _target + Vector3.Up * 1.5f;
        
        float horizontalDist = Distance * (float)Math.Cos(Pitch);
        float verticalDist = Distance * (float)-Math.Sin(Pitch);
        
        Position = new Vector3(
            lookAt.X + horizontalDist * (float)Math.Sin(_currentYaw),
            lookAt.Y + verticalDist,
            lookAt.Z + horizontalDist * (float)Math.Cos(_currentYaw));
        
        View = Matrix.CreateLookAt(Position, lookAt, Vector3.Up);
        Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspectRatio, 0.1f, 500f);
    }
}
