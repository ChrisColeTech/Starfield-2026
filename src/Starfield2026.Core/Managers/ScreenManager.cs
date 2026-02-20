using System;
using Microsoft.Xna.Framework;
using Starfield2026.Core.Screens;

namespace Starfield2026.Core.Managers;

public class ScreenManager
{
    public IGameScreen ActiveScreen { get; private set; } = null!;
    public bool IsTransitioning => _isTransitioning;
    
    private bool _isTransitioning;
    private float _transitionAlpha;
    private float _transitionSpeed = 2.5f;
    private bool _transitionFadingOut;
    private IGameScreen? _nextScreen;
    private readonly Action<string> _onScreenChanged;
    
    public ScreenManager(Action<string> onScreenChanged)
    {
        _onScreenChanged = onScreenChanged;
    }
    
    public void SetInitialScreen(IGameScreen screen)
    {
        ActiveScreen = screen;
        ActiveScreen.OnEnter();
    }
    
    public void TransitionTo(IGameScreen screen, string screenName)
    {
        if (_isTransitioning || screen == ActiveScreen) return;
        
        _isTransitioning = true;
        _transitionFadingOut = true;
        _transitionAlpha = 0f;
        _nextScreen = screen;
        
        _onScreenChanged?.Invoke(screenName);
    }
    
    public void Update(float dt, Action<IGameScreen> updateScreen)
    {
        if (_isTransitioning)
        {
            if (_transitionFadingOut)
            {
                _transitionAlpha += _transitionSpeed * dt;
                if (_transitionAlpha >= 1f)
                {
                    _transitionAlpha = 1f;
                    ActiveScreen.OnExit();
                    ActiveScreen = _nextScreen!;
                    ActiveScreen.OnEnter();
                    _transitionFadingOut = false;
                }
            }
            else
            {
                _transitionAlpha -= _transitionSpeed * dt;
                if (_transitionAlpha <= 0f)
                {
                    _transitionAlpha = 0f;
                    _isTransitioning = false;
                    _nextScreen = null;
                }
            }
        }
        else
        {
            updateScreen(ActiveScreen);
        }
    }
    
    public float GetTransitionAlpha()
    {
        return _isTransitioning ? _transitionAlpha : 0f;
    }
}
