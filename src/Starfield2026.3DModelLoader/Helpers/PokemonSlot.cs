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

    private static Dictionary<string, float>? _genScales;
    private const float DefaultGenScale = 0.013f;

    private static readonly string[] KnownGenerations = { "sun-moon-v2", "sun-moon", "scarlet", "plza" };

    public bool IsLoaded { get; private set; }
    public string FolderPath { get; private set; } = "";
    public string DisplayName { get; private set; } = "";

    public static void LoadGenScales(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            ModelLoaderLog.Info($"[Pokemon] Gen scales file not found: {jsonPath}");
            return;
        }
        try
        {
            string json = File.ReadAllText(jsonPath);
            _genScales = JsonSerializer.Deserialize<Dictionary<string, float>>(json);
            ModelLoaderLog.Info($"[Pokemon] Loaded gen scales: {_genScales?.Count ?? 0} entries");
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[Pokemon] Failed to load gen scales: {ex.Message}");
            _genScales = null;
        }
    }

    public void Load(GraphicsDevice device, string folderPath)
    {
        Dispose();
        FolderPath = folderPath;
        DisplayName = Path.GetFileName(folderPath.TrimEnd('/', '\\'));

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

        // Determine generation and apply gen-specific scale
        string gen = DetectGeneration(folderPath);
        float genScale = GetGenScale(gen);
        _fitScale = genScale;

        IsLoaded = true;
        ModelLoaderLog.Info($"[Pokemon] Loaded: {DisplayName}, gen={gen}, genScale={genScale:F4}");
    }

    private static string DetectGeneration(string folderPath)
    {
        string normalized = folderPath.Replace('\\', '/');
        foreach (var gen in KnownGenerations)
        {
            if (normalized.Contains($"/{gen}/", StringComparison.OrdinalIgnoreCase))
            {
                return gen;
            }
        }
        // Also detect PLZA from test dump paths (plza-dump-patched, etc.)
        if (normalized.Contains("/plza-dump", StringComparison.OrdinalIgnoreCase))
            return "plza";
        return "unknown";
    }

    private static float GetGenScale(string gen)
    {
        if (_genScales != null && _genScales.TryGetValue(gen, out float scale))
            return scale;
        return DefaultGenScale;
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
