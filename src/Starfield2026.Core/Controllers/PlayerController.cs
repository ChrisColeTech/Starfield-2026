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
        if (input.RunPressed && IsGrounded)
            _runningToggled = !_runningToggled;
        IsRunning = _runningToggled;
        
        bool jumpHeld = input.JumpHeld;
        bool jumpTriggered = input.JumpPressed;
        
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
        
        if (!IsGrounded)
        {
            if (jumpTriggered)
            {
                if (!IsHovering && Boosts != null && Boosts.BoostCount > 0)
                {
                    IsHovering = true;
                    Boosts.UseBoost(1);
                    _hoverTime = _hoverDuration;
                    _verticalVelocity = _hoverRiseSpeed;
                }
                else if (IsHovering)
                {
                    if (Boosts != null && Boosts.BoostCount > 0)
                    {
                        Boosts.UseBoost(1);
                        _hoverTime += _hoverDuration;
                        // Accumulate velocity rather than hard-setting, giving a satisfying "double jump" feel
                        _verticalVelocity = Math.Max(_verticalVelocity, 0) + _hoverRiseSpeed;
                    }
                }
            }
            
            if (IsHovering)
            {
                // Only allow descent if we aren't currently shooting upwards from a fresh boost
                if (jumpHeld && !jumpTriggered && _verticalVelocity <= 0)
                {
                    _verticalVelocity = -_hoverDescentSpeed;
                }
                else if (_verticalVelocity < 0 && !jumpHeld)
                {
                    _verticalVelocity = 0f;
                }
                else if (_verticalVelocity > 0)
                {
                    // Apply heavy gravity to the upward boost burst so it feels punchy and short
                    _verticalVelocity -= _gravity * 1.5f * dt;
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
            
            if (map != null)
            {
                int nx = (int)Math.Floor(newPos.X / 2f + map.Width / 2f);
                int nz = (int)Math.Floor(newPos.Z / 2f + map.Height / 2f);
                if (nx >= 0 && nx < map.Width && nz >= 0 && nz < map.Height)
                {
                    float targetGround = 0.825f + map.GetTileHeight(nx, nz);
                    bool hasWarp = map.GetWarp(nx, nz, Starfield2026.Core.Maps.WarpTrigger.Step) != null || 
                                   map.GetWarp(nx, nz, Starfield2026.Core.Maps.WarpTrigger.Interact) != null;
                                   
                    if (!hasWarp && targetGround > Position.Y + 0.5f)
                    {
                        // Wall collision: prevent XZ movement
                        newPos.X = Position.X;
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
