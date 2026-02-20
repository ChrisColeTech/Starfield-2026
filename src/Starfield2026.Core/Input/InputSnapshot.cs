using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Starfield2026.Core.Input;

public class InputSnapshot
{
    public float MoveX { get; init; }
    public float MoveZ { get; init; }
    public float MoveY { get; init; }
    
    public float CameraYaw { get; init; }
    public float CameraPitch { get; init; }
    public float CameraZoom { get; init; }
    
    public bool RunHeld { get; init; }
    public bool JumpPressed { get; init; }
    public bool FireHeld { get; init; }
    public bool RunPressed { get; init; }
    public bool JumpHeld { get; init; }
    public bool ConfirmPressed { get; init; }
    public bool CancelPressed { get; init; }
    public bool PausePressed { get; init; }
    public bool ExitPressed { get; init; }
    
    public KeyboardState Keyboard { get; init; }
    public KeyboardState PreviousKeyboard { get; init; }
    public MouseState Mouse { get; init; }
    public MouseState PreviousMouse { get; init; }
    
    
    public bool IsKeyJustPressed(Keys key) => Keyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);
    public bool IsKeyHeld(Keys key) => Keyboard.IsKeyDown(key);

    public bool Confirm => ConfirmPressed;
    public bool Cancel => CancelPressed;
    public bool AnyKey => Keyboard.GetPressedKeyCount() > 0 && PreviousKeyboard.GetPressedKeyCount() == 0;
    public bool Up => IsKeyJustPressed(Keys.Up) || IsKeyJustPressed(Keys.W);
    public bool Down => IsKeyJustPressed(Keys.Down) || IsKeyJustPressed(Keys.S);
    public bool Left => IsKeyJustPressed(Keys.Left) || IsKeyJustPressed(Keys.A);
    public bool Right => IsKeyJustPressed(Keys.Right) || IsKeyJustPressed(Keys.D);

    public bool PageLeft => IsKeyJustPressed(Keys.Q) || IsKeyJustPressed(Keys.PageUp);
    public bool PageRight => IsKeyJustPressed(Keys.E) || IsKeyJustPressed(Keys.PageDown);

    public Point MousePosition => Mouse.Position;
    public bool MouseClicked => Mouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;
}
