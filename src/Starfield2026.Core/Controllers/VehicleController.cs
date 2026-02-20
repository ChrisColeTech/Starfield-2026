using Microsoft.Xna.Framework;
using Starfield2026.Core.Input;

namespace Starfield2026.Core.Controllers;

public class VehicleController
{
    public Vector3 Position { get; private set; }
    public float Yaw { get; private set; }
    public float Speed { get; private set; }
    public bool IsMoving => Math.Abs(Speed) > 1f;
    public bool IsTurbo { get; private set; }
    public bool HasBoost { get; private set; }
    public Vector3 Forward => new((float)Math.Sin(Yaw), 0, (float)Math.Cos(Yaw));
    public Vector3 RumbleOffset { get; private set; }
    
    private float _maxSpeed = 100f;
    private float _boostMaxSpeed = 150f;
    private float _acceleration = 25f;
    private float _turnSpeed = 3f;
    private float _cruiseSpeed1x = 40f;
    private float _currentTurnInput;
    private float _boostTimer;
    private float _elapsed;
    private readonly Random _random = new();
    
    private bool _cruiseActive;
    private float _targetSpeed;
    private int _altPressCount;
    private int _ctrlPressCount;
    
    public void Initialize(Vector3 position)
    {
        Position = new Vector3(position.X, 1.5f, position.Z);
        Yaw = 0f;
        Speed = 0f;
        _targetSpeed = 0f;
        _cruiseActive = false;
        HasBoost = false;
        _boostTimer = 0f;
    }
    
    public void ActivateBoost(float duration = 5f)
    {
        HasBoost = true;
        _boostTimer = duration;
    }
    
    public void Update(float dt, InputSnapshot input)
    {
        HandleTurbo(input);
        HandleBoost(dt);
        HandleCruiseControl(input);
        HandleSpeed(dt, input);
        HandleSteering(input, dt);
        HandleMovement(dt);
        HandleRumble(dt);
    }
    
    private void HandleTurbo(InputSnapshot input)
    {
        IsTurbo = input.RunHeld;
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
    
    private void HandleCruiseControl(InputSnapshot input)
    {
        bool altPressed = input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.LeftAlt) ||
                          input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.RightAlt);
        bool ctrlPressed = input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.LeftControl) ||
                           input.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.RightControl);
        
        float maxSpeed = HasBoost ? _boostMaxSpeed : _maxSpeed;
        bool isSlow = Math.Abs(Speed) < _cruiseSpeed1x;
        
        if (altPressed)
        {
            if (!IsMoving)
            {
                _targetSpeed = _cruiseSpeed1x;
                _cruiseActive = true;
                _altPressCount = 1;
                _ctrlPressCount = 0;
            }
            else if (isSlow)
            {
                if (_altPressCount == 0)
                {
                    _targetSpeed = _cruiseSpeed1x;
                    _cruiseActive = true;
                    _altPressCount = 1;
                    _ctrlPressCount = 0;
                }
                else if (_altPressCount == 1)
                {
                    _targetSpeed = maxSpeed;
                    _altPressCount = 2;
                }
            }
            else
            {
                if (_altPressCount == 0)
                {
                    _targetSpeed = Speed;
                    _cruiseActive = true;
                    _altPressCount = 1;
                    _ctrlPressCount = 0;
                }
                else if (_altPressCount == 1)
                {
                    _targetSpeed = maxSpeed;
                    _altPressCount = 2;
                }
            }
        }
        
        if (ctrlPressed)
        {
            if (!IsMoving) { }
            else if (isSlow)
            {
                _targetSpeed = 0f;
                _cruiseActive = false;
                _altPressCount = 0;
                _ctrlPressCount = 0;
            }
            else
            {
                if (_ctrlPressCount == 0)
                {
                    _targetSpeed = Speed;
                    _cruiseActive = true;
                    _ctrlPressCount = 1;
                    _altPressCount = 0;
                }
                else if (_ctrlPressCount == 1)
                {
                    _targetSpeed = _cruiseSpeed1x;
                    _ctrlPressCount = 2;
                }
                else if (_ctrlPressCount == 2)
                {
                    _targetSpeed = 0f;
                    _cruiseActive = false;
                    _altPressCount = 0;
                    _ctrlPressCount = 0;
                }
            }
        }
        
        if (input.MoveZ != 0)
        {
            _cruiseActive = false;
            _altPressCount = 0;
            _ctrlPressCount = 0;
        }
    }
    
    private void HandleSpeed(float dt, InputSnapshot input)
    {
        if (HasBoost)
        {
            if (Speed < _boostMaxSpeed)
                Speed += _acceleration * 3f * dt;
            if (Speed > _boostMaxSpeed)
                Speed = _boostMaxSpeed;
            return;
        }
        
        float maxSpeed = _maxSpeed;
        
        if (_cruiseActive)
        {
            float diff = _targetSpeed - Speed;
            if (Math.Abs(diff) > 0.5f)
                Speed += Math.Sign(diff) * _acceleration * dt;
            else
                Speed = _targetSpeed;
            
            if (Speed < 0f) Speed = 0f;
        }
        else
        {
            if (input.MoveZ > 0)
            {
                if (Speed < maxSpeed)
                    Speed += _acceleration * dt;
            }
            else if (input.MoveZ < 0)
            {
                Speed -= _acceleration * 2f * dt;
                if (Speed < -20f) Speed = -20f;
            }
            else
            {
                if (Speed > 0.1f)
                    Speed -= _acceleration * 2f * dt;
                else if (Speed < -0.1f)
                    Speed += _acceleration * 2f * dt;
                else
                    Speed = 0f;
            }
        }
    }
    
    private void HandleSteering(InputSnapshot input, float dt)
    {
        _currentTurnInput = input.MoveX;
        
        if (input.MoveX != 0)
        {
            if (Math.Abs(Speed) > 0.5f)
            {
                float speedFactor = 1f - (Math.Abs(Speed) / _maxSpeed * 0.4f);
                if (speedFactor < 0.6f) speedFactor = 0.6f;
                Yaw -= input.MoveX * _turnSpeed * speedFactor * dt * Math.Sign(Speed);
            }
            else
            {
                Yaw -= input.MoveX * _turnSpeed * 0.5f * dt;
            }
        }
    }
    
    private void HandleMovement(float dt)
    {
        var right = new Vector3((float)Math.Cos(Yaw), 0, -(float)Math.Sin(Yaw));
        
        float lateralDrift = _currentTurnInput * Math.Abs(Speed) * 0.05f;
        
        Position += Forward * Speed * dt;
        Position += right * lateralDrift * dt;
        
        if (Position.Y < 1.5f)
            Position = new Vector3(Position.X, 1.5f, Position.Z);
    }
    
    private void HandleRumble(float dt)
    {
        _elapsed += dt;
        
        if (!IsMoving)
        {
            // Smooth hover bob only when idle
            float hoverY = (float)Math.Sin(_elapsed * 2.0) * 0.06f;
            float hoverX = (float)Math.Sin(_elapsed * 1.3) * 0.03f;
            RumbleOffset = new Vector3(hoverX, hoverY, 0);
        }
        else
        {
            RumbleOffset = Vector3.Zero;
        }
    }
}
