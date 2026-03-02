#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Controllers;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Input;

namespace Starfield2026.ModelLoader.Runtime;

public sealed class OverworldActorRuntime : IDisposable
{
    private GraphicsDevice _device = null!;
    private OverworldCharacter? _character;
    public float GroundOffset { get; set; } = 0.12f;

    public bool IsLoaded => _character is { IsLoaded: true };
    public float CameraTargetHeight(float fallback) => _character?.Party is { IsDeployed: true } ? _character.DeployedPokemonHeight : fallback;
    public string PartyStatusText => _character?.PartyStatusText ?? string.Empty;

    public void Initialize(GraphicsDevice device) => _device = device;

    public bool LoadCharacter(
        string folderPath,
        AnimationLoadMode loadMode,
        HashSet<string> fillTags,
        Dictionary<string, string> sharedAnimationFolders,
        string pokemonRoot,
        Dictionary<string, string?[]> trainerParties,
        string logPrefix)
    {
        try
        {
            var animSet = AnimationSetLoader.Load(
                folderPath,
                resolveSharedFolder: (path, skel) => TrainerGender.IsTrainerFolder(path)
                    ? SharedAnimationResolver.Resolve(path, skel, sharedAnimationFolders)
                    : null,
                loadMode: loadMode,
                fillTags: fillTags);

            _character ??= new OverworldCharacter();
            _character.Load(_device, animSet);

            string? pokeballPath = FindPokeballModel(folderPath);
            if (pokeballPath != null)
                _character.LoadPokeball(_device, pokeballPath);

            if (!string.IsNullOrEmpty(pokemonRoot))
            {
                var partyPaths = TrainerPartyAssignment.ResolveParty(folderPath, pokemonRoot, trainerParties);
                if (partyPaths != null)
                    _character.LoadParty(_device, partyPaths);
            }

            return true;
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[{logPrefix}] Failed to load character: {ex.Message}");
            _character?.Dispose();
            _character = null;
            return false;
        }
    }

    public void Update(float dt, InputSnapshot input, PlayerController player, float cameraSmoothedYaw)
    {
        if (_character?.FacingOverride is float facing)
            player.SetFacingCamera(facing);
        else if (player.IsMovingBackward && !player.HasHorizontalInput)
            player.SetFacingCamera(cameraSmoothedYaw);

        _character?.Update(dt, player.IsMoving, player.IsRunning, player.IsGrounded, input, player.Position, player.Yaw);
        ResolvePokemonCollision(player);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection, Vector3 pos, float yaw)
    {
        _character?.Draw(device, view, projection, new Vector3(pos.X, pos.Y + GroundOffset, pos.Z), yaw);
    }

    public void Dispose()
    {
        _character?.Dispose();
        _character = null;
    }

    private void ResolvePokemonCollision(PlayerController player)
    {
        if (_character?.Party is not { IsDeployed: true })
            return;

        var pokePos = _character.DeployedPokemonPosition;
        float radius = Math.Max(0.8f, _character.DeployedPokemonHeight * 0.4f);
        float dx = player.Position.X - pokePos.X;
        float dz = player.Position.Z - pokePos.Z;
        float distSq = dx * dx + dz * dz;
        if (distSq >= radius * radius || distSq <= 0.0001f)
            return;

        float dist = MathF.Sqrt(distSq);
        var p = new Vector3(pokePos.X + dx / dist * radius, player.Position.Y, pokePos.Z + dz / dist * radius);
        player.SetPosition(p, player.Yaw);
    }

    private static string? FindPokeballModel(string characterFolderPath)
    {
        string? dir = characterFolderPath;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            dir = Path.GetDirectoryName(dir);
            if (dir == null) break;
            string candidate = Path.Combine(dir, "Items", "Pokeballs", "ob0201_00", "model.dae");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
