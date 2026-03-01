#nullable enable
using System.IO;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Controllers;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Input;
using System;
using Starfield2026.ModelLoader.Loaders;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Screens;

public class MountainSceneScreen
{
    private GraphicsDevice _device = null!;
    private List<FbxModel> _models = new();
    private List<(FbxModel model, Vector3 position, float scale, float rotation)> _instances = new();
    private BasicEffect? _effect;
    private QuadrantGridRenderer _grid = null!;
    private CubeRenderer _cubeRenderer = null!;
    private OverworldCharacter? _character;
    private PlayerController _player = new();
    private FollowCamera _camera = new();
    private readonly List<(Vector3 pos, float size, Color color)> _stars = new();
    private string? _activeModelName;
    private Vector3 _activeModelPos;
    private float _activeModelScale;
    private float _climbRadius;
    private float _climbMaxHeight;

    public AnimationLoadMode LoadMode { get; set; } = AnimationLoadMode.FillMissing;
    public HashSet<string> FillTags { get; set; } = new() { "Jump", "Land" };
    public Dictionary<string, string> SharedAnimationFolders { get; set; } = new();
    public Dictionary<string, string?[]> TrainerParties { get; set; } = new();
    public string PokemonRoot { get; set; } = "";

    public string StatusText { get; private set; } = "No scene loaded";
    public Vector3 Position => _player.Position;
    public float Yaw => _player.Yaw;

    public void Initialize(GraphicsDevice device)
    {
        _device = device;

        _grid = new QuadrantGridRenderer
        {
            Spacing = 2f,
            GridHalfSize = 40,
            PlaneOffset = 0.01f,
        };
        _grid.Initialize(device);

        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);

        _player.Initialize(new Vector3(0, 0f, -12f));
        _player.WorldHalfSize = 200f;
        _camera.Initialize(_player.Position);

