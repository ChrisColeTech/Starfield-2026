using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Starfield2026.Core.Input;
using Starfield2026.Core.Systems;

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
    public BoostSystem? Boosts { get; set; }
    public float HoverBobOffset { get; private set; }
    
    private float _walkSpeed = 12f;
    private float _runSpeed = 22f;
    private float _rotationSpeed = 3f;
    private float _worldHalfSize = 100f;
    private float _verticalVelocity;
    private float _gravity = 60f;
    private float _jumpForce = 32f;
    private bool _runningToggled;
    private float _hoverTime;
    private float _hoverDuration = 10f;
    private float _hoverBobTimer;
    private float _hoverRiseSpeed = 15f;
    private float _currentSpeed;
    private float _currentTurnSpeed;
    
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
    
    
    public void Update(float dt, InputSnapshot input, Starfield2026.Core.Maps.MapDefinition? map = null)
    {
        if (input.RunPressed)
            _runningToggled = !_runningToggled;
        IsRunning = _runningToggled;
        
        bool jumpTriggered = input.JumpPressed;
        
        bool wasGrounded = IsGrounded;
        
        if (wasGrounded)
        {
            if (jumpTriggered)
            {
                _verticalVelocity = _jumpForce;
                IsGrounded = false;
                IsHovering = false;
                _hoverTime = 0f;
            }
        }
        
        if (!IsGrounded)
        {
            // Only allow boost jumping if the player was already in the air before this frame began
            if (jumpTriggered && !wasGrounded)
            {
                if (!IsHovering && Boosts != null && Boosts.BoostCount > 0)
                {
                    IsHovering = true;
                    Boosts.UseBoost(1);
                    _hoverTime = _hoverDuration;
                    _verticalVelocity = 0f; // Perfect levitation on first tap
                }
                else if (IsHovering && Boosts != null && Boosts.BoostCount > 0)
                {
                    Boosts.UseBoost(1);
                    _hoverTime += _hoverDuration;
                    _verticalVelocity = _hoverRiseSpeed; // Rocket upwards on second tap
                }
            }
            
            if (IsHovering)
            {
                if (_verticalVelocity < 0)
                {
                    // Lock falling speed to zero for pure levitation
                    _verticalVelocity = 0f;
                }
                else if (_verticalVelocity > 0)
                {
                    // Gravity gently tugs at the upward boost so they don't shoot to space forever
                    _verticalVelocity -= _gravity * 2f * dt;
                    if (_verticalVelocity < 0) _verticalVelocity = 0f;
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
        }
        
        float groundHeight = 0.825f;
        if (map != null)
        {
            int px = (int)Math.Floor(Position.X / 2f + map.Width / 2f);
            int pz = (int)Math.Floor(Position.Z / 2f + map.Height / 2f);
            if (px >= 0 && px < map.Width && pz >= 0 && pz < map.Height)
            {
                groundHeight += map.GetTileHeight(px, pz);
            }
        }

        if (!IsGrounded && Position.Y <= groundHeight && _verticalVelocity <= 0)
        {
            Position = new Vector3(Position.X, groundHeight, Position.Z);
            _verticalVelocity = 0f;
            IsGrounded = true;
            IsHovering = false;
            _hoverTime = 0f;
            HoverBobOffset = 0f;
        }
        
        float moveX = input.MoveX;
        float moveZ = input.MoveZ;
        
        IsMoving = (Math.Abs(_currentSpeed) > 0.1f) || (moveZ != 0);
        
        // Rotational momentum: smoothly interpolate towards target rotation speed using stable exp decay
        float targetTurnSpeed = moveX != 0 ? -moveX * _rotationSpeed : 0f;
        float turnBlend = 1f - (float)Math.Exp(-10f * dt);
        _currentTurnSpeed = MathHelper.Lerp(_currentTurnSpeed, targetTurnSpeed, turnBlend);
        Yaw += _currentTurnSpeed * dt;
        
        float targetSpeed = 0f;
        if (moveZ != 0)
        {
            targetSpeed = (IsRunning ? _runSpeed : _walkSpeed) * Math.Sign(moveZ);
        }
        
        // Acceleration / Deceleration
        float accelRate = moveZ != 0 ? 8f : 12f;
        float speedBlend = 1f - (float)Math.Exp(-accelRate * dt);
        _currentSpeed = MathHelper.Lerp(_currentSpeed, targetSpeed, speedBlend);
        Speed = Math.Abs(_currentSpeed);
        
        if (Math.Abs(_currentSpeed) > 0.01f)
        {
            var forward = new Vector3(
                (float)Math.Sin(Yaw),
                0f,
                (float)Math.Cos(Yaw));
            
            var newPos = Position + forward * _currentSpeed * dt;
            newPos.X = MathHelper.Clamp(newPos.X, -_worldHalfSize, _worldHalfSize);
            newPos.Z = MathHelper.Clamp(newPos.Z, -_worldHalfSize, _worldHalfSize);
            
            if (map != null)
            {
                // Test X axis independently
                int nxX = (int)Math.Floor(newPos.X / 2f + map.Width / 2f);
                int nzOrig = (int)Math.Floor(Position.Z / 2f + map.Height / 2f);
                if (nxX >= 0 && nxX < map.Width && nzOrig >= 0 && nzOrig < map.Height)
                {
                    float targetGroundX = 0.825f + map.GetTileHeight(nxX, nzOrig);
                    bool hasWarpX = map.GetWarp(nxX, nzOrig, Starfield2026.Core.Maps.WarpTrigger.Step) != null || 
                                    map.GetWarp(nxX, nzOrig, Starfield2026.Core.Maps.WarpTrigger.Interact) != null;
                                    
                    if (!hasWarpX && targetGroundX > Position.Y + 0.5f)
                    {
                        newPos.X = Position.X;
                    }
                }
                
                // Test Z axis independently
                int nxOrig = (int)Math.Floor(Position.X / 2f + map.Width / 2f);
                int nzZ = (int)Math.Floor(newPos.Z / 2f + map.Height / 2f);
                if (nxOrig >= 0 && nxOrig < map.Width && nzZ >= 0 && nzZ < map.Height)
                {
                    float targetGroundZ = 0.825f + map.GetTileHeight(nxOrig, nzZ);
                    bool hasWarpZ = map.GetWarp(nxOrig, nzZ, Starfield2026.Core.Maps.WarpTrigger.Step) != null || 
                                    map.GetWarp(nxOrig, nzZ, Starfield2026.Core.Maps.WarpTrigger.Interact) != null;
                                    
                    if (!hasWarpZ && targetGroundZ > Position.Y + 0.5f)
                    {
                        newPos.Z = Position.Z;
                    }
                }
            }
            
            if (IsGrounded)
            {
                float finalGround = 0.825f;
                if (map != null)
                {
                    int endX = (int)Math.Floor(newPos.X / 2f + map.Width / 2f);
                    int endZ = (int)Math.Floor(newPos.Z / 2f + map.Height / 2f);
                    if (endX >= 0 && endX < map.Width && endZ >= 0 && endZ < map.Height)
                    {
                        finalGround += map.GetTileHeight(endX, endZ);
                    }
                }
                newPos.Y = finalGround;
            }
            
            Position = newPos;
        }
    }
}
