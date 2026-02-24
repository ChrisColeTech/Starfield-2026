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

public class FreeRoamScreen
{
    private GraphicsDevice _device = null!;
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

    public Vector3 Position => _player.Position;
    public float Yaw => _player.Yaw;
    public string StatusText { get; private set; } = "No model loaded";

    public void Initialize(GraphicsDevice device)
    {
        _device = device;

        _grid = new QuadrantGridRenderer
        {
            Spacing = 2f,
            GridHalfSize = 250,
            PlaneOffset = 0f,
        };
        _grid.Initialize(device);

        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);

        _player.Initialize(new Vector3(0, 0f, 0));
        _player.WorldHalfSize = 500f;
        _camera.Initialize(_player.Position);
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

            // Load pokeball model if available
            string? pokeballPath = FindPokeballModel(folderPath);
            if (pokeballPath != null)
                _character.LoadPokeball(_device, pokeballPath);

            // Load Pokemon party if assigned
            if (!string.IsNullOrEmpty(PokemonRoot))
            {
                var partyPaths = TrainerPartyAssignment.ResolveParty(folderPath, PokemonRoot, TrainerParties);
                if (partyPaths != null)
                    _character.LoadParty(_device, partyPaths);
            }

            string partyInfo = _character.PartyStatusText;
            StatusText = $"Loaded: {System.IO.Path.GetFileName(folderPath)} ({animSet.ClipsByTag.Count} tags) {partyInfo}";
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[FreeRoam] Failed to load character: {ex.Message}");
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
        else if (_player.IsMovingBackward)
            _player.SetFacingCamera(_camera.SmoothedYaw);

        _character?.Update(dt, _player.IsMoving, _player.IsRunning, _player.IsGrounded, input,
            _player.Position, _player.Yaw);

        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        bool pokemonOut = _character?.Party is { IsDeployed: true };
        _camera.Update(dt, aspect, _player.Position, _player.Yaw, _player.Speed,
            _player.IsRunning, _player.IsMovingBackward, pokemonOut,
            input.CameraYaw, input.CameraPitch, input.CameraZoom);
    }

    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(20, 25, 50));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
        device.SamplerStates[0] = SamplerState.AnisotropicClamp;

        _grid.Draw(device, _camera.View, _camera.Projection);

        var pos = _player.Position;
        float yaw = _player.Yaw;

        // Shadow
        _cubeRenderer.Draw(device, _camera.View, _camera.Projection,
            new Vector3(pos.X, 0.05f, pos.Z),
            yaw, new Vector3(1.5f, 0.05f, 1.5f), Color.Black * 0.4f);

        // Character or fallback cube
        if (_character is { IsLoaded: true })
        {
            _character.Draw(device, _camera.View, _camera.Projection, pos, yaw);
        }
        else
        {
            var cubePos = new Vector3(pos.X, pos.Y + 0.75f, pos.Z);
            _cubeRenderer.Draw(device, _camera.View, _camera.Projection, cubePos, yaw, 1.5f, new Color(0, 220, 255));
        }
    }

    private static string? FindPokeballModel(string characterFolderPath)
    {
        // Walk up from character folder to find Models/Items/Pokeballs/ob0201_00/model.dae
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
}
