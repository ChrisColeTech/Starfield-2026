#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Controllers;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Screens;

public class TerritoriesScreen
{
    private GraphicsDevice _device = null!;
    private RegionMap _map = null!;
    private TexturedTileRenderer _tileRenderer = null!;
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

    public int MapWidth { get; set; } = 60;
    public int MapDepth { get; set; } = 60;
    public int MapSeed { get; set; } = 42;

    public string StatusText { get; private set; } = "No character loaded";
    public Vector3 Position => _player.Position;
    public float Yaw => _player.Yaw;

    public void Initialize(GraphicsDevice device)
    {
        _device = device;

        _map = new RegionMap(MapWidth, MapDepth, cellSize: 2f);
        _map.GenerateProcedural(MapSeed);

        _tileRenderer = new TexturedTileRenderer();

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
    }

    public void LoadTerrain(string textureFolder, string _)
    {
        _tileRenderer.Initialize(_device, textureFolder);
        ModelLoaderLog.Info($"[Territories] Loaded terrain tiles");
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

            StatusText = $"Loaded: {System.IO.Path.GetFileName(folderPath)}";
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[Territories] Failed to load character: {ex.Message}");
            _character?.Dispose();
            _character = null;
            StatusText = $"Failed: {System.IO.Path.GetFileName(folderPath)}";
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
        device.Clear(new Color(30, 40, 50));
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.AlphaBlend;
        device.SamplerStates[0] = SamplerState.LinearWrap;

        _tileRenderer.Draw(device, _camera.View, _camera.Projection, _map, _camera.Position);
        _grid.Draw(device, _camera.View, _camera.Projection);

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

    private static string? FindPokeballModel(string characterFolderPath)
    {
        string? dir = characterFolderPath;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            dir = System.IO.Path.GetDirectoryName(dir);
            if (dir == null) break;
            string candidate = System.IO.Path.Combine(dir, "Items", "Pokeballs", "ob0201_00", "model.dae");
            if (System.IO.File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    public void Dispose()
    {
        _tileRenderer?.Dispose();
        _character?.Dispose();
    }
}
