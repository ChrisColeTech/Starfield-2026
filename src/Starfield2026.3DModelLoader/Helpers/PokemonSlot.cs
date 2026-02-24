#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

    private static Dictionary<string, float>? _heightCache;
    private const float DefaultHeight = 1.0f;

    public bool IsLoaded { get; private set; }
    public string FolderPath { get; private set; } = "";
    public string DisplayName { get; private set; } = "";

    public static void LoadHeightConfig(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return;
        try
        {
            string json = File.ReadAllText(jsonPath);
            _heightCache = JsonSerializer.Deserialize<Dictionary<string, float>>(json);
        }
        catch { _heightCache = null; }
    }

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
        float targetHeight = GetTargetHeight(DisplayName);
        
        if (modelHeight > 0.001f)
            _fitScale = targetHeight / modelHeight;

        IsLoaded = true;
        ModelLoaderLog.Info($"[Pokemon] Loaded: {DisplayName}, modelHeight={modelHeight:F3}, targetHeight={targetHeight:F2}, scale={_fitScale:F3}");
    }

    private float GetTargetHeight(string folderName)
    {
        if (_heightCache == null) return DefaultHeight;

        string? speciesId = ExtractSpeciesId(folderName);
        if (speciesId != null && _heightCache.TryGetValue(speciesId, out float height))
            return height;

        if (_heightCache.TryGetValue("default", out float defaultHeight))
            return defaultHeight;

        return DefaultHeight;
    }

    private static string? ExtractSpeciesId(string folderName)
    {
        if (folderName.Length >= 7 && folderName.StartsWith("pm"))
        {
            return folderName.Substring(0, 7);
        }
        return null;
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
