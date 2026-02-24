using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Starfield2026.ModelLoader.Input;

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
    public bool SwitchModePressed { get; init; }

    public KeyboardState Keyboard { get; init; }
    public KeyboardState PreviousKeyboard { get; init; }
    public MouseState Mouse { get; init; }
    public MouseState PreviousMouse { get; init; }
    public GamePadState GamePad { get; init; }
    public GamePadState PreviousGamePad { get; init; }
    public bool GamePadConnected { get; init; }

    public bool IsKeyJustPressed(Keys key) => Keyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);
    public bool IsKeyHeld(Keys key) => Keyboard.IsKeyDown(key);

    public bool IsButtonJustPressed(Buttons button) => GamePad.IsButtonDown(button) && PreviousGamePad.IsButtonUp(button);
    public bool IsButtonHeld(Buttons button) => GamePad.IsButtonDown(button);

    public bool Confirm => ConfirmPressed;
    public bool Cancel => CancelPressed;
    public bool AnyKey => Keyboard.GetPressedKeyCount() > 0 && PreviousKeyboard.GetPressedKeyCount() == 0;
    public bool Up => IsKeyJustPressed(Keys.Up) || IsKeyJustPressed(Keys.W) || IsButtonJustPressed(Buttons.DPadUp);
    public bool Down => IsKeyJustPressed(Keys.Down) || IsKeyJustPressed(Keys.S) || IsButtonJustPressed(Buttons.DPadDown);
    public bool Left => IsKeyJustPressed(Keys.Left) || IsKeyJustPressed(Keys.A) || IsButtonJustPressed(Buttons.DPadLeft);
    public bool Right => IsKeyJustPressed(Keys.Right) || IsKeyJustPressed(Keys.D) || IsButtonJustPressed(Buttons.DPadRight);

    public bool PageLeft => IsKeyJustPressed(Keys.Q) || IsKeyJustPressed(Keys.PageUp) || IsButtonJustPressed(Buttons.LeftShoulder);
    public bool PageRight => IsKeyJustPressed(Keys.E) || IsKeyJustPressed(Keys.PageDown) || IsButtonJustPressed(Buttons.RightShoulder);

    public bool CyclePokemon => IsKeyJustPressed(Keys.LeftControl) || IsButtonJustPressed(Buttons.RightShoulder);
    public bool ThrowRecall => IsKeyJustPressed(Keys.LeftAlt) || IsButtonJustPressed(Buttons.X);

    public Point MousePosition => Mouse.Position;
    public bool MouseClicked => Mouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;
}
