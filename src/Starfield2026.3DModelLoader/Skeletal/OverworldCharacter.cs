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
    private const float TargetHeight = 2.0f;

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
            _fitScale = TargetHeight / modelHeight;

        ModelLoaderLog.Info($"[Character] Model height={modelHeight:F3}, fitScale={_fitScale:F3} (target={TargetHeight})");
        ModelLoaderLog.Info($"[Character] === Character load complete ===");
        IsLoaded = true;
    }

    public void Update(float dt, bool isMoving, bool isRunning, bool isGrounded)
    {
        if (_controller == null) return;

        string desiredTag;
        if (isRunning && _controller.HasClip("Run"))
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
