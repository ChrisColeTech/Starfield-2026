using System;
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.Input;

namespace Starfield2026.ModelLoader.Controllers;

public class PlayerController
{
    public Vector3 Position { get; private set; }
    public float Yaw { get; private set; }
    public float Speed { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGrounded { get; private set; } = true;

    private float _walkSpeed = 12f;
    private float _runSpeed = 22f;
    private float _rotationSpeed = 3f;
    private float _worldHalfSize = 500f;
    private float _verticalVelocity;
    private float _gravity = 60f;
    private float _jumpForce = 32f;
    private bool _runningToggled;
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
    }

    public void SetPosition(Vector3 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
        IsMoving = false;
        _verticalVelocity = 0f;
        IsGrounded = true;
    }

    public void Update(float dt, InputSnapshot input)
    {
        if (input.RunPressed)
            _runningToggled = !_runningToggled;
        IsRunning = _runningToggled;

        bool jumpTriggered = input.JumpPressed;

        if (IsGrounded && jumpTriggered)
        {
            _verticalVelocity = _jumpForce;
            IsGrounded = false;
        }

        if (!IsGrounded)
        {
            _verticalVelocity -= _gravity * dt;
            Position = new Vector3(Position.X, Position.Y + _verticalVelocity * dt, Position.Z);
        }

        float groundHeight = 0f;
        if (!IsGrounded && Position.Y <= groundHeight && _verticalVelocity <= 0)
        {
            Position = new Vector3(Position.X, groundHeight, Position.Z);
            _verticalVelocity = 0f;
            IsGrounded = true;
        }

        float moveX = input.MoveX;
        float moveZ = input.MoveZ;

        IsMoving = (Math.Abs(_currentSpeed) > 0.1f) || (moveZ != 0);

        float targetTurnSpeed = moveX != 0 ? -moveX * _rotationSpeed : 0f;
        float turnBlend = 1f - (float)Math.Exp(-10f * dt);
        _currentTurnSpeed = MathHelper.Lerp(_currentTurnSpeed, targetTurnSpeed, turnBlend);
        Yaw += _currentTurnSpeed * dt;

        float targetSpeed = 0f;
        if (moveZ != 0)
        {
            targetSpeed = (IsRunning ? _runSpeed : _walkSpeed) * Math.Sign(moveZ);
        }

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

            if (IsGrounded)
                newPos.Y = groundHeight;

            Position = newPos;
        }
    }
}
