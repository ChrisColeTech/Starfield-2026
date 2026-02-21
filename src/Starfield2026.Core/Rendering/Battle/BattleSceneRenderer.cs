using Starfield2026.Core.Rendering;
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
// using PokemonGreen.Assets;

namespace Starfield2026.Core.Rendering.Battle;

/// <summary>
/// Handles 3D rendering: battle backgrounds, platforms, Pokemon models,
/// and placeholder cubes.
/// </summary>
public class BattleSceneRenderer
{
    private GraphicsDevice? _device;

    // Battle scene assets
    private readonly Dictionary<BattleBackground, (BattleModelData bg, BattleModelData ally, BattleModelData foe)>
        _battleScenes = new();
    private BattleModelData? _activeBattleBG;
    private BattleModelData? _activePlatformAlly;
    private BattleModelData? _activePlatformFoe;
    private AlphaTestEffect? _battleEffect;

    // Pokemon 3D models
    private SkeletalModelData? _allyModel;
    private SkeletalModelData? _foeModel;

    // Placeholder cubes
    private VertexBuffer? _cubeVB;
    private IndexBuffer? _cubeIB;
    private BasicEffect? _cubeEffect;
    private float _foeCubeScale = 8.0f;
    private float _allyCubeScale = 2.0f;

    // Deploy/recall animation (0 = hidden, 1 = fully visible)
    private float _foeDisplayScale = 0f;
    private float _allyDisplayScale = 0f;
    private float _foeTargetScale = 0f;
    private float _allyTargetScale = 0f;
    private const float DeploySpeed = 1.2f; // units per second

    public bool HasLoadedModels => _battleEffect != null && _activeBattleBG != null;
    public GraphicsDevice? Device => _device;
    public SkeletalModelData? AllyModel => _allyModel;
    public SkeletalModelData? FoeModel => _foeModel;

