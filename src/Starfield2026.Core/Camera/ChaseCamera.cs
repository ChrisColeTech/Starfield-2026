using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Camera;

public class ChaseCamera
{
    public float Distance { get; set; } = 18f;
    public float Height { get; set; } = 5f;
    public float LookAheadDistance { get; set; } = 20f;
    public float BaseFov { get; set; } = MathHelper.PiOver4;
    public float FollowSpeed { get; set; } = 12f;
    
    public Vector3 Position { get; private set; }
    public Matrix View { get; private set; }
    public Matrix Projection { get; private set; }
    
    private readonly float _nearPlane;
    private readonly float _farPlane;
    private float _currentYaw;
    private Vector3 _currentPosition;
    private bool _initialized;
    
    public ChaseCamera(float fov = MathHelper.PiOver4, float nearPlane = 0.5f, float farPlane = 1000f)
    {
        BaseFov = fov;
        _nearPlane = nearPlane;
        _farPlane = farPlane;
        _currentYaw = 0f;
        _currentPosition = Vector3.Zero;
        _initialized = false;
    }
    
    public void Update(Vector3 targetPosition, float targetYaw, float aspectRatio, float speedFactor = 0f, float dt = 0.016f)
    {
        if (!_initialized)
        {
            _currentPosition = targetPosition;
            _currentYaw = targetYaw;
            _initialized = true;
        }
        
        _currentPosition = Vector3.Lerp(_currentPosition, targetPosition, FollowSpeed * dt);
        
        float yawDiff = targetYaw - _currentYaw;
        while (yawDiff > MathHelper.Pi) yawDiff -= MathHelper.TwoPi;
        while (yawDiff < -MathHelper.Pi) yawDiff += MathHelper.TwoPi;
        _currentYaw += yawDiff * FollowSpeed * dt;
        
        var offset = new Vector3(
            -(float)Math.Sin(_currentYaw) * Distance,
            Height,
            -(float)Math.Cos(_currentYaw) * Distance);
        
        Position = _currentPosition + offset;
        
        var lookAt = _currentPosition + new Vector3(
            (float)Math.Sin(_currentYaw) * LookAheadDistance,
            1f,
            (float)Math.Cos(_currentYaw) * LookAheadDistance);
        
        View = Matrix.CreateLookAt(Position, lookAt, Vector3.Up);
        
        float fov = BaseFov + speedFactor * 0.15f;
        Projection = Matrix.CreatePerspectiveFieldOfView(fov, aspectRatio, _nearPlane, _farPlane);
    }
}
