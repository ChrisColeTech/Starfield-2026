using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Starfield2026.Core.Input;

public class InputManager
{
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _mouse;
    private MouseState _previousMouse;
    
    public InputSnapshot Current { get; private set; } = new();
    
    public void Update()
    {
        _previousKeyboard = _keyboard;
        _previousMouse = _mouse;
        _keyboard = Keyboard.GetState();
        _mouse = Mouse.GetState();
        
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
        
        return new InputSnapshot
        {
            MoveX = moveX,
            MoveZ = moveZ,
            MoveY = moveY,
            CameraYaw = cameraYaw,
            CameraPitch = cameraPitch,
            CameraZoom = cameraZoom,
            RunHeld = _keyboard.IsKeyDown(Keys.LeftShift) || _keyboard.IsKeyDown(Keys.RightShift),
            RunPressed = IsJustPressed(Keys.LeftShift) || IsJustPressed(Keys.RightShift),
            JumpHeld = _keyboard.IsKeyDown(Keys.Space) || _keyboard.IsKeyDown(Keys.C) || _keyboard.IsKeyDown(Keys.LeftAlt) || _keyboard.IsKeyDown(Keys.RightAlt),
            JumpPressed = IsJustPressed(Keys.Space) || IsJustPressed(Keys.C) || IsJustPressed(Keys.LeftAlt) || IsJustPressed(Keys.RightAlt),
            FireHeld = _keyboard.IsKeyDown(Keys.Space),
            ConfirmPressed = IsJustPressed(Keys.Enter),
            CancelPressed = IsJustPressed(Keys.Escape) || IsJustPressed(Keys.Back),
            PausePressed = IsJustPressed(Keys.Tab),
            Keyboard = _keyboard,
            PreviousKeyboard = _previousKeyboard,
            Mouse = _mouse,
            PreviousMouse = _previousMouse,
        };
    }
    
    private bool IsJustPressed(Keys key) => _keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
}
