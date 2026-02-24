#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Helpers;

public sealed class PokemonSlot : IDisposable
{
    private ClipPlayer? _player;
    private AnimationSet? _animSet;
    private SkinnedModel? _model;
    private BasicEffect? _effect;
    private float _fitScale = 1f;

    private const float TargetPokemonHeight = 0.8f;

    public bool IsLoaded { get; private set; }
    public string FolderPath { get; private set; } = "";
    public string DisplayName { get; private set; } = "";

    public void Load(GraphicsDevice device, string folderPath)
    {
        Dispose();
        FolderPath = folderPath;
        DisplayName = System.IO.Path.GetFileName(folderPath.TrimEnd('/', '\\'));

        var animSet = AnimationSetLoader.Load(folderPath);

        _animSet = animSet;
        _player = new ClipPlayer(animSet.Skeleton);
        _model = new SkinnedModel();
        _model.Load(device, animSet.ModelPath, animSet.Skeleton);

        _effect = new BasicEffect(device)
        {
            LightingEnabled = false,
            VertexColorEnabled = false,
        };

        if (animSet.HasTag("Idle"))
        {
            _player.Play(animSet.GetByTag("Idle")!, loop: true);
        }
        _player.Update(0f);
        _model.UpdatePose(device, _player.SkinPose);
        _model.ComputeSkinnedBounds(_player.SkinPose);

        float modelHeight = _model.BoundsMax.Y - _model.BoundsMin.Y;
        if (modelHeight > 0.001f)
            _fitScale = TargetPokemonHeight / modelHeight;

        IsLoaded = true;
        ModelLoaderLog.Info($"[Pokemon] Loaded: {DisplayName}, height={modelHeight:F3}, fitScale={_fitScale:F6}");
    }

    public void Update(float dt)
    {
        _player?.Update(dt);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float yaw, float deployScale = 1f)
    {
        if (_model is null || _effect is null || _player is null || deployScale <= 0.001f) return;

        _model.UpdatePose(device, _player.SkinPose);

        float scale = _fitScale * deployScale;
        float baseY = _model.BoundsMin.Y * scale;
        var world = Matrix.CreateScale(scale)
            * Matrix.CreateRotationY(yaw)
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
        _player = null;
        _animSet = null;
        _fitScale = 1f;
        IsLoaded = false;
    }
}
