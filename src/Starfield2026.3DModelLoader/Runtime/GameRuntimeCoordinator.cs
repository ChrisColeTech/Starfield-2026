#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Maps;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Runtime;

public sealed class GameRuntimeCoordinator
{
    private const string LastModeSettingKey = "last_mode";

    public void DeferredInitialize(GameRuntimeState s, GraphicsDevice device)
    {
        if (s.PendingAssetsRoot == null)
            return;

        string assetsRoot = s.PendingAssetsRoot;
        s.PendingAssetsRoot = null;
        string modelsRoot = Path.Combine(assetsRoot, "Models");
        var entries = ManifestScanner.Scan(modelsRoot);

        int dbCount = s.Database.GetCharacterCount();
        if (dbCount != entries.Count)
            s.Database.RebuildCharacters(entries);

        s.Characters = s.Database.GetAllCharacters();
        SharedAnimationResolver.ScanFolders(Path.Combine(assetsRoot, "Models", "SharedAnimations"), s.FreeRoam.SharedAnimationFolders);
        s.FreeRoam.PokemonRoot = Path.Combine(assetsRoot, "Models", "Pokemon");
        s.FreeRoam.TrainerParties = TrainerPartyAssignment.LoadFromJson(Path.Combine(assetsRoot, "trainer_parties.json"));
        PokemonSlot.LoadGenScales(Path.Combine(assetsRoot, "pokemon_gen_scales.json"));

        ApplySavedAnimationSettings(s);
        s.MapsFolder = Path.Combine(assetsRoot, "Models", "Maps");
        s.SharedTileCache = new TileModelCache();

        s.MapScreen.SetTileCache(s.SharedTileCache);
        s.AnimeScreen.SetTileCache(s.SharedTileCache);
        s.AnimeWorldScreen.SetTileCache(s.SharedTileCache);
        MapCatalog.LoadAllMaps();
        CopySharedSettings(s);
        string? savedMapId = s.Database.GetSetting("last_map_id");
        string? savedAnimeMapId = s.Database.GetSetting("last_anime_map_id");
        s.MapScreen.LoadFromAssets(assetsRoot, savedMapId);
        s.AnimeScreen.LoadAnimeModels(Path.Combine(assetsRoot, "Models", "Maps", "Anime Trees"), savedAnimeMapId);
        s.AnimeWorldScreen.LoadWorld();

        RebuildTileCacheForScreen(s, s.ScreenMode, device, preloadBlocking: true);
        if (s.Characters.Count > 0)
        {
            s.CharacterIndex = ResolveInitialCharacterIndex(s);
            LoadCharacterForCurrentMode(s);
        }

        s.Initialized = true;
    }

    public void RebuildTileCacheForScreen(GameRuntimeState s, ScreenMode mode, GraphicsDevice device, bool preloadBlocking)
    {
        if (s.SharedTileCache == null || string.IsNullOrWhiteSpace(s.MapsFolder))
            return;

        if (mode == ScreenMode.AnimeWorld)
        {
            if (s.AnimeWorldScreen.Maps.Count == 0) return;
            s.SharedTileCache.BuildForMaps(s.AnimeWorldScreen.Maps, s.MapsFolder);
            if (preloadBlocking)
                s.SharedTileCache.LoadQueuedBlocking(device);
            return;
        }

        MapDefinition? map = mode switch
        {
            ScreenMode.Map => s.MapScreen.CurrentMap,
            ScreenMode.AnimeModels => s.AnimeScreen.CurrentMap,
            _ => null,
        };
        if (map == null)
            return;

        s.SharedTileCache.BuildForMap(map, s.MapsFolder);
        if (preloadBlocking)
            s.SharedTileCache.LoadQueuedBlocking(device);
    }

    public void LoadCharacterForCurrentMode(GameRuntimeState s)
    {
        if (s.CharacterIndex < 0 || s.CharacterIndex >= s.Characters.Count)
            return;
        string folder = Path.GetDirectoryName(s.Characters[s.CharacterIndex].ManifestPath) ?? "";
        if (s.ScreenMode == ScreenMode.Map) s.MapScreen.LoadCharacter(folder);
        else if (s.ScreenMode == ScreenMode.AnimeModels) s.AnimeScreen.LoadCharacter(folder);
        else if (s.ScreenMode == ScreenMode.AnimeWorld) s.AnimeWorldScreen.LoadCharacter(folder);
        else s.FreeRoam.LoadCharacter(folder);
    }

    public void PersistCurrentMode(GameRuntimeState s)
    {
        string modeStr = s.ScreenMode switch
        {
            ScreenMode.Map => "map",
            ScreenMode.AnimeModels => "anime",
            ScreenMode.AnimeWorld => "animeworld",
            _ => "freeroam"
        };
        s.Database.SetSetting(LastModeSettingKey, modeStr);
    }

    private static void CopySharedSettings(GameRuntimeState s)
    {
        s.MapScreen.SharedAnimationFolders = s.FreeRoam.SharedAnimationFolders;
        s.MapScreen.TrainerParties = s.FreeRoam.TrainerParties;
        s.MapScreen.PokemonRoot = s.FreeRoam.PokemonRoot;
        s.MapScreen.LoadMode = s.FreeRoam.LoadMode;
        s.MapScreen.FillTags = s.FreeRoam.FillTags;

        s.AnimeScreen.SharedAnimationFolders = s.FreeRoam.SharedAnimationFolders;
        s.AnimeScreen.TrainerParties = s.FreeRoam.TrainerParties;
        s.AnimeScreen.PokemonRoot = s.FreeRoam.PokemonRoot;
        s.AnimeScreen.LoadMode = s.FreeRoam.LoadMode;
        s.AnimeScreen.FillTags = s.FreeRoam.FillTags;

        s.AnimeWorldScreen.SharedAnimationFolders = s.FreeRoam.SharedAnimationFolders;
        s.AnimeWorldScreen.TrainerParties = s.FreeRoam.TrainerParties;
        s.AnimeWorldScreen.PokemonRoot = s.FreeRoam.PokemonRoot;
        s.AnimeWorldScreen.LoadMode = s.FreeRoam.LoadMode;
        s.AnimeWorldScreen.FillTags = s.FreeRoam.FillTags;
    }

    private static void ApplySavedAnimationSettings(GameRuntimeState s)
    {
        string? savedMode = s.Database.GetSetting("animation_mode");
        if (savedMode != null && Enum.TryParse<AnimationLoadMode>(savedMode, out var mode))
            s.FreeRoam.LoadMode = mode;

        string? savedTags = s.Database.GetSetting("fill_tags");
        if (savedTags != null)
            s.FreeRoam.FillTags = new HashSet<string>(savedTags.Split(',', StringSplitOptions.RemoveEmptyEntries));

        string? savedScreenMode = s.Database.GetSetting(LastModeSettingKey);
        s.ScreenMode = string.IsNullOrWhiteSpace(savedScreenMode) ? ScreenMode.FreeRoam : savedScreenMode.ToLowerInvariant() switch
        {
            "map" => ScreenMode.Map,
            "anime" => ScreenMode.AnimeModels,
            "animeworld" => ScreenMode.AnimeWorld,
            _ => ScreenMode.FreeRoam
        };
    }

    private static int ResolveInitialCharacterIndex(GameRuntimeState s)
    {
        string? lastCharId = s.Database.GetSetting("last_character_id");
        if (lastCharId == null || !int.TryParse(lastCharId, out int savedId))
            return 0;
        for (int i = 0; i < s.Characters.Count; i++)
            if (s.Characters[i].Id == savedId)
                return i;
        return 0;
    }
}
