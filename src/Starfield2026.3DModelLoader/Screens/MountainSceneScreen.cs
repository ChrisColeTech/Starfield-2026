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

        _player.Initialize(new Vector3(0, 0f, 0));
        _player.WorldHalfSize = 200f;
        _camera.Initialize(_player.Position);

        _effect = new BasicEffect(device)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            TextureEnabled = true,
        };
        _effect.EnableDefaultLighting();
        _effect.AmbientLightColor = new Vector3(0.4f, 0.4f, 0.45f);
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, 0.3f));
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

        var rng = new Random(42);
        float mapRadius = 60f;

        // Main mountain at center (scale to ~10 units tall)
        if (loadedModels.TryGetValue("Mountain", out var mountain))
        {
            float scale = 10f / Math.Max(0.1f, mountain.BoundsMax.Y - mountain.BoundsMin.Y);
            _instances.Add((mountain, new Vector3(0, 0, 0), scale, 0f));
        }

        // Rocks scattered (scale to ~1 unit)
        if (loadedModels.TryGetValue("Rock1", out var rock1) && loadedModels.TryGetValue("Rock2", out var rock2))
        {
            float rockScale = 1f / Math.Max(0.1f, Math.Max(rock1.Radius, rock2.Radius));
            for (int i = 0; i < 15; i++)
            {
                float angle = (float)rng.NextDouble() * MathHelper.TwoPi;
                float dist = 5f + (float)rng.NextDouble() * mapRadius * 0.6f;
                var pos = new Vector3(MathF.Sin(angle) * dist, 0, MathF.Cos(angle) * dist);
                var rock = rng.Next(2) == 0 ? rock1 : rock2;
                _instances.Add((rock, pos, rockScale * (0.8f + (float)rng.NextDouble() * 0.4f), (float)rng.NextDouble() * MathHelper.TwoPi));
            }
        }

        // Trees (scale to ~3 units tall)
        if (loadedModels.TryGetValue("Tree", out var tree))
        {
            float treeScale = 3f / Math.Max(0.1f, tree.BoundsMax.Y - tree.BoundsMin.Y);
            for (int i = 0; i < 20; i++)
            {
                float angle = (float)rng.NextDouble() * MathHelper.TwoPi;
                float dist = 8f + (float)rng.NextDouble() * mapRadius * 0.7f;
                var pos = new Vector3(MathF.Sin(angle) * dist, 0, MathF.Cos(angle) * dist);
                _instances.Add((tree, pos, treeScale * (0.8f + (float)rng.NextDouble() * 0.4f), (float)rng.NextDouble() * MathHelper.TwoPi));
            }
        }

        // Bushes (scale to ~0.8 units)
        if (loadedModels.TryGetValue("Bush", out var bush))
        {
            float bushScale = 0.8f / Math.Max(0.1f, bush.Radius);
            for (int i = 0; i < 15; i++)
            {
                float angle = (float)rng.NextDouble() * MathHelper.TwoPi;
                float dist = 4f + (float)rng.NextDouble() * mapRadius * 0.5f;
                var pos = new Vector3(MathF.Sin(angle) * dist, 0, MathF.Cos(angle) * dist);
                _instances.Add((bush, pos, 0.4f + (float)rng.NextDouble() * 0.2f, (float)rng.NextDouble() * MathHelper.TwoPi));
            }
        }

        // Grass patches
        if (loadedModels.TryGetValue("Grass", out var grass))
        {
            for (int i = 0; i < 25; i++)
            {
                float angle = (float)rng.NextDouble() * MathHelper.TwoPi;
                float dist = 3f + (float)rng.NextDouble() * mapRadius * 0.6f;
                var pos = new Vector3(MathF.Sin(angle) * dist, 0, MathF.Cos(angle) * dist);
                _instances.Add((grass, pos, 0.3f + (float)rng.NextDouble() * 0.15f, (float)rng.NextDouble() * MathHelper.TwoPi));
            }
        }

        // Flowers
        if (loadedModels.TryGetValue("Flower", out var flower))
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = (float)rng.NextDouble() * MathHelper.TwoPi;
                float dist = 5f + (float)rng.NextDouble() * mapRadius * 0.4f;
                var pos = new Vector3(MathF.Sin(angle) * dist, 0, MathF.Cos(angle) * dist);
                _instances.Add((flower, pos, 0.25f + (float)rng.NextDouble() * 0.1f, (float)rng.NextDouble() * MathHelper.TwoPi));
            }
        }

        // Pebbles
        if (loadedModels.TryGetValue("Pebbles", out var pebbles))
        {
            for (int i = 0; i < 10; i++)
            {
                float angle = (float)rng.NextDouble() * MathHelper.TwoPi;
                float dist = 2f + (float)rng.NextDouble() * mapRadius * 0.5f;
                var pos = new Vector3(MathF.Sin(angle) * dist, 0, MathF.Cos(angle) * dist);
                _instances.Add((pebbles, pos, 0.2f + (float)rng.NextDouble() * 0.1f, (float)rng.NextDouble() * MathHelper.TwoPi));
            }
        }

        ModelLoaderLog.Info($"[MountainScene] Placed {_instances.Count} instances");
        StatusText = $"Loaded Mountain scene ({_instances.Count} objects)";
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

        _player.SetTerrainHeight(0f);

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

    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(70, 90, 70));
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.AlphaBlend;
        device.SamplerStates[0] = SamplerState.LinearWrap;

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
