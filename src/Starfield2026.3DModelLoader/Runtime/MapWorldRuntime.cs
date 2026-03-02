#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Maps;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Runtime;

public sealed class MapWorldRuntime
{
    private readonly MapRenderer _renderer = new();
    private readonly List<MapDefinition> _cycle = new();
    private TileModelCache? _cache;
    private MapDefinition? _map;

    public MapDefinition? CurrentMap => _map;

    public void Initialize(GraphicsDevice device) => _renderer.Initialize(device);
    public void SetTileCache(TileModelCache cache) => _cache = cache;

    public string Load(
        string? preferredMapId,
        string? defaultMapId,
        Func<MapDefinition, bool> include,
        string noMapStatus,
        Func<MapDefinition, string> status)
    {
        _map = ResolveInitialMap(preferredMapId, defaultMapId, include);
        if (_map == null)
            return noMapStatus;

        BuildCycle(include);
        return status(_map);
    }

    public bool SwitchMap(int direction, Func<MapDefinition, bool> include, out string status, Func<MapDefinition, string> statusFactory)
    {
        BuildCycle(include);
        status = _map == null ? string.Empty : statusFactory(_map);
        if (_cycle.Count == 0 || _map == null)
            return false;

        int idx = _cycle.FindIndex(m => string.Equals(m.Id, _map.Id, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            idx = 0;

        int next = idx + Math.Sign(direction);
        if (next < 0) next = _cycle.Count - 1;
        if (next >= _cycle.Count) next = 0;
        if (next == idx) return false;

        _map = _cycle[next];
        status = statusFactory(_map);
        return true;
    }

    public float? SampleHeight(Vector3 worldPos)
    {
        if (_map == null || _cache == null) return null;
        return MapCollision.SampleHeight(worldPos, _map, _cache);
    }

    public bool IsPassable(Vector3 worldPos, float radius)
    {
        if (_map == null || _cache == null) return true;
        return MapCollision.IsPassable(worldPos, radius, _map);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        if (_map != null && _cache != null && _cache.ModelCount > 0)
            _renderer.Draw(device, view, projection, _map, _cache, cameraPosition);
    }

    private static MapDefinition? ResolveInitialMap(string? preferred, string? fallback, Func<MapDefinition, bool> include)
    {
        if (!string.IsNullOrWhiteSpace(preferred) && MapCatalog.TryGetMap(preferred, out var pref) && pref != null && include(pref)) return pref;
        if (!string.IsNullOrWhiteSpace(fallback) && MapCatalog.TryGetMap(fallback, out var def) && def != null && include(def)) return def;
        foreach (var map in MapCatalog.GetAllMaps()) if (include(map)) return map;
        return null;
    }

    private void BuildCycle(Func<MapDefinition, bool> include)
    {
        _cycle.Clear();
        foreach (var map in MapCatalog.GetAllMaps())
            if (include(map)) _cycle.Add(map);
        _cycle.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
    }
}
