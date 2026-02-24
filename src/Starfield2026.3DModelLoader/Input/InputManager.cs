using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Starfield2026.ModelLoader.Input;

public class InputManager
{
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _mouse;
    private MouseState _previousMouse;
    private GamePadState _gamePad;
    private GamePadState _previousGamePad;

    private const float StickDeadzone = 0.15f;
    private const float TriggerThreshold = 0.5f;

    public InputSnapshot Current { get; private set; } = new();

    public void Update()
    {
        _previousKeyboard = _keyboard;
        _previousMouse = _mouse;
        _previousGamePad = _gamePad;
        
        _keyboard = Keyboard.GetState();
        _mouse = Mouse.GetState();
        _gamePad = GamePad.GetState(PlayerIndex.One);

        Current = BuildSnapshot();
    }

    private InputSnapshot BuildSnapshot()
    {
        float moveX = 0f;
        if (_keyboard.IsKeyDown(Keys.A) || _keyboard.IsKeyDown(Keys.Left)) moveX -= 1f;
        if (_keyboard.IsKeyDown(Keys.D) || _keyboard.IsKeyDown(Keys.Right)) moveX += 1f;

        float moveZ = 0f;
        if (_keyboard.IsKeyDown(Keys.W) || _keyboard.IsKeyDown(Keys.Up)) moveZ += 1f;
        if (_keyboard.IsKeyDown(Keys.S) || _keyboard.IsKeyDown(Keys.Down)) moveZ -= 1f;

        float moveY = 0f;
        if (_keyboard.IsKeyDown(Keys.Space)) moveY += 1f;
        if (_keyboard.IsKeyDown(Keys.LeftControl) || _keyboard.IsKeyDown(Keys.RightControl)) moveY -= 1f;

        float cameraYaw = 0f;
        if (_keyboard.IsKeyDown(Keys.Q)) cameraYaw -= 1f;
        if (_keyboard.IsKeyDown(Keys.E)) cameraYaw += 1f;

        float cameraPitch = 0f;
        if (_keyboard.IsKeyDown(Keys.R)) cameraPitch -= 1f;
        if (_keyboard.IsKeyDown(Keys.F)) cameraPitch += 1f;

        float cameraZoom = 0f;
        if (_keyboard.IsKeyDown(Keys.Z)) cameraZoom -= 1f;
        if (_keyboard.IsKeyDown(Keys.X)) cameraZoom += 1f;

        int scrollDelta = _mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (scrollDelta != 0)
            cameraZoom += scrollDelta > 0 ? -1f : 1f;

        bool runHeld = _keyboard.IsKeyDown(Keys.LeftShift) || _keyboard.IsKeyDown(Keys.RightShift);
        bool jumpHeld = _keyboard.IsKeyDown(Keys.Space) || _keyboard.IsKeyDown(Keys.C);

        // GamePad input
        if (_gamePad.IsConnected)
        {
            float leftStickX = ApplyDeadzone(_gamePad.ThumbSticks.Left.X);
            float leftStickY = ApplyDeadzone(_gamePad.ThumbSticks.Left.Y);
            float rightStickX = ApplyDeadzone(_gamePad.ThumbSticks.Right.X);
            float rightStickY = ApplyDeadzone(_gamePad.ThumbSticks.Right.Y);

            if (Math.Abs(leftStickX) > Math.Abs(moveX)) moveX = leftStickX;
            if (Math.Abs(leftStickY) > Math.Abs(moveZ)) moveZ = leftStickY;

            if (Math.Abs(rightStickX) > Math.Abs(cameraYaw)) cameraYaw = rightStickX * 2f;
            if (Math.Abs(rightStickY) > Math.Abs(cameraPitch)) cameraPitch = -rightStickY * 2f;

            if (_gamePad.DPad.Left == ButtonState.Pressed) moveX = -1f;
            if (_gamePad.DPad.Right == ButtonState.Pressed) moveX = 1f;
            if (_gamePad.DPad.Up == ButtonState.Pressed) moveZ = 1f;
            if (_gamePad.DPad.Down == ButtonState.Pressed) moveZ = -1f;

            if (_gamePad.Triggers.Right > TriggerThreshold) runHeld = true;
            if (_gamePad.Buttons.LeftStick == ButtonState.Pressed) runHeld = true;
            if (_gamePad.Buttons.A == ButtonState.Pressed) jumpHeld = true;
            if (_gamePad.Buttons.B == ButtonState.Pressed) moveY = -1f;
        }

        return new InputSnapshot
        {
            MoveX = moveX,
            MoveZ = moveZ,
            MoveY = moveY,
            CameraYaw = cameraYaw,
            CameraPitch = cameraPitch,
            CameraZoom = cameraZoom,
            RunHeld = runHeld,
            RunPressed = IsJustPressed(Keys.LeftShift) || IsJustPressed(Keys.RightShift) || IsButtonJustPressed(Buttons.LeftStick),
            JumpHeld = jumpHeld,
            JumpPressed = IsJustPressed(Keys.Space) || IsJustPressed(Keys.C) || IsButtonJustPressed(Buttons.A),
            FireHeld = _keyboard.IsKeyDown(Keys.Space) || _gamePad.Buttons.RightShoulder == ButtonState.Pressed,
            ConfirmPressed = IsJustPressed(Keys.Enter) || IsButtonJustPressed(Buttons.A),
            CancelPressed = IsJustPressed(Keys.Escape) || IsJustPressed(Keys.Back) || IsButtonJustPressed(Buttons.B),
            PausePressed = IsJustPressed(Keys.Tab) || IsButtonJustPressed(Buttons.Start),
            SwitchModePressed = IsJustPressed(Keys.F1) || IsButtonJustPressed(Buttons.BigButton),
            Keyboard = _keyboard,
            PreviousKeyboard = _previousKeyboard,
            Mouse = _mouse,
            PreviousMouse = _previousMouse,
            GamePad = _gamePad,
            PreviousGamePad = _previousGamePad,
            GamePadConnected = _gamePad.IsConnected,
        };
    }

    private float ApplyDeadzone(float value)
    {
        if (Math.Abs(value) < StickDeadzone) return 0f;
        return value > 0 ? (value - StickDeadzone) / (1f - StickDeadzone) : (value + StickDeadzone) / (1f - StickDeadzone);
    }

    private bool IsJustPressed(Keys key) => _keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    private bool IsButtonJustPressed(Buttons button) => _gamePad.IsButtonDown(button) && _previousGamePad.IsButtonUp(button);
}
