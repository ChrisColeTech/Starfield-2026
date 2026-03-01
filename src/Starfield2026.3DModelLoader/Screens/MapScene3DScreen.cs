#nullable enable
using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Controllers;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Loaders;
using Starfield2026.ModelLoader.Maps;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Screens;

/// <summary>
/// Map-driven 3D scene. Parses a .g.cs MapDefinition + TileRegistry.cs,
/// resolves tiles with ModelId to FbxModel instances, places them on a grid.
/// Standalone — no dependency on Starfield2026.Core.
/// </summary>
public class MapScene3DScreen
{
    private GraphicsDevice _device = null!;
    private readonly Dictionary<string, FbxModel> _modelCache = new();
    private readonly List<(FbxModel model, Vector3 position, float scale, float rotation)> _instances = new();
    private BasicEffect? _effect;
    private QuadrantGridRenderer _grid = null!;
    private CubeRenderer _cubeRenderer = null!;
    private OverworldCharacter? _character;
    private PlayerController _player = new();
    private FollowCamera _camera = new();
    private readonly List<(Vector3 pos, float size, Color color)> _stars = new();
    private MapData3D? _mapData;
    private Dictionary<int, Tile3D> _tileRegistry = new();

    public AnimationLoadMode LoadMode { get; set; } = AnimationLoadMode.FillMissing;
    public HashSet<string> FillTags { get; set; } = new() { "Jump", "Land" };
    public Dictionary<string, string> SharedAnimationFolders { get; set; } = new();
    public Dictionary<string, string?[]> TrainerParties { get; set; } = new();
    public string PokemonRoot { get; set; } = "";

    public string StatusText { get; private set; } = "No map loaded";
    public Vector3 Position => _player.Position;
    public float Yaw => _player.Yaw;

    public void Initialize(GraphicsDevice device)
    {
        _device = device;

        _grid = new QuadrantGridRenderer
        {
            Spacing = 2f,
            GridHalfSize = 60,
            PlaneOffset = 0.01f,
        };
        _grid.Initialize(device);

        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);

        _player.Initialize(Vector3.Zero);
        _player.WorldHalfSize = 400f;
        _camera.Initialize(_player.Position);
        _camera.TerrainHeightSampler = SampleTerrainHeight;

