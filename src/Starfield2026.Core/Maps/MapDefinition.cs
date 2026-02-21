#nullable enable

using System;
using System.Collections.Generic;
using Starfield2026.Core.Encounters;

namespace Starfield2026.Core.Maps;

/// <summary>
/// Abstract base class for generated map definitions.
/// Generated subclasses store tile data as flat row-major arrays
/// and pass them to the protected constructor.
/// </summary>
public abstract class MapDefinition
{
    private readonly int[] _baseTileData;
    private readonly int?[] _overlayTileData;
    private readonly HashSet<int> _walkableTileIds;
    private readonly WarpConnection[] _warps;
    private readonly MapConnection[] _connections;
    private readonly EncounterTable[] _encounterGroups;
    private readonly float _progressMultiplier;

    /// <summary>Identifier of the world this map belongs to.</summary>
    public string WorldId { get; }

    /// <summary>Unique identifier for this map.</summary>
    public string Id { get; }

    /// <summary>Display name for this map.</summary>
    public string Name { get; }

    /// <summary>Width of the map in tiles.</summary>
    public int Width { get; }

    /// <summary>Height of the map in tiles.</summary>
    public int Height { get; }

    /// <summary>Tile size in world units used by the runtime renderer.</summary>
    public int TileSize { get; }

    /// <summary>World grid X position. Maps at adjacent grid positions auto-connect.</summary>
    public int WorldX { get; }

    /// <summary>World grid Y position. Maps at adjacent grid positions auto-connect.</summary>
    public int WorldY { get; }

    /// <summary>Warp connections (doors, teleporters) defined on this map.</summary>
    public IReadOnlyList<WarpConnection> Warps => _warps;

    /// <summary>Edge connections to adjacent maps.</summary>
    public IReadOnlyList<MapConnection> Connections => _connections;

    /// <summary>Encounter tables defined for this map.</summary>
    public IReadOnlyList<EncounterTable> EncounterGroups => _encounterGroups;

    /// <summary>Progress-based level scaling multiplier for encounters on this map.</summary>
    public float ProgressMultiplier => _progressMultiplier;

    /// <summary>Whether this map should use the wireframe GridRenderer instead of MapRenderer3D.</summary>
    public virtual bool UseWireframeGrid => false;

    /// <summary>
    /// Creates a MapDefinition from flat row-major tile arrays.
    /// Automatically registers this map in MapCatalog.
    /// </summary>
    protected MapDefinition(
        string worldId, string id, string name,
        int width, int height, int tileSize,
        int[] baseTileData, int?[] overlayTileData, int[] walkableTileIds,
        WarpConnection[]? warps = null,
        MapConnection[]? connections = null,
        int worldX = 0, int worldY = 0,
        EncounterTable[]? encounterGroups = null,
        float progressMultiplier = 0f)
    {
        WorldId = worldId;
        Id = id;
        Name = name;
        Width = width;
        Height = height;
        TileSize = tileSize;
        WorldX = worldX;
        WorldY = worldY;
        _baseTileData = baseTileData;
        _overlayTileData = overlayTileData;
        _walkableTileIds = new HashSet<int>(walkableTileIds);
        _warps = warps ?? [];
        _connections = connections ?? [];
        _encounterGroups = encounterGroups ?? [];
        _progressMultiplier = progressMultiplier;

        MapCatalog.TryRegister(this);
    }

    /// <summary>
    /// Gets the base tile ID at position (x, y).
    /// </summary>
    public int GetBaseTile(int x, int y) => _baseTileData[y * Width + x];

    /// <summary>
    /// Gets the overlay tile ID at position (x, y), or null if no overlay.
    /// </summary>
    public int? GetOverlayTile(int x, int y) => _overlayTileData[y * Width + x];

    /// <summary>
    /// Checks if a tile ID is walkable according to this map's walkable set.
    /// </summary>
    public bool IsWalkableTile(int tileId) => _walkableTileIds.Contains(tileId);

    /// <summary>
    /// Gets the 3D height for a tile at position (x, y) using the TileRegistry.
    /// </summary>
    public float GetTileHeight(int x, int y)
    {
        float height = 0f;
        int tileId = GetBaseTile(x, y);
        var tileDef = TileRegistry.GetTile(tileId);
        if (tileDef != null) height = Math.Max(height, tileDef.Height);
        
        int? overId = GetOverlayTile(x, y);
        if (overId.HasValue)
        {
            var overDef = TileRegistry.GetTile(overId.Value);
            if (overDef != null) height = Math.Max(height, overDef.Height);
        }
        return height;
    }

    /// <summary>
    /// Gets the warp connection at the specified position with the given trigger, or null if none.
    /// </summary>
    public WarpConnection? GetWarp(int x, int y, WarpTrigger trigger)
    {
        foreach (var warp in _warps)
        {
            if (warp.X == x && warp.Y == y && warp.Trigger == trigger)
                return warp;
        }
        return null;
    }

    /// <summary>
    /// Gets the edge connection for the given direction, or null if none.
    /// </summary>
    public MapConnection? GetConnection(MapEdge edge)
    {
        foreach (var conn in _connections)
        {
            if (conn.Edge == edge)
                return conn;
        }
        return null;
    }
}
