#nullable enable
using System;
using System.Collections.Generic;

namespace Starfield2026.ModelLoader.Maps;

public static class MapCatalog
{
    private static readonly Dictionary<string, MapDefinition> _maps = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (_maps.ContainsKey(map.Id))
            throw new ArgumentException($"A map with ID '{map.Id}' is already registered.", nameof(map));
        _maps[map.Id] = map;
    }

    public static bool TryRegister(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return _maps.TryAdd(map.Id, map);
    }

    public static MapDefinition GetMap(string id)
    {
        if (_maps.TryGetValue(id, out var map))
            return map;
        throw new KeyNotFoundException($"No map found with ID '{id}'.");
    }

    public static IReadOnlyCollection<MapDefinition> GetAllMaps() => _maps.Values;

    public static bool TryGetMap(string id, out MapDefinition? map) =>
        _maps.TryGetValue(id, out map);

    public static void LoadAllMaps()
    {
        var assembly = typeof(MapCatalog).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsSubclassOf(typeof(MapDefinition)) && !type.IsAbstract)
            {
                var prop = type.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                prop?.GetValue(null);
            }
        }
    }

    public static void Clear() => _maps.Clear();
}
