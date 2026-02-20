using Microsoft.Xna.Framework;
using Starfield2026.Core.Input;

namespace Starfield2026.Core.Controllers;

public class ShipController
{
    public Vector3 Position;
    public float CurrentSpeed;
    public bool IsMoving => CurrentSpeed > 0.5f;
    public bool HasBoost { get; private set; }
    public Vector3 BobOffset { get; private set; }
    
    private float _targetSpeed;
    private float _speedLerpRate = 40f;
    private float _dodgeSpeed = 35f;
    private int _gear = 1;
    private readonly float[] _gearSpeeds = { 0f, 40f, 100f };
    private float _boostTimer;
    private float _boostMaxSpeed = 150f;
    private float _elapsed;
    
    public void Initialize(Vector3 startPosition)
    {
        Position = startPosition;
        _targetSpeed = _gearSpeeds[_gear];
        CurrentSpeed = _targetSpeed;
        HasBoost = false;
        _boostTimer = 0f;
    }
    
    public void ActivateBoost(float duration = 10f)
    {
        HasBoost = true;
        _boostTimer = duration;
    }
    
    public void Update(float dt, InputSnapshot input)
    {
        HandleBoost(dt);
        HandleGearShift(input);
        HandleSpeed(dt);
        HandleMovement(dt, input);
    }
    
    private void HandleBoost(float dt)
    {
        if (_boostTimer > 0)
        {
            _boostTimer -= dt;
            if (_boostTimer <= 0)
            {
                HasBoost = false;
                _boostTimer = 0;
            }
        }
    }
    
    private void HandleGearShift(InputSnapshot input)
    {
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.LeftAlt) ||
            input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.RightAlt))
        {
            _gear = Math.Min(_gear + 1, 2);
            _targetSpeed = _gearSpeeds[_gear];
        }
        if (input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.LeftControl) ||
            input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.RightControl))
        {
            _gear = Math.Max(_gear - 1, 0);
            _targetSpeed = _gearSpeeds[_gear];
        }
    }
    
    private void HandleSpeed(float dt)
    {
        float target = HasBoost ? _boostMaxSpeed : _targetSpeed;
        float speedDiff = target - CurrentSpeed;
        if (Math.Abs(speedDiff) > 0.5f)
        {
            CurrentSpeed += Math.Sign(speedDiff) * _speedLerpRate * dt;
        }
        else
        {
            CurrentSpeed = target;
        }
    }
    
    private void HandleMovement(float dt, InputSnapshot input)
    {
        Position.Z -= CurrentSpeed * dt;
        Position.X += input.MoveX * _dodgeSpeed * dt;
        Position.Y += input.MoveZ * _dodgeSpeed * dt * 0.6f;
        
        _elapsed += dt;
        if (!IsMoving)
        {
            float bobY = (float)Math.Sin(_elapsed * 1.8) * 0.08f;
            float bobX = (float)Math.Sin(_elapsed * 1.1) * 0.04f;
            BobOffset = new Vector3(bobX, bobY, 0);
        }
        else
        {
            BobOffset = Vector3.Zero;
        }
    }
    
    public void ClampToBounds(float maxX, float floorY, float ceilingY)
    {
        Position.X = MathHelper.Clamp(Position.X, -maxX, maxX);
        Position.Y = MathHelper.Clamp(Position.Y, floorY + 2.5f, ceilingY - 2.5f);
    }
}
