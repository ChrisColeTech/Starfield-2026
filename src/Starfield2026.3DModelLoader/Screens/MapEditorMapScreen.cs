#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Maps;
using Starfield2026.ModelLoader.Runtime;

namespace Starfield2026.ModelLoader.Screens;

public class MapEditorMapScreen : IDisposable
{
    private readonly NavigationRuntime _nav = new();
    private readonly OverworldActorRuntime _actor = new();
    private readonly WorldRenderRuntime _render = new();
    private readonly MapWorldRuntime _map = new();
    private GraphicsDevice _device = null!;

    public Vector3 Position => _nav.Position;
    public float Yaw => _nav.Yaw;
    public MapDefinition? CurrentMap => _map.CurrentMap;
    public string StatusText { get; private set; } = "No map loaded";

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
        _map.Initialize(device);
        _nav.Initialize(new Vector3(10, 0f, 10), 100f);
        _nav.ConfigureTerrain(pos => _map.SampleHeight(pos) ?? 0f, (pos, radius) => _map.IsPassable(pos, radius));
    }

    public void SetTileCache(Rendering.TileModelCache cache) => _map.SetTileCache(cache);

    public void LoadFromAssets(string assetsRoot, string? preferredMapId = null)
    {
        StatusText = _map.Load(preferredMapId, "default", _ => true, "No map found",
            m => $"Map: {m.Name} ({m.Width}x{m.Height})");
    }

    public bool SwitchMap(int direction)
    {
        bool changed = _map.SwitchMap(direction, _ => true, out var status,
            m => $"Map: {m.Name} ({m.Width}x{m.Height})");
        if (changed) StatusText = status;
        return changed;
    }

    public void LoadCharacter(string folderPath)
    {
        bool ok = _actor.LoadCharacter(folderPath, LoadMode, FillTags, SharedAnimationFolders,
            PokemonRoot, TrainerParties, "MapScreen");
        string charName = System.IO.Path.GetFileName(folderPath);
        StatusText = ok ? $"Map: {_map.CurrentMap?.Name} | Char: {charName}" : $"Map: {_map.CurrentMap?.Name} | Failed: {charName}";
    }

    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _nav.UpdateMovement(dt, input);
        _actor.Update(dt, input, _nav.Player, _nav.SmoothedYaw);
        _nav.UpdateCamera(_device, dt, input, _actor.CameraTargetHeight(0f));
    }

    public void Draw(GraphicsDevice device)
    {
        _render.BeginFrame(device);
        _render.DrawWorld(device, _nav.View, _nav.Projection, _nav.CameraPosition);
        _map.Draw(device, _nav.View, _nav.Projection, _nav.CameraPosition);
        _render.DrawActorOrFallback(device, _nav.View, _nav.Projection, _nav.Position, _nav.Yaw, _actor, new Color(0, 220, 255));
    }

    public void Dispose() => _actor.Dispose();
}
