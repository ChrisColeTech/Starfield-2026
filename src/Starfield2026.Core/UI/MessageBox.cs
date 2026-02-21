using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI;

/// <summary>
/// Typewriter-style text display with callback on completion.
/// Shows text character by character, advances on key press.
/// </summary>
public class MessageBox
{
    private string _fullText = "";
    private int _charIndex;
    private float _charTimer;
    private bool _finished;

    private const float CharsPerSecond = 60f;

    public bool IsActive { get; private set; }
    public Action? OnFinished { get; set; }

    /// <summary>Show a message with typewriter effect.</summary>
    public void Show(string text)
    {
        _fullText = text ?? "";
        _charIndex = 0;
        _charTimer = 0f;
        _finished = false;
        IsActive = true;
    }

    /// <summary>Clear the message box.</summary>
    public void Clear()
    {
        _fullText = "";
        _charIndex = 0;
        _finished = false;
        IsActive = false;
        OnFinished = null;
    }

    /// <summary>Advance typewriter and handle input.</summary>
    public void Update(float deltaTime, bool anyKeyPressed)
    {
        if (!IsActive) return;

        if (!_finished)
        {
            _charTimer += deltaTime;
            int charsToShow = (int)(CharsPerSecond * _charTimer);
            if (charsToShow > 0)
            {
                _charIndex = Math.Min(_charIndex + charsToShow, _fullText.Length);
                _charTimer = 0f;
            }

            if (anyKeyPressed && _charIndex < _fullText.Length)
            {
                _charIndex = _fullText.Length;
                return;
            }

            if (_charIndex >= _fullText.Length)
                _finished = true;
        }
        else if (anyKeyPressed)
        {
            var cb = OnFinished;
            OnFinished = null;
            IsActive = cb != null;
            cb?.Invoke();
        }
    }

    /// <summary>Draw the message box panel with current text.</summary>
    public void Draw(SpriteBatch sb, PixelFont font, Texture2D pixel,
        Rectangle bounds, int fontScale)
    {
        if (!IsActive) return;

        int radius = Math.Max(2, fontScale * 2);
        int shadowOff = Math.Max(1, fontScale);

        // Panel with drop shadow
        UIDraw.ShadowedPanel(sb, pixel, bounds, radius,
            UITheme.SlatePanelBg, shadowOff, Color.Black * 0.3f);

        // Text
        int pad = 8 * fontScale;
        font.Scale = fontScale;

        string visible = _fullText[.._charIndex];
        UIDraw.ShadowedText(sb, font, visible,
            new Vector2(bounds.X + pad, bounds.Y + pad),
            UITheme.TextPrimary, UITheme.TextShadow);

        // Blinking advance indicator
        if (_finished && OnFinished != null)
        {
            float blink = (float)(DateTime.Now.Millisecond % 600) / 600f;
            if (blink < 0.5f)
            {
                int arrowX = bounds.Right - pad - font.CharWidth;
                int arrowY = bounds.Bottom - pad - font.CharHeight;
                font.Draw(">", arrowX, arrowY, UITheme.PurpleAccent);
            }
        }
    }
}
