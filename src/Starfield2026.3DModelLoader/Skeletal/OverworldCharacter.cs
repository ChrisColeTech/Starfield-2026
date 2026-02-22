#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader;

namespace Starfield2026.ModelLoader.Skeletal;

public sealed class OverworldCharacter : IDisposable
{
    public bool IsLoaded { get; private set; }

    private AnimationController? _controller;
    private SkinnedDaeModel? _model;
    private BasicEffect? _effect;
    private float _fitScale = 1f;
    private const float TargetHumanHeight = 2.0f;

    // Models come from different sources with different unit scales.
    // Sun-moon field characters are ~170 units for a human.
    // Scarlet characters/pokemon are ~1.2 units for a human.
    // We detect which group by checking if height > threshold, then apply
    // a single scale per group so relative sizes within a group are preserved.
    private const float SunMoonFieldRefHeight = 170f;  // human-sized trainer in sun-moon field units
    private const float ScarletRefHeight = 1.2f;       // human-sized trainer in scarlet units
    private const float GroupThreshold = 10f;           // heights above this are sun-moon scale

    public void Load(GraphicsDevice device, string characterFolderPath)
    {
        ModelLoaderLog.Info($"[Character] === Loading character from: {characterFolderPath} ===");
        Dispose();

        var animSet = SplitModelAnimationSetLoader.Load(characterFolderPath);

        ModelLoaderLog.Info($"[Character] Model path: {animSet.ModelPath}");
        ModelLoaderLog.Info($"[Character] Clips: {animSet.Clips.Count} total, {animSet.ClipsByTag.Count} tagged");
        foreach (var kvp in animSet.ClipsByTag)
            ModelLoaderLog.Info($"  Tag '{kvp.Key}': duration={kvp.Value.DurationSeconds:F3}s, tracks={kvp.Value.Tracks.Count}");

        _controller = new AnimationController(animSet);
        _model = new SkinnedDaeModel();
        _model.Load(device, animSet.ModelPath, animSet.Skeleton);

        _effect = new BasicEffect(device)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            VertexColorEnabled = false,
        };
        _effect.EnableDefaultLighting();

        _controller.Play("Idle");
        _controller.Update(0f);
        _model.UpdatePose(device, _controller.SkinPose);
        _model.ComputeSkinnedBounds(_controller.SkinPose);

        float modelHeight = _model.BoundsMax.Y - _model.BoundsMin.Y;
        if (modelHeight > 0.001f)
        {
            float refHeight = modelHeight > GroupThreshold ? SunMoonFieldRefHeight : ScarletRefHeight;
            _fitScale = TargetHumanHeight / refHeight;
        }

        ModelLoaderLog.Info($"[Character] Model height={modelHeight:F3}, fitScale={_fitScale:F3}, rendered height={modelHeight * _fitScale:F3}");
        ModelLoaderLog.Info($"[Character] === Character load complete ===");
        IsLoaded = true;
    }

    public void Update(float dt, bool isMoving, bool isRunning, bool isGrounded)
    {
        if (_controller == null) return;

        string desiredTag;
        if (!isGrounded && _controller.HasClip("Jump"))
            desiredTag = "Jump";
        else if (isRunning && _controller.HasClip("Run"))
            desiredTag = "Run";
        else if (isMoving && _controller.HasClip("Walk"))
            desiredTag = "Walk";
        else
            desiredTag = "Idle";

        if (_controller.ActiveTag != desiredTag)
            _controller.Play(desiredTag);

        _controller.Update(dt);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY)
    {
        if (_model == null || _effect == null || _controller == null)
            return;

        _model.UpdatePose(device, _controller.SkinPose);

        float baseY = _model.BoundsMin.Y * _fitScale;

        Matrix world = Matrix.CreateScale(_fitScale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position.X, position.Y - baseY, position.Z);

        _effect.World = world;
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
        _controller = null;
        IsLoaded = false;
    }

}
