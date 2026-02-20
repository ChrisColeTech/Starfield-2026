using System;
using System.Collections.Generic;

namespace Starfield2026.Core.Maps;

/// <summary>
/// Static registry for map definitions.
/// Provides centralized access to all registered maps in the game.
/// </summary>
public static class MapCatalog
{
    private static readonly Dictionary<string, MapDefinition> _maps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a map definition in the catalog.
    /// </summary>
    public static void Register(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (_maps.ContainsKey(map.Id))
            throw new ArgumentException($"A map with ID '{map.Id}' is already registered.", nameof(map));

        _maps[map.Id] = map;
    }

    /// <summary>
    /// Registers a map definition if not already present.
    /// </summary>
    public static bool TryRegister(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return _maps.TryAdd(map.Id, map);
    }

    /// <summary>
    /// Gets a map definition by its ID.
    /// </summary>
    public static MapDefinition GetMap(string id)
    {
        if (_maps.TryGetValue(id, out var map))
            return map;

        throw new KeyNotFoundException($"No map found with ID '{id}'.");
    }

    /// <summary>
    /// Gets all registered map definitions.
    /// </summary>
    public static IReadOnlyCollection<MapDefinition> GetAllMaps()
    {
        return _maps.Values;
    }

    /// <summary>
    /// Attempts to get a map definition by its ID.
    /// </summary>
    public static bool TryGetMap(string id, out MapDefinition? map)
    {
        return _maps.TryGetValue(id, out map);
    }

    /// <summary>
    /// Finds the neighboring map at the adjacent grid position for the given edge.
    /// Returns null if no map exists at that position.
    /// </summary>
    public static MapDefinition? GetNeighbor(MapDefinition from, MapEdge edge)
    {
        int nx = from.WorldX;
        int ny = from.WorldY;
        switch (edge)
        {
            case MapEdge.North: ny--; break;
            case MapEdge.South: ny++; break;
            case MapEdge.West: nx--; break;
            case MapEdge.East: nx++; break;
        }

        foreach (var map in _maps.Values)
        {
            if (map.WorldId == from.WorldId && map.WorldX == nx && map.WorldY == ny)
                return map;
        }
        return null;
    }

    /// <summary>
    /// Removes a map definition from the catalog by its ID.
    /// </summary>
    public static bool Unregister(string id)
    {
        return _maps.Remove(id);
    }

    /// <summary>
    /// Replaces an existing map definition or adds it if not present.
    /// </summary>
    public static void RegisterOrReplace(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _maps[map.Id] = map;
    }

    /// <summary>
    /// Clears all registered maps from the catalog.
    /// </summary>
    public static void Clear()
    {
        _maps.Clear();
    }
}
