#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Screens;

namespace Starfield2026.ModelLoader.Runtime;

public sealed class GameUpdateModule
{
    public void Update(GameRuntimeState s, GameRuntimeCoordinator coordinator, GraphicsDevice device, GameTime gameTime)
    {
        s.Input.Update();
        var snap = s.Input.Current;
        s.SharedTileCache?.PumpQueuedLoads(device, maxModelsPerUpdate: 1, maxMilliseconds: 4.0);

        if (HandleCharacterSelect(s, coordinator, snap, gameTime))
            return;

        if (snap.PausePressed)
        {
            s.CharSelect = new CharacterSelectOverlay(s.Characters, s.FreeRoam.LoadMode, s.FreeRoam.FillTags);
            return;
        }

        if (snap.SwitchModePressed)
        {
            s.ScreenMode = s.ScreenMode switch
            {
                ScreenMode.FreeRoam => ScreenMode.Map,
                ScreenMode.Map => ScreenMode.AnimeModels,
                ScreenMode.AnimeModels => ScreenMode.AnimeWorld,
                _ => ScreenMode.FreeRoam,
            };
            coordinator.RebuildTileCacheForScreen(s, s.ScreenMode, device, preloadBlocking: true);
            if (s.ScreenMode == ScreenMode.Map) LoadCurrentCharacterToMap(s);
            if (s.ScreenMode == ScreenMode.AnimeModels) LoadCurrentCharacterToAnime(s);
            if (s.ScreenMode == ScreenMode.AnimeWorld) LoadCurrentCharacterToAnimeWorld(s);
            coordinator.PersistCurrentMode(s);
            return;
        }

        if (snap.PageLeft || snap.PageRight)
        {
            int dir = snap.PageRight ? 1 : -1;
            bool changed = s.ScreenMode == ScreenMode.Map ? s.MapScreen.SwitchMap(dir)
                : s.ScreenMode == ScreenMode.AnimeModels && s.AnimeScreen.SwitchMap(dir);
            if (changed)
            {
                coordinator.RebuildTileCacheForScreen(s, s.ScreenMode, device, preloadBlocking: true);
                string? mapId = s.ScreenMode == ScreenMode.Map
                    ? s.MapScreen.CurrentMap?.Id
                    : s.AnimeScreen.CurrentMap?.Id;
                if (mapId != null)
                    s.Database.SetSetting(s.ScreenMode == ScreenMode.Map ? "last_map_id" : "last_anime_map_id", mapId);
            }
        }

        if (s.ScreenMode == ScreenMode.Map) s.MapScreen.Update(gameTime, snap);
        else if (s.ScreenMode == ScreenMode.AnimeModels) s.AnimeScreen.Update(gameTime, snap);
        else if (s.ScreenMode == ScreenMode.AnimeWorld) s.AnimeWorldScreen.Update(gameTime, snap);
        else s.FreeRoam.Update(gameTime, snap);
    }

    private static bool HandleCharacterSelect(GameRuntimeState s, GameRuntimeCoordinator coordinator, InputSnapshot snap, GameTime gameTime)
    {
        if (s.CharSelect == null)
            return false;

        s.CharSelect.Update(snap, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (!s.CharSelect.IsFinished)
            return true;

        bool settingsChanged = s.CharSelect.AnimationSettingsChanged;
        if (settingsChanged)
        {
            s.FreeRoam.LoadMode = s.CharSelect.LoadMode;
            s.FreeRoam.FillTags = s.CharSelect.FillTags;
            s.MapScreen.LoadMode = s.CharSelect.LoadMode;
            s.MapScreen.FillTags = s.CharSelect.FillTags;
            s.AnimeScreen.LoadMode = s.CharSelect.LoadMode;
            s.AnimeScreen.FillTags = s.CharSelect.FillTags;
            s.AnimeWorldScreen.LoadMode = s.CharSelect.LoadMode;
            s.AnimeWorldScreen.FillTags = s.CharSelect.FillTags;
        }

        if (s.CharSelect.SelectedFolder != null)
            LoadSelectedCharacter(s, s.CharSelect.SelectedFolder);
        else if (settingsChanged)
            coordinator.LoadCharacterForCurrentMode(s);

        s.Database.SetSetting("animation_mode", s.FreeRoam.LoadMode.ToString());
        s.Database.SetSetting("fill_tags", string.Join(",", s.FreeRoam.FillTags));
        s.CharSelect = null;
        return true;
    }

    private static void LoadSelectedCharacter(GameRuntimeState s, string folder)
    {
        if (s.ScreenMode == ScreenMode.Map) s.MapScreen.LoadCharacter(folder);
        else if (s.ScreenMode == ScreenMode.AnimeModels) s.AnimeScreen.LoadCharacter(folder);
        else if (s.ScreenMode == ScreenMode.AnimeWorld) s.AnimeWorldScreen.LoadCharacter(folder);
        else s.FreeRoam.LoadCharacter(folder);

        for (int i = 0; i < s.Characters.Count; i++)
        {
            string candidate = Path.GetDirectoryName(s.Characters[i].ManifestPath) ?? "";
            if (!string.Equals(candidate, folder, StringComparison.OrdinalIgnoreCase))
                continue;
            s.CharacterIndex = i;
            s.Database.SetSetting("last_character_id", s.Characters[i].Id.ToString());
            break;
        }
    }

    private static void LoadCurrentCharacterToMap(GameRuntimeState s)
    {
        if (s.CharacterIndex < 0 || s.CharacterIndex >= s.Characters.Count) return;
        s.MapScreen.LoadCharacter(Path.GetDirectoryName(s.Characters[s.CharacterIndex].ManifestPath) ?? "");
    }

    private static void LoadCurrentCharacterToAnime(GameRuntimeState s)
    {
        if (s.CharacterIndex < 0 || s.CharacterIndex >= s.Characters.Count) return;
        s.AnimeScreen.LoadCharacter(Path.GetDirectoryName(s.Characters[s.CharacterIndex].ManifestPath) ?? "");
    }

    private static void LoadCurrentCharacterToAnimeWorld(GameRuntimeState s)
    {
        if (s.CharacterIndex < 0 || s.CharacterIndex >= s.Characters.Count) return;
        s.AnimeWorldScreen.LoadCharacter(Path.GetDirectoryName(s.Characters[s.CharacterIndex].ManifestPath) ?? "");
    }
}
