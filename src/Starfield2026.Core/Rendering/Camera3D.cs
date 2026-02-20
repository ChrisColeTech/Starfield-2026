using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Rendering;

/// <summary>
/// Third-person camera with orbit controls and follow behavior.
/// </summary>
public class Camera3D
{
    public float Yaw { get; set; }
    public float Pitch { get; set; } = -0.4f;
    public float Distance { get; set; } = 15f;
    public float MinDistance { get; set; } = 5f;
    public float MaxDistance { get; set; } = 50f;
    public float MinPitch { get; set; } = -1.4f;
    public float MaxPitch { get; set; } = -0.1f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 500f;
    public float FieldOfView { get; set; } = MathHelper.PiOver4;

    public Matrix ViewMatrix { get; private set; } = Matrix.Identity;
    public Matrix ProjectionMatrix { get; private set; } = Matrix.Identity;
    public Vector3 Position { get; private set; }

    private Vector3 _followTarget;
    private float _targetHeight;
    private float _smoothSpeed = 8f;

    /// <summary>How fast the camera follows the target (higher = snappier).</summary>
    public float SmoothSpeed { get => _smoothSpeed; set => _smoothSpeed = value; }

    /// <summary>
    /// Rotate the camera by delta yaw and pitch, clamping pitch.
    /// </summary>
    public void Rotate(float yawDelta, float pitchDelta)
    {
        Yaw += yawDelta;
        Pitch = MathHelper.Clamp(Pitch + pitchDelta, MinPitch, MaxPitch);
    }

    /// <summary>
    /// Zoom the camera by adjusting distance, clamped to min/max.
    /// </summary>
    public void Zoom(float delta)
    {
        Distance = MathHelper.Clamp(Distance + delta, MinDistance, MaxDistance);
    }

    /// <summary>
    /// Set the follow target position and height offset.
    /// </summary>
    public void Follow(Vector3 target, float targetHeight = 2f)
    {
        _followTarget = target;
        _targetHeight = targetHeight;
        
        // Initialize current target on first call to avoid snap
        if (_currentTarget == Vector3.Zero)
            _currentTarget = target;
    }

    /// <summary>
    /// Instantly snap the camera to the current follow target (no smooth lerp).
    /// Call this after teleporting the player to prevent camera spinning.
    /// </summary>
    public void SnapToTarget()
    {
        _currentTarget = _followTarget;
    }

    /// <summary>
    /// Recalculate view and projection matrices.
    /// </summary>
    public void Update(float aspectRatio, float deltaTime = 0f)
    {
        // Smooth follow — lerp toward target
        if (deltaTime > 0)
        {
            float t = 1f - (float)System.Math.Exp(-_smoothSpeed * deltaTime);
            _currentTarget = Vector3.Lerp(_currentTarget, _followTarget, t);
        }
        else
        {
            _currentTarget = _followTarget;
        }

        var lookAtPoint = _currentTarget + Vector3.Up * _targetHeight;

        // Compute camera position from spherical coordinates around target
        var offset = new Vector3(
            (float)(Distance * System.Math.Cos(Pitch) * System.Math.Sin(Yaw)),
            (float)(Distance * -System.Math.Sin(Pitch)),
            (float)(Distance * System.Math.Cos(Pitch) * System.Math.Cos(Yaw))
        );

        Position = lookAtPoint + offset;
        ViewMatrix = Matrix.CreateLookAt(Position, lookAtPoint, Vector3.Up);
        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            FieldOfView, aspectRatio, NearPlane, FarPlane);
    }

    private Vector3 _currentTarget;

    /// <summary>
    /// Get the forward direction vector on the XZ plane (for movement relative to camera).
    /// </summary>
    public Vector3 GetForwardXZ()
    {
        var forward = new Vector3(
            (float)System.Math.Sin(Yaw),
            0,
            (float)System.Math.Cos(Yaw)
        );
        forward.Normalize();
        return forward;
    }

    /// <summary>
    /// Get the right direction vector on the XZ plane.
    /// </summary>
    public Vector3 GetRightXZ()
    {
        var right = new Vector3(
            (float)System.Math.Cos(Yaw),
            0,
            -(float)System.Math.Sin(Yaw)
        );
        right.Normalize();
        return right;
    }
}
