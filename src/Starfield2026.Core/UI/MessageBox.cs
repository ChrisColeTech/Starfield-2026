using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
                // Skip to full reveal
                _revealedChars = _currentMessage.Length;
            }
            else
            {
                // Advance to next message
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
    /// Draw the message box — dark background with pixel-block text.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds)
    {
        if (_currentMessage.Length == 0) return;

        // Background
        spriteBatch.Draw(pixel, bounds, Color.Black * 0.85f);

        // Border
        int b = 2;
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, b), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - b, bounds.Width, b), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, b, bounds.Height), Color.White * 0.6f);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - b, bounds.Y, b, bounds.Height), Color.White * 0.6f);

        // Text (pixel-block characters)
        string visibleText = _currentMessage[.._revealedChars];
        DrawPixelText(spriteBatch, pixel, visibleText, bounds.X + 12, bounds.Y + 10, Color.White);

        // Advance indicator (blinking arrow when fully revealed)
        if (IsTextFullyRevealed && _messageQueue.Count > 0)
        {
            int arrowX = bounds.Right - 20;
            int arrowY = bounds.Bottom - 16;
            if ((int)(DateTime.Now.TimeOfDay.TotalSeconds * 3) % 2 == 0)
                spriteBatch.Draw(pixel, new Rectangle(arrowX, arrowY, 6, 6), Color.Yellow);
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

    private static void DrawPixelText(SpriteBatch sb, Texture2D pixel, string text, int x, int y, Color color)
    {
        int charW = 7, charH = 12, spacing = 1;
        int startX = x;

        foreach (char c in text)
        {
            if (c == '\n')
            {
                x = startX;
                y += charH + 4;
                continue;
            }
            if (c == ' ')
            {
                x += charW + spacing;
                continue;
            }

            sb.Draw(pixel, new Rectangle(x, y, charW - 1, charH - 1), color * 0.9f);
            x += charW + spacing;
        }
    }
}
