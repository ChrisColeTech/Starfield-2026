#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Helpers;

public sealed class OverworldCharacter : IDisposable
{
    private ClipPlayer? _player;
    private AnimationSet? _animSet;
    private SkinnedModel? _model;
    private BasicEffect? _effect;
    private string? _activeTag;
    private float _fitScale = 1f;

    private enum BallState { None, Throwing, Deployed, Recalling }
    private BallState _ballState = BallState.None;

    private const float TargetHumanHeight = 2.0f;
    private const float SunMoonRefHeight = 170f;
    private const float ScarletRefHeight = 1.2f;
    private const float GroupThreshold = 10f;

    public bool IsLoaded { get; private set; }
    public AnimationSet? AnimationSet => _animSet;

    public void Load(GraphicsDevice device, AnimationSet animSet)
    {
        Dispose();

        _animSet = animSet;
        _player = new ClipPlayer(animSet.Skeleton);
        _model = new SkinnedModel();
        _model.Load(device, animSet.ModelPath, animSet.Skeleton);

        _effect = new BasicEffect(device)
        {
            LightingEnabled = false,
            VertexColorEnabled = false,
        };

        Play("Idle");
        _player.Update(0f);
        _model.UpdatePose(device, _player.SkinPose);
        _model.ComputeSkinnedBounds(_player.SkinPose);

        float modelHeight = _model.BoundsMax.Y - _model.BoundsMin.Y;
        if (modelHeight > 0.001f)
        {
            float refHeight = modelHeight > GroupThreshold ? SunMoonRefHeight : ScarletRefHeight;
            _fitScale = TargetHumanHeight / refHeight;
        }

        IsLoaded = true;
    }

    public bool Play(string tag, bool loop = true, bool resetTime = true)
    {
        if (_player is null || _animSet is null) return false;
        if (_activeTag == tag && !resetTime) return true;

        _activeTag = tag;
        var clip = _animSet.GetByTag(tag);
        if (clip is null) return false;

        _player.Play(clip, loop, resetTime);
        return true;
    }

    public bool HasClip(string tag) => _animSet?.HasTag(tag) ?? false;

    public void Update(float dt, bool isMoving, bool isRunning, bool isGrounded, InputSnapshot? input = null)
    {
        if (_player is null) return;

        // Ball throw/recall state machine
        if (input != null && input.IsKeyJustPressed(Keys.LeftAlt))
        {
            if (_ballState == BallState.None && HasClip("BallThrow"))
            {
                _ballState = BallState.Throwing;
                Play("BallThrow", loop: false);
                _player.Speed = 1.25f;
                _player.Update(dt);
                return;
            }
            else if (_ballState == BallState.Deployed && HasClip("BallRecall"))
            {
                _ballState = BallState.Recalling;
                Play("BallRecall", loop: false);
                _player.Speed = 1.25f;
                _player.Update(dt);
                return;
            }
        }

        if (_ballState == BallState.Throwing)
        {
            if (_player.IsFinished)
                _ballState = BallState.Deployed;
            else
            {
                _player.Update(dt);
                return;
            }
        }

        if (_ballState == BallState.Recalling)
        {
            if (_player.IsFinished)
                _ballState = BallState.None;
            else
            {
                _player.Update(dt);
                return;
            }
        }

        // Normal locomotion
        string desiredTag;
        if (!isGrounded && HasClip("Jump"))
            desiredTag = "Jump";
        else if (isRunning && HasClip("Run"))
            desiredTag = "Run";
        else if (isMoving && HasClip("Walk"))
            desiredTag = "Walk";
        else
            desiredTag = "Idle";

        if (_activeTag != desiredTag)
            Play(desiredTag);

        _player.Speed = desiredTag == "Jump" ? 0.5f : 1f;
        _player.Update(dt);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY)
    {
        if (_model is null || _effect is null || _player is null) return;

        _model.UpdatePose(device, _player.SkinPose);

        float baseY = _model.BoundsMin.Y * _fitScale;

        _effect.World = Matrix.CreateScale(_fitScale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position.X, position.Y - baseY, position.Z);
        _effect.View = view;
        _effect.Projection = projection;

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.LinearWrap;

        _model.Draw(device, _effect);
    }

    public void Dispose()
    {
        _model?.Dispose();
        _effect?.Dispose();
        _model = null;
        _effect = null;
        _player = null;
        _animSet = null;
        _activeTag = null;
        IsLoaded = false;
    }
}
