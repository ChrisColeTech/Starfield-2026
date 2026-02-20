using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.UI.Fonts;

namespace Starfield2026.Core.UI;

/// <summary>
/// Displays text with a typewriter effect and supports queuing multiple messages.
/// Press confirm to skip typing or advance to the next message.
/// </summary>
public class BattleMessageBox
{
    private const float CharsPerSecond = 38f;
    private const float PromptBlinkRate = 2f; // blinks per second

    private readonly Queue<string> _messages = new();
    private string? _currentMessage;
    private float _visibleChars;
    private float _blinkTimer;

    /// <summary>True when there is a message being displayed or queued.</summary>
    public bool IsActive => _currentMessage != null;

    /// <summary>True when the current message is fully revealed.</summary>
    public bool IsTextFullyRevealed =>
        _currentMessage != null && (int)_visibleChars >= _currentMessage.Length;

    /// <summary>True when all messages have been shown and dismissed.</summary>
    public bool IsFinished => _currentMessage == null && _messages.Count == 0;

    /// <summary>Fires when the last message is dismissed.</summary>
    public Action? OnFinished { get; set; }

    /// <summary>Queue a single message for display.</summary>
    public void Show(string message)
    {
        if (_currentMessage == null)
        {
            _currentMessage = message;
            _visibleChars = 0f;
            _blinkTimer = 0f;
        }
        else
        {
            _messages.Enqueue(message);
        }
    }

    /// <summary>Queue multiple messages for sequential display.</summary>
    public void ShowMultiple(params string[] messages)
    {
        foreach (var msg in messages)
            Show(msg);
    }

    /// <summary>Clear all messages and reset state.</summary>
    public void Clear()
    {
        _messages.Clear();
        _currentMessage = null;
        _visibleChars = 0f;
        _blinkTimer = 0f;
        OnFinished = null;
    }

    /// <summary>
    /// Advance the typewriter or move to the next message.
    /// </summary>
    /// <param name="deltaTime">Frame time in seconds.</param>
    /// <param name="confirmPressed">True on the frame the confirm button is pressed.</param>
    public void Update(float deltaTime, bool confirmPressed)
    {
        if (_currentMessage == null)
            return;

        if (!IsTextFullyRevealed)
        {
            _visibleChars += deltaTime * CharsPerSecond;

            // Confirm while typing → reveal all instantly
            if (confirmPressed)
                _visibleChars = _currentMessage.Length;
        }
        else
        {
            _blinkTimer += deltaTime;

            // Confirm while fully revealed → advance
            if (confirmPressed)
                Advance();
        }
    }

    /// <summary>
    /// Draw the message box with bordered panel and current text (SpriteFont version).
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Rectangle bounds)
    {
        if (_currentMessage == null)
            return;

        // Bordered panel
        UIStyle.DrawBattlePanel(spriteBatch, pixel, bounds);

        // Visible portion of text
        int charCount = Math.Min((int)_visibleChars, _currentMessage.Length);
        string visibleText = _currentMessage[..charCount];

        int padding = 16;
        UIStyle.DrawShadowedText(spriteBatch, font, visibleText,
            new Vector2(bounds.X + padding, bounds.Y + padding),
            UIStyle.TextNormal, UIStyle.TextNormalOutline);

        DrawPrompt(spriteBatch, pixel, bounds, padding);
    }

    /// <summary>
    /// Draw the message box with bordered panel and current text (KermFont version).
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, KermFontRenderer fontRenderer, Texture2D pixel, Rectangle bounds, int fontScale = 1)
    {
        if (_currentMessage == null)
            return;

        UIStyle.DrawBattlePanel(spriteBatch, pixel, bounds);

        int padding = 6 * fontScale;
        int maxChars = Math.Min((int)_visibleChars, _currentMessage.Length);
        fontRenderer.DrawString(spriteBatch, _currentMessage,
            new Vector2(bounds.X + padding, bounds.Y + padding),
            fontScale, Color.White, maxChars);

        DrawPrompt(spriteBatch, pixel, bounds, padding);
    }

    private void DrawPrompt(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, int padding)
    {
        if (IsTextFullyRevealed)
        {
            bool showPrompt = ((int)(_blinkTimer * PromptBlinkRate)) % 2 == 0;
            if (showPrompt)
            {
                int arrowSize = 15;
                int ax = bounds.Right - arrowSize - padding;
                int ay = bounds.Bottom - arrowSize - padding + 2;
                UIStyle.DrawArrowDown(spriteBatch, pixel, ax, ay, arrowSize, UIStyle.TextPrompt);
            }
        }
    }

    private void Advance()
    {
        if (_messages.Count > 0)
        {
            _currentMessage = _messages.Dequeue();
            _visibleChars = 0f;
            _blinkTimer = 0f;
        }
        else
        {
            _currentMessage = null;
            _visibleChars = 0f;
            OnFinished?.Invoke();
        }
    }

}
