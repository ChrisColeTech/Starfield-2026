using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Camera;

public class FollowCamera
{
    public float Distance { get; set; } = 25f;
    public float Height { get; set; } = 15f;
    public float DeadZoneX { get; set; } = 5f;
    public float DeadZoneY { get; set; } = 3f;
    public float FollowSpeed { get; set; } = 4f;
    public float BaseFov { get; set; } = MathHelper.PiOver4;
    
    public Vector3 Position { get; private set; }
    public Matrix View { get; private set; }
    public Matrix Projection { get; private set; }
    
    private readonly float _nearPlane;
    private readonly float _farPlane;
    
    private Vector2 _offset;
    
    public FollowCamera(float fov = MathHelper.PiOver4, float nearPlane = 0.5f, float farPlane = 1000f)
    {
        BaseFov = fov;
        _nearPlane = nearPlane;
        _farPlane = farPlane;
    }
    
    public void Reset(Vector3 targetPosition)
    {
        _offset = new Vector2(targetPosition.X, targetPosition.Y);
    }
    
    public void Update(Vector3 targetPosition, float aspectRatio, float dt, float speedFactor = 0f)
    {
        float relX = targetPosition.X - _offset.X;
        float relY = targetPosition.Y - _offset.Y;
        
        if (Math.Abs(relX) > DeadZoneX)
        {
            float excess = relX - Math.Sign(relX) * DeadZoneX;
            _offset.X += excess * FollowSpeed * dt;
        }
        
        if (Math.Abs(relY) > DeadZoneY)
        {
            float excess = relY - Math.Sign(relY) * DeadZoneY;
            _offset.Y += excess * FollowSpeed * dt;
        }
        
        Position = new Vector3(_offset.X, _offset.Y + Height, targetPosition.Z - Distance);
        
        var lookAt = new Vector3(_offset.X, _offset.Y, targetPosition.Z + 20f);
        View = Matrix.CreateLookAt(Position, lookAt, Vector3.Up);
        
        float fov = BaseFov + speedFactor * 0.15f;
        Projection = Matrix.CreatePerspectiveFieldOfView(fov, aspectRatio, _nearPlane, _farPlane);
    }
}