    /// <summary>
    /// Load all battle background sets from BattleBG/ folder.
    /// </summary>
    public void LoadBattleModels(GraphicsDevice device, string basePath)
    {
        _device = device;
        try
        {
            var modelCache = new Dictionary<string, BattleModelData>();

            BattleModelData LoadModel(string relativePath)
            {
                if (modelCache.TryGetValue(relativePath, out var cached))
                    return cached;
                var model = BattleModelLoader.Load(Path.Combine(basePath, relativePath), device);
                modelCache[relativePath] = model;
                return model;
            }

            _battleScenes[BattleBackground.Grass] = (
                LoadModel("Grass/Grass.dae"),
                LoadModel("PlatformGrassAlly/GrassAlly.dae"),
                LoadModel("PlatformGrassFoe/GrassFoe.dae"));

            _battleScenes[BattleBackground.TallGrass] = (
                LoadModel("Grass/Grass.dae"),
                LoadModel("PlatformTallGrassAlly/TallGrassAlly.dae"),
                LoadModel("PlatformTallGrassFoe/TallGrassFoe.dae"));

            _battleScenes[BattleBackground.Cave] = (
                LoadModel("Cave/Cave.dae"),
                LoadModel("PlatformCaveAlly/CaveAlly.dae"),
                LoadModel("PlatformCaveFoe/CaveFoe.dae"));

            _battleScenes[BattleBackground.Dark] = (
                LoadModel("Dark/Dark.dae"),
                LoadModel("PlatformDark/Dark.dae"),
                LoadModel("PlatformDark/Dark.dae"));

            SetBackground(BattleBackground.Grass);

            _battleEffect = new AlphaTestEffect(device)
            {
                VertexColorEnabled = false,
                Alpha = 1f,
                ReferenceAlpha = 128,
                AlphaFunction = CompareFunction.GreaterEqual,
            };

            Console.WriteLine($"[Battle3D] Loaded {_battleScenes.Count} background sets, {modelCache.Count} unique models");

            CreatePlaceholderCube(device);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Battle3D] FAILED to load backgrounds: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    public void SetBackground(BattleBackground bg)
    {
        if (_battleScenes.TryGetValue(bg, out var scene))
        {
            _activeBattleBG = scene.bg;
            _activePlatformAlly = scene.ally;
            _activePlatformFoe = scene.foe;
        }
    }

    public void SetPokemonModels(SkeletalModelData? ally, SkeletalModelData? foe)
    {
        _allyModel = ally;
        _foeModel = foe;
    }

    public void ClearPokemonModels()
    {
        _allyModel = null;
        _foeModel = null;
        _foeDisplayScale = 0f;
        _allyDisplayScale = 0f;
        _foeTargetScale = 0f;
        _allyTargetScale = 0f;
    }

    /// <summary>
    /// Randomize placeholder cube sizes for testing camera framing.
    /// Call at battle start before computing heights.
    /// </summary>
    public void RandomizePlaceholderCubes(System.Random? rng = null)
    {
        rng ??= System.Random.Shared;
        _foeCubeScale = 1.5f + (float)rng.NextDouble() * 10.5f;  // 1.5 to 12.0
        _allyCubeScale = 1.0f + (float)rng.NextDouble() * 5.0f;  // 1.0 to 6.0
        System.Console.WriteLine($"[Battle3D] Placeholder cubes: foe={_foeCubeScale:F1}, ally={_allyCubeScale:F1}");
    }

    /// <summary>
    /// Compute the foe height for camera adjustment, accounting for model scale.
    /// </summary>
    public float ComputeFoeHeight()
    {
        if (_foeModel != null)
            return (_foeModel.BoundsMax.Y - _foeModel.BoundsMin.Y) *
                   FitModelScale(_foeModel, 3.0f);
        return _foeCubeScale;
    }

    /// <summary>
    /// Compute the ally height for camera adjustment.
    /// </summary>
    public float ComputeAllyHeight()
    {
        if (_allyModel != null)
            return (_allyModel.BoundsMax.Y - _allyModel.BoundsMin.Y) *
                   FitModelScale(_allyModel, 3.5f);
        return _allyCubeScale;
    }

    /// <summary>
    /// Draw the full 3D battle scene.
    /// </summary>
    public void Draw(GraphicsDevice device, Matrix view, float fovRadians)
    {
        device.Clear(Color.Black);

        if (_battleEffect == null || _activeBattleBG == null)
            return;

        float aspect = device.Viewport.AspectRatio;
        var projection = Matrix.CreatePerspectiveFieldOfView(
            fovRadians, aspect, 1f, 512f);

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.PointClamp;
        device.BlendState = BlendState.AlphaBlend;

        _battleEffect.View = view;
        _battleEffect.Projection = projection;
        _battleEffect.VertexColorEnabled = false;
        _battleEffect.Alpha = 1f;
        _battleEffect.DiffuseColor = Vector3.One;

        // Background at origin
        _battleEffect.World = Matrix.Identity;
        _activeBattleBG.Draw(device, _battleEffect);

        // Foe platform at (0, -0.20, -15)
        if (_activePlatformFoe != null)
        {
            _battleEffect.World = Matrix.CreateTranslation(0f, -0.20f, -15f);
            _activePlatformFoe.Draw(device, _battleEffect);
        }

        // Ally platform at (0, -0.20, 3)
        if (_activePlatformAlly != null)
        {
            _battleEffect.World = Matrix.CreateTranslation(0f, -0.20f, 3f);
            _activePlatformAlly.Draw(device, _battleEffect);
        }

        // Foe Pokemon (scaled by deploy animation)
        if (_foeDisplayScale > 0.001f)
        {
            if (_foeModel != null)
            {
                float scale = FitModelScale(_foeModel, 3.0f) * _foeDisplayScale;
                _battleEffect.World = Matrix.CreateScale(scale) *
                    Matrix.CreateTranslation(0f, -0.20f - _foeModel.BoundsMin.Y * scale, -15f);
                _foeModel.Draw(device, _battleEffect);
            }
            else
            {
                float s = _foeCubeScale * _foeDisplayScale;
                DrawPlaceholderCube(device, view, projection,
                    scale: s,
                    position: new Vector3(0f, -0.20f + s / 2f, -15f));
            }
        }

        // Ally Pokemon (scaled by deploy animation, rotated 180° to face foe)
        if (_allyDisplayScale > 0.001f)
        {
            if (_allyModel != null)
            {
                float scale = FitModelScale(_allyModel, 3.5f) * _allyDisplayScale;
                _battleEffect.World = Matrix.CreateScale(scale) *
                    Matrix.CreateRotationY(MathF.PI) *
                    Matrix.CreateTranslation(0f, -0.20f - _allyModel.BoundsMin.Y * scale, 3f);
                _allyModel.Draw(device, _battleEffect);
            }
            else
            {
                float s = _allyCubeScale * _allyDisplayScale;
                DrawPlaceholderCube(device, view, projection,
                    scale: s,
                    position: new Vector3(0f, -0.20f + s / 2f, 3f));
            }
        }

        // Reset GPU state for 2D rendering
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
    }

    public void DeployFoe() { _foeTargetScale = 1f; _foeDisplayScale = 1f; }
    public void RecallFoe() => _foeTargetScale = 0f;
    public void DeployAlly() => _allyTargetScale = 1f;
    public void RecallAlly() => _allyTargetScale = 0f;

    public bool IsFoeDeployed => _foeDisplayScale >= 1f;
    public bool IsAllyDeployed => _allyDisplayScale >= 1f;
    public bool IsFoeRecalled => _foeDisplayScale <= 0f;
    public bool IsAllyRecalled => _allyDisplayScale <= 0f;

    /// <summary>
    /// Update Pokemon model animations and deploy/recall scale.
    /// </summary>
    public void UpdateModels(float dt, double totalSeconds)
    {
        _allyModel?.Update(totalSeconds);
        _foeModel?.Update(totalSeconds);

        // Animate deploy/recall
        if (_foeDisplayScale < _foeTargetScale)
            _foeDisplayScale = System.Math.Min(_foeTargetScale, _foeDisplayScale + DeploySpeed * dt);
        else if (_foeDisplayScale > _foeTargetScale)
            _foeDisplayScale = System.Math.Max(_foeTargetScale, _foeDisplayScale - DeploySpeed * dt);

        if (_allyDisplayScale < _allyTargetScale)
            _allyDisplayScale = System.Math.Min(_allyTargetScale, _allyDisplayScale + DeploySpeed * dt);
        else if (_allyDisplayScale > _allyTargetScale)
            _allyDisplayScale = System.Math.Max(_allyTargetScale, _allyDisplayScale - DeploySpeed * dt);
    }

    private void CreatePlaceholderCube(GraphicsDevice device)
    {
        var verts = new VertexPositionColor[]
        {
            // Front face (red)
            new(new Vector3(-0.5f, -0.5f,  0.5f), Color.Red),
            new(new Vector3( 0.5f, -0.5f,  0.5f), Color.Red),
            new(new Vector3( 0.5f,  0.5f,  0.5f), Color.DarkRed),
            new(new Vector3(-0.5f,  0.5f,  0.5f), Color.DarkRed),
            // Back face (blue)
            new(new Vector3(-0.5f, -0.5f, -0.5f), Color.Blue),
            new(new Vector3( 0.5f, -0.5f, -0.5f), Color.Blue),
            new(new Vector3( 0.5f,  0.5f, -0.5f), Color.DarkBlue),
            new(new Vector3(-0.5f,  0.5f, -0.5f), Color.DarkBlue),
            // Top face (green)
            new(new Vector3(-0.5f,  0.5f, -0.5f), Color.Green),
            new(new Vector3( 0.5f,  0.5f, -0.5f), Color.Green),
            new(new Vector3( 0.5f,  0.5f,  0.5f), Color.DarkGreen),
            new(new Vector3(-0.5f,  0.5f,  0.5f), Color.DarkGreen),
            // Bottom face (yellow)
            new(new Vector3(-0.5f, -0.5f, -0.5f), Color.Yellow),
            new(new Vector3( 0.5f, -0.5f, -0.5f), Color.Yellow),
            new(new Vector3( 0.5f, -0.5f,  0.5f), Color.DarkGoldenrod),
            new(new Vector3(-0.5f, -0.5f,  0.5f), Color.DarkGoldenrod),
            // Right face (magenta)
            new(new Vector3( 0.5f, -0.5f, -0.5f), Color.Magenta),
            new(new Vector3( 0.5f, -0.5f,  0.5f), Color.Magenta),
            new(new Vector3( 0.5f,  0.5f,  0.5f), Color.DarkMagenta),
            new(new Vector3( 0.5f,  0.5f, -0.5f), Color.DarkMagenta),
            // Left face (cyan)
            new(new Vector3(-0.5f, -0.5f, -0.5f), Color.Cyan),
            new(new Vector3(-0.5f, -0.5f,  0.5f), Color.Cyan),
            new(new Vector3(-0.5f,  0.5f,  0.5f), Color.DarkCyan),
            new(new Vector3(-0.5f,  0.5f, -0.5f), Color.DarkCyan),
        };

        var indices = new short[]
        {
             0, 1, 2,  0, 2, 3,    // front
             5, 4, 7,  5, 7, 6,    // back
             8, 9,10,  8,10,11,    // top
            13,12,15, 13,15,14,    // bottom
            16,17,18, 16,18,19,    // right
            21,20,23, 21,23,22,    // left
        };

        _cubeVB = new VertexBuffer(device, typeof(VertexPositionColor), verts.Length, BufferUsage.WriteOnly);
        _cubeVB.SetData(verts);
        _cubeIB = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _cubeIB.SetData(indices);

        _cubeEffect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
    }

    private void DrawPlaceholderCube(GraphicsDevice device, Matrix view, Matrix projection,
        float scale, Vector3 position)
    {
        if (_cubeVB == null || _cubeIB == null || _cubeEffect == null) return;

        _cubeEffect.View = view;
        _cubeEffect.Projection = projection;
        _cubeEffect.World = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);

        device.SetVertexBuffer(_cubeVB);
        device.Indices = _cubeIB;

        foreach (var pass in _cubeEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);
        }
    }

    public static float FitModelScale(SkeletalModelData model, float targetHeight)
    {
        float modelHeight = model.BoundsMax.Y - model.BoundsMin.Y;
        if (modelHeight <= 0.001f) return 1f;
        return targetHeight / modelHeight;
    }
}
