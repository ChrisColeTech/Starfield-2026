using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
    public bool IsHovering { get; private set; }
    public int BoostCount { get; private set; }
    public float HoverBobOffset { get; private set; }
    
    private float _walkSpeed = 12f;
    private float _runSpeed = 22f;
    private float _rotationSpeed = 3f;
    private float _worldHalfSize = 100f;
    private float _verticalVelocity;
    private float _gravity = 60f;
    private float _jumpForce = 32f;
    private bool _runningToggled;
    private bool _wasJumpHeld;
    private float _hoverTime;
    private float _hoverDuration = 10f;
    private float _hoverBobTimer;
    private float _hoverDescentSpeed = 8f;
    private float _hoverRiseSpeed = 15f;
    
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
        _runningToggled = false;
        IsHovering = false;
        BoostCount = 0;
    }
    
    public void SetPosition(Vector3 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
        IsMoving = false;
        _verticalVelocity = 0f;
        IsGrounded = true;
        IsHovering = false;
        _hoverTime = 0f;
    }
    
    public void SetBoostCount(int count)
    {
        BoostCount = count;
    }
    
    public int GetAndResetBoostsUsed()
    {
        return 0;
    }
    
    public void Update(float dt, InputSnapshot input)
    {
        if (input.RunPressed && IsGrounded)
            _runningToggled = !_runningToggled;
        IsRunning = _runningToggled;
        
        var kb = Keyboard.GetState();
        bool jumpHeld = kb.IsKeyDown(Keys.Space) || kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        bool jumpTriggered = jumpHeld && !_wasJumpHeld;
        _wasJumpHeld = jumpHeld;
        
        if (IsGrounded)
        {
            if (jumpTriggered)
            {
                _verticalVelocity = _jumpForce;
                IsGrounded = false;
                IsHovering = false;
                _hoverTime = 0f;
            }
        }
        else
        {
            if (jumpTriggered)
            {
                if (!IsHovering && BoostCount > 0)
                {
                    IsHovering = true;
                    BoostCount--;
                    _hoverTime = _hoverDuration;
                    _verticalVelocity = 0f;
                }
                else if (IsHovering)
                {
                    if (BoostCount > 0)
                    {
                        BoostCount--;
                        _hoverTime += _hoverDuration;
                        _verticalVelocity = _hoverRiseSpeed;
                    }
                    else
                    {
                        _verticalVelocity = -_hoverDescentSpeed;
                    }
                }
            }
            
            if (IsHovering)
            {
                if (jumpHeld)
                {
                    _verticalVelocity = -_hoverDescentSpeed;
                }
                else if (_verticalVelocity < 0)
                {
                    _verticalVelocity = 0f;
                }
                
                _hoverTime -= dt;
                if (_hoverTime <= 0f)
                {
                    IsHovering = false;
                    _hoverTime = 0f;
                }
                
                _hoverBobTimer += dt * 3f;
                HoverBobOffset = (float)Math.Sin(_hoverBobTimer) * 0.15f;
            }
            else
            {
                HoverBobOffset = 0f;
                _verticalVelocity -= _gravity * dt;
            }
            
            Position = new Vector3(Position.X, Position.Y + _verticalVelocity * dt, Position.Z);
            
            if (Position.Y <= 1.5f)
            {
                Position = new Vector3(Position.X, 1.5f, Position.Z);
                _verticalVelocity = 0f;
                IsGrounded = true;
                IsHovering = false;
                _hoverTime = 0f;
                HoverBobOffset = 0f;
            }
        }
        
        float moveX = input.MoveX;
        float moveZ = input.MoveZ;
        
        if (moveX != 0)
            Yaw -= moveX * _rotationSpeed * dt;
        
        IsMoving = false;
        Speed = 0f;
        
        if (moveZ != 0)
        {
            float speed = IsRunning ? _runSpeed : _walkSpeed;
            Speed = speed;
            IsMoving = true;
            
            var forward = new Vector3(
                (float)Math.Sin(Yaw),
                0f,
                (float)Math.Cos(Yaw));
            
            var newPos = Position + forward * moveZ * speed * dt;
            newPos.X = MathHelper.Clamp(newPos.X, -_worldHalfSize, _worldHalfSize);
            newPos.Z = MathHelper.Clamp(newPos.Z, -_worldHalfSize, _worldHalfSize);
            
            if (IsGrounded)
                newPos.Y = 1.5f;
            
            Position = newPos;
        }
    }
}
