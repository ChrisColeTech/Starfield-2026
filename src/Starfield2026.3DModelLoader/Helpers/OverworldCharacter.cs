#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
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
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            VertexColorEnabled = false,
            DiffuseColor = Vector3.One,
            AmbientLightColor = new Vector3(0.6f, 0.6f, 0.6f),
            EmissiveColor = Vector3.Zero,
        };
        _effect.DirectionalLight0.Enabled = true;
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.5f));
        _effect.DirectionalLight0.DiffuseColor = new Vector3(0.6f, 0.6f, 0.6f);
        _effect.DirectionalLight0.SpecularColor = Vector3.Zero;
        _effect.DirectionalLight1.Enabled = false;
        _effect.DirectionalLight2.Enabled = false;

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

    public void Update(float dt, bool isMoving, bool isRunning, bool isGrounded)
    {
        if (_player is null) return;

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
