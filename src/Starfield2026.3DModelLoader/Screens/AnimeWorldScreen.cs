#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Maps;
using Starfield2026.ModelLoader.Rendering;
using Starfield2026.ModelLoader.Runtime;

namespace Starfield2026.ModelLoader.Screens;

public class AnimeWorldScreen : IDisposable
{
    private const string WorldId = "anime_world";

    private readonly NavigationRuntime _nav = new();
    private readonly OverworldActorRuntime _actor = new();
    private readonly WorldRenderRuntime _render = new();
    private readonly MapRenderer _renderer = new();
    private readonly List<MapDefinition> _maps = new();
    private TileModelCache? _cache;
    private GraphicsDevice _device = null!;

    public Vector3 Position => _nav.Position;
    public float Yaw => _nav.Yaw;
    public IReadOnlyList<MapDefinition> Maps => _maps;
    public string StatusText { get; private set; } = "No anime world loaded";

    public Dictionary<string, string> SharedAnimationFolders { get; set; } = new();
    public Dictionary<string, string?[]> TrainerParties { get; set; } = new();
    public string PokemonRoot { get; set; } = "";
    public AnimationLoadMode LoadMode { get; set; } = AnimationLoadMode.FillMissing;
    public HashSet<string> FillTags { get; set; } = new() { "Jump", "Land" };

    public void Initialize(GraphicsDevice device)
    {
        _device = device;
        _render.Initialize(device);
        _actor.Initialize(device);
        _renderer.Initialize(device);
        _nav.Initialize(new Vector3(10, 0f, 10), 60f);
        _nav.ConfigureTerrain(SampleHeight, IsPassable, SampleCameraHeight);
    }

    public void SetTileCache(TileModelCache cache) => _cache = cache;

    public void LoadWorld()
    {
        _maps.Clear();
        foreach (var map in MapCatalog.GetAllMaps())
        {
            if (string.Equals(map.WorldId, WorldId, StringComparison.OrdinalIgnoreCase))
                _maps.Add(map);
        }

        if (_maps.Count > 0)
            StatusText = $"Anime World ({_maps.Count} maps)";
        else
            StatusText = "No anime world maps found";
    }

    public void LoadCharacter(string folderPath)
    {
        bool ok = _actor.LoadCharacter(folderPath, LoadMode, FillTags, SharedAnimationFolders,
            PokemonRoot, TrainerParties, "AnimeWorldScreen");
        string charName = System.IO.Path.GetFileName(folderPath);
        StatusText = ok ? $"Anime World ({_maps.Count} maps) | {charName}" : $"Anime World | Failed: {charName}";
    }

    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _nav.UpdateMovement(dt, input);
        _actor.Update(dt, input, _nav.Player, _nav.SmoothedYaw);
        _nav.UpdateCamera(_device, dt, input, _actor.CameraTargetHeight(1f));
    }

    public void Draw(GraphicsDevice device)
    {
        if (_cache == null || _maps.Count == 0) return;

        _render.BeginFrame(device);
        _render.DrawWorld(device, _nav.View, _nav.Projection, _nav.CameraPosition);

        device.RasterizerState = RenderStates.CullNone;

        // Opaque pass for all maps
        device.BlendState = BlendState.Opaque;
        foreach (var map in _maps)
        {
            int ox = map.WorldX * map.Width;
            int oz = map.WorldY * map.Height;
            _renderer.DrawWithOffset(device, _nav.View, _nav.Projection, map, _cache, ox, oz, false, _nav.CameraPosition);
        }

        // Alpha pass for all maps
        device.BlendState = BlendState.NonPremultiplied;
        foreach (var map in _maps)
        {
            int ox = map.WorldX * map.Width;
            int oz = map.WorldY * map.Height;
            _renderer.DrawWithOffset(device, _nav.View, _nav.Projection, map, _cache, ox, oz, true, _nav.CameraPosition);
        }

        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;

        _render.DrawActorOrFallback(device, _nav.View, _nav.Projection, _nav.Position, _nav.Yaw, _actor, new Color(100, 220, 100));
    }

    private float SampleHeight(Vector3 worldPos)
    {
        var map = FindMapAt(worldPos);
        if (map == null) return 0f;

        int localX = (int)MathF.Floor(worldPos.X + 0.5f) - map.WorldX * map.Width;
        int localZ = (int)MathF.Floor(worldPos.Z + 0.5f) - map.WorldY * map.Height;
        localX = Math.Clamp(localX, 0, map.Width - 1);
        localZ = Math.Clamp(localZ, 0, map.Height - 1);

        return map.GetTileHeight(localX, localZ);
    }

    private float SampleCameraHeight(Vector3 worldPos)
    {
        var map = FindMapAt(worldPos);
        if (map == null) return 0f;

        int localX = (int)MathF.Floor(worldPos.X + 0.5f) - map.WorldX * map.Width;
        int localZ = (int)MathF.Floor(worldPos.Z + 0.5f) - map.WorldY * map.Height;
        localX = Math.Clamp(localX, 0, map.Width - 1);
        localZ = Math.Clamp(localZ, 0, map.Height - 1);

        return map.GetCameraCollisionHeight(localX, localZ);
    }

    private bool IsPassable(Vector3 worldPos, float radius)
    {
        var map = FindMapAt(worldPos);
        if (map == null) return false;

        int localX = (int)MathF.Floor(worldPos.X + 0.5f) - map.WorldX * map.Width;
        int localZ = (int)MathF.Floor(worldPos.Z + 0.5f) - map.WorldY * map.Height;

        if (localX < 0 || localX >= map.Width || localZ < 0 || localZ >= map.Height)
            return false;

        return map.IsWalkable(localX, localZ);
    }

    private MapDefinition? FindMapAt(Vector3 worldPos)
    {
        foreach (var map in _maps)
        {
            int ox = map.WorldX * map.Width;
            int oz = map.WorldY * map.Height;
            float lx = worldPos.X - ox;
            float lz = worldPos.Z - oz;

            if (lx >= -0.5f && lx < map.Width - 0.5f && lz >= -0.5f && lz < map.Height - 0.5f)
                return map;
        }
        return null;
    }

    public void Dispose() => _actor.Dispose();
}
