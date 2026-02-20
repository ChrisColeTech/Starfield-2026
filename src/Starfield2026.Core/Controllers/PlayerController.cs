using Microsoft.Xna.Framework;
using Starfield2026.Core.Input;

namespace Starfield2026.Core.Controllers;

public class PlayerController
{
    public Vector3 Position { get; private set; }
    public float Yaw { get; private set; }
    public float Speed { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGrounded { get; private set; } = true;
    
    private float _moveSpeed = 8f;
    private float _runSpeed = 14f;
    private float _rotationSpeed = 3f;
    private float _worldHalfSize = 100f;
    private float _verticalVelocity;
    private float _gravity = 45f;
    private float _jumpForce = 18f;
    
    public float WorldHalfSize
    {
        get => _worldHalfSize;
        set => _worldHalfSize = value;
    }
    
    public void Initialize(Vector3 position, float yaw = 0f)
    {
        Position = position;
        Yaw = yaw;
        IsGrounded = true;
        IsMoving = false;
        IsRunning = false;
    }
    
    public void SetPosition(Vector3 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
        IsMoving = false;
    }
    
    public void Update(float dt, InputSnapshot input)
    {
        HandleJump(input);
        HandleGravity(dt);
        HandleRotation(input, dt);
        HandleMovement(input, dt);
    }
    
    private void HandleJump(InputSnapshot input)
    {
        if (input.JumpPressed && IsGrounded)
        {
            _verticalVelocity = _jumpForce;
            IsGrounded = false;
        }
    }
    
    private void HandleGravity(float dt)
    {
        if (!IsGrounded)
        {
            _verticalVelocity -= _gravity * dt;
            Position = new Vector3(Position.X, Position.Y + _verticalVelocity * dt, Position.Z);
            
            if (Position.Y <= 0.75f)
            {
                Position = new Vector3(Position.X, 0.75f, Position.Z);
                _verticalVelocity = 0f;
                IsGrounded = true;
            }
        }
    }
    
    private void HandleRotation(InputSnapshot input, float dt)
    {
        if (input.MoveX != 0)
        {
            Yaw -= input.MoveX * _rotationSpeed * dt;
        }
    }
    
    private void HandleMovement(InputSnapshot input, float dt)
    {
        IsMoving = false;
        Speed = 0f;
        IsRunning = input.RunHeld;
        
        if (input.MoveZ != 0)
        {
            var forward = new Vector3(
                (float)Math.Sin(Yaw),
                0f,
                (float)Math.Cos(Yaw));
            
            float speed = IsRunning ? _runSpeed : _moveSpeed;
            Speed = speed * input.MoveZ;
            
            var newPos = Position + forward * input.MoveZ * speed * dt;
            newPos.X = MathHelper.Clamp(newPos.X, -_worldHalfSize, _worldHalfSize);
            newPos.Z = MathHelper.Clamp(newPos.Z, -_worldHalfSize, _worldHalfSize);
            
            if (IsGrounded)
                newPos.Y = 0.75f;
            
            Position = newPos;
            IsMoving = true;
        }
    }
}
