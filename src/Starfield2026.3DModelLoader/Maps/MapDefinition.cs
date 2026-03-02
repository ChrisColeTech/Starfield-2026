#nullable enable
using System;
using System.Collections.Generic;

namespace Starfield2026.ModelLoader.Maps;

/// <summary>
/// Abstract base class for generated map definitions.
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

    public string WorldId { get; }
    public string Id { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int TileSize { get; }
    public int WorldX { get; }
    public int WorldY { get; }

    public IReadOnlyList<WarpConnection> Warps => _warps;
    public IReadOnlyList<MapConnection> Connections => _connections;
    public IReadOnlyList<EncounterTable> EncounterGroups => _encounterGroups;
    public float ProgressMultiplier => _progressMultiplier;

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

    public int GetBaseTile(int x, int y) => _baseTileData[y * Width + x];
    public int? GetOverlayTile(int x, int y) => _overlayTileData[y * Width + x];
    public bool IsWalkableTile(int tileId) => _walkableTileIds.Contains(tileId);

    /// <summary>
    /// Returns true if the tile at (x, y) is walkable — both the base tile
    /// and the overlay tile (if present) must be walkable.
    /// Out-of-bounds positions are treated as non-walkable.
    /// </summary>
    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;

        int baseTileId = GetBaseTile(x, y);
        if (!IsWalkableTile(baseTileId)) return false;

        int? overlayId = GetOverlayTile(x, y);
        if (overlayId.HasValue && !IsWalkableTile(overlayId.Value)) return false;

        return true;
    }

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

    public float GetCameraCollisionHeight(int x, int y)
    {
        float height = GetTileHeight(x, y);
        int? overId = GetOverlayTile(x, y);
        if (overId.HasValue)
        {
            var overDef = TileRegistry.GetTile(overId.Value);
            if (overDef?.ModelId != null)
                height = Math.Max(height, overDef.BaselineSize * overDef.Scale);
        }
        return height;
    }

    public WarpConnection? GetWarp(int x, int y, WarpTrigger trigger)
    {
        foreach (var warp in _warps)
            if (warp.X == x && warp.Y == y && warp.Trigger == trigger)
                return warp;
        return null;
    }

    public MapConnection? GetConnection(MapEdge edge)
    {
        foreach (var conn in _connections)
            if (conn.Edge == edge)
                return conn;
        return null;
    }
}