        _effect = new BasicEffect(device)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            TextureEnabled = true,
        };

        // Tone lighting down so rock texture isn't washed out to white
        _effect.AmbientLightColor = new Vector3(0.15f, 0.15f, 0.18f);

        _effect.DirectionalLight0.Enabled = true;
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, 0.3f));
        _effect.DirectionalLight0.DiffuseColor = new Vector3(0.7f, 0.65f, 0.6f);
        _effect.DirectionalLight0.SpecularColor = new Vector3(0.1f, 0.1f, 0.1f);

        _effect.DirectionalLight1.Enabled = true;
        _effect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(0.5f, -0.3f, -0.5f));
        _effect.DirectionalLight1.DiffuseColor = new Vector3(0.15f, 0.18f, 0.25f);
        _effect.DirectionalLight1.SpecularColor = Vector3.Zero;

        _effect.DirectionalLight2.Enabled = false;

        BuildStarBackdrop();
    }

    private void BuildStarBackdrop()
    {
        _stars.Clear();
        var rng = new Random(1337);

        for (int i = 0; i < 220; i++)
        {
            float az = (float)rng.NextDouble() * MathHelper.TwoPi;
            float el = MathHelper.Lerp(0.15f, 1.05f, (float)rng.NextDouble());
            float dist = MathHelper.Lerp(140f, 260f, (float)rng.NextDouble());

            var pos = new Vector3(
                MathF.Cos(az) * MathF.Cos(el) * dist,
                MathF.Sin(el) * dist,
                MathF.Sin(az) * MathF.Cos(el) * dist);

            float size = MathHelper.Lerp(0.04f, 0.12f, (float)rng.NextDouble());
            byte tint = (byte)rng.Next(220, 255);
            var color = new Color(tint, tint, 255);
            _stars.Add((pos, size, color));
        }
    }

    public void LoadScene(string folderPath)
    {
        string modelsPath = Path.Combine(folderPath, "Models");

        var modelFiles = new Dictionary<string, string>
        {
            ["Mountain"] = Path.Combine(modelsPath, "Mountain01.fbx"),
            ["Rock1"] = Path.Combine(modelsPath, "Rock01.fbx"),
            ["Rock2"] = Path.Combine(modelsPath, "Rock02.fbx"),
            ["Tree"] = Path.Combine(modelsPath, "Tree01.fbx"),
            ["Bush"] = Path.Combine(modelsPath, "Bush01.fbx"),
            ["Grass"] = Path.Combine(modelsPath, "Grass01.fbx"),
            ["Flower"] = Path.Combine(modelsPath, "Flower01.fbx"),
            ["Flowers"] = Path.Combine(modelsPath, "Flowers01.fbx"),
            ["Pebbles"] = Path.Combine(modelsPath, "Pebbles01.fbx"),
            ["Bridge"] = Path.Combine(modelsPath, "Bridge01.fbx"),
        };

        var loadedModels = new Dictionary<string, FbxModel>();

        foreach (var kvp in modelFiles)
        {
            if (File.Exists(kvp.Value))
            {
                var model = new FbxModel();
                model.Load(_device, kvp.Value);
                if (model.IsLoaded)
                {
                    loadedModels[kvp.Key] = model;
                    _models.Add(model);
                }
            }
        }

        ModelLoaderLog.Info($"[MountainScene] Loaded {loadedModels.Count} models");

        // One-thing-at-a-time debug mode: place exactly one model.
        // Priority order keeps testing deterministic.
        string[] priority =
        {
            "Mountain", "Rock1", "Rock2", "Tree", "Bush",
            "Grass", "Flower", "Flowers", "Pebbles", "Bridge"
        };

        FbxModel? selected = null;
        string? selectedName = null;
        foreach (string key in priority)
        {
            if (loadedModels.TryGetValue(key, out var model))
            {
                selected = model;
                selectedName = key;
                break;
            }
        }

        if (selected != null && selectedName != null)
        {
            float modelHeight = Math.Max(0.1f, selected.BoundsMax.Y - selected.BoundsMin.Y);
            float targetHeight = selectedName == "Mountain" ? 10f : 2.5f;
            float scale = targetHeight / modelHeight;
            var modelPos = new Vector3(0f, 0f, 20f);
            _instances.Add((selected, modelPos, scale, 0f));

            _activeModelName = selectedName;
            _activeModelPos = modelPos;
            _activeModelScale = scale;

            if (selectedName == "Mountain")
            {
                _climbRadius = Math.Max(4f, selected.Radius * scale * 0.95f);
                _climbMaxHeight = Math.Max(2f, (selected.BoundsMax.Y - selected.BoundsMin.Y) * scale * 0.85f);
            }
            else
            {
                _climbRadius = 0f;
                _climbMaxHeight = 0f;
            }

            ModelLoaderLog.Info(
                $"[MountainScene] One-model mode: {selectedName}, scale={scale:F3}, boundsMin={selected.BoundsMin}, boundsMax={selected.BoundsMax}, radius={selected.Radius:F3}");
            StatusText = $"One-model mode: {selectedName} (scale {scale:F2})";
        }
        else
        {
            StatusText = "No FBX models found in Mountain/Models";
        }
    }

    public void LoadCharacter(string folderPath)
    {
        try
        {
            var animSet = AnimationSetLoader.Load(
                folderPath,
                resolveSharedFolder: (path, skel) =>
                    TrainerGender.IsTrainerFolder(path)
                        ? SharedAnimationResolver.Resolve(path, skel, SharedAnimationFolders)
                        : null,
                loadMode: LoadMode,
                fillTags: FillTags);

            _character ??= new OverworldCharacter();
            _character.Load(_device, animSet);

            string? pokeballPath = FindPokeballModel(folderPath);
            if (pokeballPath != null)
                _character.LoadPokeball(_device, pokeballPath);

            if (!string.IsNullOrEmpty(PokemonRoot))
            {
                var partyPaths = TrainerPartyAssignment.ResolveParty(folderPath, PokemonRoot, TrainerParties);
                if (partyPaths != null)
                    _character.LoadParty(_device, partyPaths);
            }

            StatusText = $"Loaded: {Path.GetFileName(folderPath)}";
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[MountainScene] Failed to load character: {ex.Message}");
            _character?.Dispose();
            _character = null;
            StatusText = $"Failed: {Path.GetFileName(folderPath)}";
        }
    }

    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _player.Update(dt, input);

        if (_character?.FacingOverride is float facing)
            _player.SetFacingCamera(facing);
        else if (_player.IsMovingBackward)
            _player.SetFacingCamera(_camera.SmoothedYaw);

        _player.SetTerrainHeight(SampleTerrainHeight(_player.Position));

        ResolveModelCollisions();

        _character?.Update(dt, _player.IsMoving, _player.IsRunning, _player.IsGrounded, input,
            _player.Position, _player.Yaw);

        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        float pokemonHeight = _character?.DeployedPokemonHeight ?? 0f;
        bool pokemonOut = _character?.Party is { IsDeployed: true };

        if (pokemonOut && _character != null)
        {
            var pokePos = _character.DeployedPokemonPosition;
            float collisionRadius = Math.Max(0.8f, pokemonHeight * 0.4f);
            float dx = _player.Position.X - pokePos.X;
            float dz = _player.Position.Z - pokePos.Z;
            float distSq = dx * dx + dz * dz;
            if (distSq < collisionRadius * collisionRadius && distSq > 0.0001f)
            {
                float dist = MathF.Sqrt(distSq);
                _player.SetPosition(
                    new Vector3(pokePos.X + dx / dist * collisionRadius, 0, pokePos.Z + dz / dist * collisionRadius),
                    _player.Yaw);
            }
        }

        _camera.Update(dt, aspect, _player.Position, _player.Yaw, _player.Speed,
            _player.IsRunning, _player.IsMovingBackward, pokemonOut ? pokemonHeight : 0f,
            input.CameraYaw, input.CameraPitch, input.CameraZoom);
    }

    private void ResolveModelCollisions()
    {
        if (_instances.Count == 0)
            return;

        var playerPos = _player.Position;
        bool corrected = false;
        const float playerRadius = 0.6f;

        foreach (var (model, position, scale, _) in _instances)
        {
            if (!model.IsLoaded)
                continue;

            // Allow climbing the active mountain model instead of hard-blocking it.
            if (_activeModelName == "Mountain" && position == _activeModelPos)
                continue;

            float colliderRadius = Math.Max(0.75f, model.Radius * scale * 0.6f);

            float dx = playerPos.X - position.X;
            float dz = playerPos.Z - position.Z;
            float distSq = dx * dx + dz * dz;
            float minDist = playerRadius + colliderRadius;
            float minDistSq = minDist * minDist;

            if (distSq < minDistSq && distSq > 0.0001f)
            {
                float dist = MathF.Sqrt(distSq);
                float nx = dx / dist;
                float nz = dz / dist;

                playerPos = new Vector3(
                    position.X + nx * minDist,
                    playerPos.Y,
                    position.Z + nz * minDist);
                corrected = true;
            }
        }

        if (corrected)
            _player.SetPosition(playerPos, _player.Yaw);
    }

    private float SampleTerrainHeight(Vector3 worldPos)
    {
        float y = 0f;

        // Raycast against actual mesh geometry for the mountain
        foreach (var (model, position, scale, rotation) in _instances)
        {
            if (!model.IsLoaded) continue;

            var world = Matrix.CreateScale(scale)
                * Matrix.CreateRotationY(rotation)
                * Matrix.CreateTranslation(position);

            float? meshY = model.SampleHeight(worldPos, world);
            if (meshY.HasValue && meshY.Value > y)
                y = meshY.Value;
        }

        return y;
    }

    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(8, 12, 28));
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
        device.SamplerStates[0] = SamplerState.LinearWrap;

        // Draw stars with alpha blending first
        device.BlendState = BlendState.AlphaBlend;
        DrawStars(device);
        device.BlendState = BlendState.Opaque;

        _grid.Draw(device, _camera.View, _camera.Projection);

        DrawModels(device);

        var pos = _player.Position;
        float yaw = _player.Yaw;

        _cubeRenderer.Draw(device, _camera.View, _camera.Projection,
            new Vector3(pos.X, 0.02f, pos.Z),
            yaw, new Vector3(1.2f, 0.02f, 1.2f), Color.Black * 0.3f);

        if (_character is { IsLoaded: true })
        {
            _character.Draw(device, _camera.View, _camera.Projection, pos, yaw);
        }
        else
        {
            var cubePos = new Vector3(pos.X, 0.75f, pos.Z);
            _cubeRenderer.Draw(device, _camera.View, _camera.Projection, cubePos, yaw, 1.2f, new Color(0, 255, 255));
        }
    }

    private void DrawModels(GraphicsDevice device)
    {
        if (_effect == null) return;

        var prevRaster = device.RasterizerState;
        device.RasterizerState = RasterizerState.CullNone;

        _effect.View = _camera.View;
        _effect.Projection = _camera.Projection;

        var camPos = _camera.Position;
        float cullDist = 100f;

        foreach (var (model, position, scale, rotation) in _instances)
        {
            if (!model.IsLoaded) continue;

            float distSq = Vector3.DistanceSquared(position, camPos);
            if (distSq > cullDist * cullDist) continue;

            var world = Matrix.CreateScale(scale)
                * Matrix.CreateRotationY(rotation)
                * Matrix.CreateTranslation(position);

            model.Draw(device, _effect, world);
        }

        device.RasterizerState = prevRaster;
    }

    private void DrawStars(GraphicsDevice device)
    {
        foreach (var (pos, size, color) in _stars)
        {
            _cubeRenderer.Draw(
                device,
                _camera.View,
                _camera.Projection,
                pos,
                0f,
                new Vector3(size, size, size),
                color);
        }
    }

    private static string? FindPokeballModel(string characterFolderPath)
    {
        string? dir = characterFolderPath;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            dir = Path.GetDirectoryName(dir);
            if (dir == null) break;
            string candidate = Path.Combine(dir, "Items", "Pokeballs", "ob0201_00", "model.dae");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var model in _models)
            model.Dispose();
        _models.Clear();
        _instances.Clear();
        _effect?.Dispose();
        _character?.Dispose();
    }
}
