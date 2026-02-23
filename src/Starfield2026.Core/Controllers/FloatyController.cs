using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Starfield2026.Core.Input;
using Starfield2026.Core.Systems;

namespace Starfield2026.Core.Controllers;

public class FloatyController
{
    public Vector3 Position { get; private set; }
    public float Yaw { get; private set; }
    public float Speed { get; private set; }
    public bool IsMoving => Math.Abs(Speed) > 1f;
    public bool HasBoost => Boosts?.IsActive ?? false;
    public BoostSystem? Boosts { get; set; }
    public Vector3 Forward => new((float)Math.Sin(Yaw), 0, (float)Math.Cos(Yaw));
    public Vector3 RumbleOffset { get; private set; }

    private float _maxSpeed = 30f;
    private float _boostMaxSpeed = 60f;
    private float _acceleration = 8f;
    private float _turnSpeed = 2f;
    private float _currentTurnInput;
    private float _verticalVelocity;
    private float _elapsed;

    private const float Gravity = 18f;
    private const float LevitateForce = 40f;
    private const float DescendForce = 45f;
    private const float GroundY = 4f;

    // Thrust fuel — depletes while levitating, recharges when not
    private const float MaxFuel = 100f;
    private const float FuelDrainRate = 25f;   // per second while Alt held
    private const float FuelRechargeRate = 15f; // per second while not levitating
    private const float FuelCooldownThreshold = 20f; // must recharge to this before re-use
    private float _fuel = MaxFuel;
    private bool _fuelOverheated;

    public float FuelPercent => _fuel / MaxFuel;
    public bool FuelOverheated => _fuelOverheated;

    public void Initialize(Vector3 position)
    {
        Position = new Vector3(position.X, Math.Max(GroundY, position.Y), position.Z);
        Yaw = 0f;
        Speed = 0f;
        _verticalVelocity = 0f;
    }

    public void SetPosition(Vector3 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
        Speed = 0f;
        _verticalVelocity = 0f;
    }

    public void ActivateBoost()
    {
        Boosts?.ActivateBoost();
    }

    public void Update(float dt, InputSnapshot input)
    {
        HandleSpeed(dt, input);
        HandleSteering(input, dt);
        HandleVertical(dt, input);
        HandleMovement(dt);
        HandleRumble(dt);
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

        if (input.MoveZ > 0)
        {
            if (Speed < _maxSpeed)
                Speed += _acceleration * dt;
        }
        else if (input.MoveZ < 0)
        {
            Speed -= _acceleration * 1.5f * dt;
            if (Speed < -10f) Speed = -10f;
        }
        else
        {
            // Slow drift-down — floaty deceleration
            if (Speed > 0.1f)
                Speed -= _acceleration * 0.5f * dt;
            else if (Speed < -0.1f)
                Speed += _acceleration * 0.5f * dt;
            else
                Speed = 0f;
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

    private void HandleVertical(float dt, InputSnapshot input)
    {
        // Alt = levitate up
        bool altHeld = input.IsKeyHeld(Keys.LeftAlt) || input.IsKeyHeld(Keys.RightAlt);
        // Ctrl = descend
        bool ctrlHeld = input.IsKeyHeld(Keys.LeftControl) || input.IsKeyHeld(Keys.RightControl);

        // Fuel system — Alt drains fuel, releasing recharges
        bool canThrust = altHeld && !_fuelOverheated && _fuel > 0;

        if (canThrust)
        {
            _fuel -= FuelDrainRate * dt;
            if (_fuel <= 0)
            {
                _fuel = 0;
                _fuelOverheated = true;
            }
            _verticalVelocity += LevitateForce * dt;
        }
        else
        {
            // Recharge fuel when not thrusting
            _fuel += FuelRechargeRate * dt;
            if (_fuel >= MaxFuel) _fuel = MaxFuel;
            if (_fuelOverheated && _fuel >= FuelCooldownThreshold)
                _fuelOverheated = false;
        }

        if (ctrlHeld)
            _verticalVelocity -= DescendForce * dt;

        // Boost also provides upward thrust (unaffected by fuel)
        if (HasBoost)
            _verticalVelocity += LevitateForce * 0.5f * dt;

        // Gravity always pulls down
        _verticalVelocity -= Gravity * dt;

        // Apply vertical movement
        float newY = Position.Y + _verticalVelocity * dt;

        // Ground collision — soft bounce
        if (newY <= GroundY)
        {
            newY = GroundY;
            _verticalVelocity = Math.Abs(_verticalVelocity) * 0.3f;
        }

        Position = new Vector3(Position.X, newY, Position.Z);
    }

    private void HandleMovement(float dt)
    {
        var right = new Vector3((float)Math.Cos(Yaw), 0, -(float)Math.Sin(Yaw));
        float lateralDrift = _currentTurnInput * Math.Abs(Speed) * 0.05f;

        Position += Forward * Speed * dt;
        Position += right * lateralDrift * dt;
    }

    private void HandleRumble(float dt)
    {
        _elapsed += dt;

        if (!IsMoving && Math.Abs(_verticalVelocity) < 1f)
        {
            float hoverY = (float)Math.Sin(_elapsed * 1.2) * 0.25f;
            float hoverX = (float)Math.Sin(_elapsed * 0.7) * 0.12f;
            float hoverZ = (float)Math.Sin(_elapsed * 0.9) * 0.08f;
            RumbleOffset = new Vector3(hoverX, hoverY, hoverZ);
        }
        else
        {
            RumbleOffset = Vector3.Zero;
        }
    }
}