        _effect = new BasicEffect(device)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            TextureEnabled = true,
        };

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

    /// <summary>
    /// Load a TileRegistry.cs file, then a .g.cs map file, and place models.
    /// </summary>
    public void LoadMap(string registryPath, string mapPath, string fbxModelsFolder)
    {
        _instances.Clear();
        _tileRegistry = MapParser.LoadRegistryFile(registryPath);
        ModelLoaderLog.Info($"[MapScene3D] Loaded {_tileRegistry.Count} tile definitions from registry");

        _mapData = MapParser.LoadMapFile(mapPath);
        ModelLoaderLog.Info($"[MapScene3D] Loaded map '{_mapData.DisplayName}' ({_mapData.Width}x{_mapData.Height})");

        int placed = 0;
        Vector3 spawnPos = Vector3.Zero;
        bool foundSpawn = false;

        for (int y = 0; y < _mapData.Height; y++)
        {
            for (int x = 0; x < _mapData.Width; x++)
            {
                int tileId = _mapData.GetBaseTile(x, y);
                PlaceTileModel(tileId, x, y, fbxModelsFolder, ref placed);

                // Check for spawn by name convention
                if (_tileRegistry.TryGetValue(tileId, out var tileDef) &&
                    tileDef.Name.Contains("Spawn", StringComparison.OrdinalIgnoreCase) && !foundSpawn)
                {
                    spawnPos = TileToWorld(x, y);
                    foundSpawn = true;
                }

                // Overlay
                int? overlayId = _mapData.GetOverlayTile(x, y);
                if (overlayId.HasValue)
                    PlaceTileModel(overlayId.Value, x, y, fbxModelsFolder, ref placed);
            }
        }

        // Default spawn at center if none found
        if (!foundSpawn)
            spawnPos = TileToWorld(_mapData.Width / 2, _mapData.Height / 2);

        _player.Initialize(spawnPos);
        _camera.Initialize(spawnPos);
        _camera.TerrainHeightSampler = SampleTerrainHeight;

        StatusText = $"Map '{_mapData.DisplayName}' — {_mapData.Width}x{_mapData.Height}, {placed} models";
        ModelLoaderLog.Info($"[MapScene3D] {StatusText}, cached models: {_modelCache.Count}");
    }

    private void PlaceTileModel(int tileId, int x, int y, string fbxModelsFolder, ref int placed)
    {
        if (!_tileRegistry.TryGetValue(tileId, out var tileDef)) return;
        if (string.IsNullOrEmpty(tileDef.ModelId)) return;

        var model = LoadOrGetModel(fbxModelsFolder, tileDef.ModelId!);
        if (model == null) return;

        float modelHeight = Math.Max(0.1f, model.BoundsMax.Y - model.BoundsMin.Y);
        float targetHeight = Math.Max(1f, tileDef.Height);
        float scale = targetHeight / modelHeight;

        Vector3 worldPos = TileToWorld(x, y);
        _instances.Add((model, worldPos, scale, 0f));
        placed++;
    }

    private FbxModel? LoadOrGetModel(string modelsPath, string modelId)
    {
        if (_modelCache.TryGetValue(modelId, out var cached))
            return cached;

        string fbxPath = Path.Combine(modelsPath, $"{modelId}.fbx");
        if (!File.Exists(fbxPath))
        {
            ModelLoaderLog.Info($"[MapScene3D] FBX not found: {fbxPath}");
            return null;
        }

        var model = new FbxModel();
        model.Load(_device, fbxPath);
        if (!model.IsLoaded)
        {
            model.Dispose();
            return null;
        }

        _modelCache[modelId] = model;
        return model;
    }

    private Vector3 TileToWorld(int x, int y)
    {
        int tileSize = _mapData?.TileSize ?? 1;
        float worldX = x * tileSize;
        float worldZ = y * tileSize;
        return new Vector3(worldX, 0f, worldZ);
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
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[MapScene3D] Failed to load character: {ex.Message}");
            _character?.Dispose();
            _character = null;
        }
    }

    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _player.Update(dt, input);

        if (_character?.FacingOverride is float facing)
            _player.SetFacingCamera(facing);
        else if (_player.IsMovingBackward && !_player.HasHorizontalInput)
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
        if (_instances.Count == 0) return;

        var playerPos = _player.Position;
        bool corrected = false;
        const float playerRadius = 0.6f;

        foreach (var (model, position, scale, _) in _instances)
        {
            if (!model.IsLoaded) continue;

            // If mesh has height at this point, it's climbable — skip collision
            var world = Matrix.CreateScale(scale)
                * Matrix.CreateRotationY(0f)
                * Matrix.CreateTranslation(position);
            float? meshY = model.SampleHeight(position, world);
            if (meshY.HasValue && meshY.Value > 0.1f) continue;

            float colliderRadius = Math.Max(0.75f, model.Radius * scale * 0.6f);
            float dx = playerPos.X - position.X;
            float dz = playerPos.Z - position.Z;
            float distSq = dx * dx + dz * dz;
            float minDist = playerRadius + colliderRadius;

            if (distSq < minDist * minDist && distSq > 0.0001f)
            {
                float dist = MathF.Sqrt(distSq);
                playerPos = new Vector3(
                    position.X + dx / dist * minDist,
                    playerPos.Y,
                    position.Z + dz / dist * minDist);
                corrected = true;
            }
        }

        if (corrected)
            _player.SetPosition(playerPos, _player.Yaw);
    }

    private float SampleTerrainHeight(Vector3 worldPos)
    {
        float y = 0f;
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
        float cullDist = 120f;

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

    private void BuildStarBackdrop()
    {
        _stars.Clear();
        var rng = new Random(42);
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
            _stars.Add((pos, size, new Color(tint, tint, 255)));
        }
    }

    private void DrawStars(GraphicsDevice device)
    {
        foreach (var (pos, size, color) in _stars)
        {
            _cubeRenderer.Draw(device, _camera.View, _camera.Projection,
                pos, 0f, new Vector3(size, size, size), color);
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
        foreach (var model in _modelCache.Values)
            model.Dispose();
        _modelCache.Clear();
        _instances.Clear();
        _effect?.Dispose();
        _character?.Dispose();
    }
}
