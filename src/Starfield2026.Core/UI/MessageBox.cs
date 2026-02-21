using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI;

/// <summary>
/// Text display with typewriter effect and message queue.
/// Shows one message at a time with character-by-character reveal.
/// Press confirm to skip typing or advance to the next message.
/// </summary>
public class MessageBox
{
    private readonly Queue<string> _messageQueue = new();
    private string _currentMessage = "";
    private int _revealedChars;
    private float _charTimer;

    /// <summary>Characters per second for typewriter effect.</summary>
    public float TypeSpeed { get; set; } = 40f;

    /// <summary>True while a message is being displayed.</summary>
    public bool IsActive => _currentMessage.Length > 0 || _messageQueue.Count > 0;

    /// <summary>True when all characters of the current message are visible.</summary>
    public bool IsTextFullyRevealed => _revealedChars >= _currentMessage.Length;

    /// <summary>True when the last message has been dismissed.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Callback fired when the last message is dismissed.</summary>
    public Action? OnFinished { get; set; }

    /// <summary>Queue a single message for display.</summary>
    public void Show(string message)
    {
        IsFinished = false;
        _messageQueue.Enqueue(message);
        if (_currentMessage.Length == 0)
            AdvanceToNext();
    }

    /// <summary>Queue multiple messages.</summary>
    public void ShowMultiple(params string[] messages)
    {
        IsFinished = false;
        foreach (var msg in messages)
            _messageQueue.Enqueue(msg);
        if (_currentMessage.Length == 0)
            AdvanceToNext();
    }

    /// <summary>Clear all state.</summary>
    public void Clear()
    {
        _messageQueue.Clear();
        _currentMessage = "";
        _revealedChars = 0;
        _charTimer = 0;
        IsFinished = true;
    }

    /// <summary>
    /// Advance the typewriter. If confirmPressed: skip to full reveal or advance to next message.
    /// </summary>
    public void Update(float deltaTime, bool confirmPressed)
    {
        if (_currentMessage.Length == 0) return;

        if (confirmPressed)
        {
            if (!IsTextFullyRevealed)
            {
                _revealedChars = _currentMessage.Length;
            }
            else
            {
                AdvanceToNext();
            }
            return;
        }

        // Typewriter tick
        if (!IsTextFullyRevealed)
        {
            _charTimer += deltaTime;
            float interval = 1f / TypeSpeed;
            while (_charTimer >= interval && _revealedChars < _currentMessage.Length)
            {
                _revealedChars++;
                _charTimer -= interval;
            }
        }
    }

    /// <summary>
    /// Draw the message box. All layout proportional to bounds.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, PixelFont uiFont, Texture2D pixel, Rectangle bounds, int fontScale = 1)
    {
        if (_currentMessage.Length == 0) return;

        uiFont.Scale = fontScale;

        // Background
        spriteBatch.Draw(pixel, bounds, UITheme.MenuBackground);

        // Border
        int b = Math.Max(1, fontScale / 2);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, b), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - b, bounds.Width, b), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, b, bounds.Height), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - b, bounds.Y, b, bounds.Height), Color.White * 0.6f);

        // Text — inset proportionally from bounds
        int padX = bounds.Width / 20;
        int padY = bounds.Height / 6;
        string visibleText = _currentMessage[.._revealedChars];
        UIStyle.DrawShadowedText(spriteBatch, uiFont, visibleText,
            new Vector2(bounds.X + padX, bounds.Y + padY),
            Color.White, Color.Black * 0.5f);

        // Blinking advance arrow when fully revealed
        if (IsTextFullyRevealed)
        {
            int arrowSize = uiFont.CharHeight / 2;
            int arrowX = bounds.Right - padX;
            int arrowY = bounds.Bottom - padY;
            if ((int)(DateTime.Now.TimeOfDay.TotalSeconds * 3) % 2 == 0)
                UIStyle.DrawDownArrow(spriteBatch, pixel, new Vector2(arrowX, arrowY), arrowSize, Color.White);
        }
    }

    private void AdvanceToNext()
    {
        if (_messageQueue.Count > 0)
        {
            _currentMessage = _messageQueue.Dequeue();
            _revealedChars = 0;
            _charTimer = 0;
        }
        else
        {
            _currentMessage = "";
            _revealedChars = 0;
            IsFinished = true;
            OnFinished?.Invoke();
        }
    }
}
