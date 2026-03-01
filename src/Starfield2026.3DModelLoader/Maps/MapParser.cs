#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Starfield2026.ModelLoader.Maps;

/// <summary>
/// Lightweight tile definition for the 3D scene — parsed from TileRegistry.cs.
/// </summary>
public record Tile3D(int Id, string Name, bool Walkable, string? ModelId, float Height);

/// <summary>
/// Lightweight map grid — parsed from a generated .g.cs MapDefinition file.
/// No dependency on Core.
/// </summary>
public class MapData3D
{
    public string MapId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public int TileSize { get; init; } = 1;
    public int[] BaseTiles { get; init; } = Array.Empty<int>();
    public int?[] OverlayTiles { get; init; } = Array.Empty<int?>();

    public int GetBaseTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return 0;
        int idx = y * Width + x;
        return idx >= 0 && idx < BaseTiles.Length ? BaseTiles[idx] : 0;
    }

    public int? GetOverlayTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return null;
        int idx = y * Width + x;
        return idx >= 0 && idx < OverlayTiles.Length ? OverlayTiles[idx] : null;
    }
}

/// <summary>
/// Parses TileRegistry.cs and .g.cs map files into lightweight 3D-ready types.
/// </summary>
public static class MapParser
{
    /// <summary>
    /// Parse a TileRegistry.cs file and extract tile definitions (including ModelId).
    /// </summary>
    public static Dictionary<int, Tile3D> ParseRegistry(string csSource)
    {
        var tiles = new Dictionary<int, Tile3D>();

        // Match: [id] = new TileDefinition(id, "Name", walkable, "color", TileCategory.Cat, ...)
        var tilePattern = new Regex(
            @"\[(\d+)\]\s*=\s*new\s+TileDefinition\(\s*(\d+)\s*,\s*""([^""]+)""\s*,\s*(true|false)\s*,\s*""[^""]+""[^)]*\)",
            RegexOptions.Compiled);

        var modelPattern = new Regex(@"ModelId:\s*""([^""]+)""", RegexOptions.Compiled);
        var heightPattern = new Regex(@"Height:\s*([\d.]+)f?", RegexOptions.Compiled);

        foreach (Match m in tilePattern.Matches(csSource))
        {
            int id = int.Parse(m.Groups[2].Value);
            string name = m.Groups[3].Value;
            bool walkable = m.Groups[4].Value == "true";

            string fullMatch = m.Value;
            string? modelId = null;
            float height = 0f;

            var modelMatch = modelPattern.Match(fullMatch);
            if (modelMatch.Success)
                modelId = modelMatch.Groups[1].Value;

            var heightMatch = heightPattern.Match(fullMatch);
            if (heightMatch.Success)
                height = float.Parse(heightMatch.Groups[1].Value);

            tiles[id] = new Tile3D(id, name, walkable, modelId, height);
        }

        return tiles;
    }

    /// <summary>
    /// Parse a generated .g.cs MapDefinition file into a MapData3D.
    /// </summary>
    public static MapData3D ParseMap(string csSource)
    {
        // Parse constructor: base("worldId", "map_id", "Display Name", width, height, tileSize, ...)
        var ctorPattern = new Regex(
            @"base\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)");
        var ctorMatch = ctorPattern.Match(csSource);

        string mapId, displayName;
        int width, height, tileSize;

        if (ctorMatch.Success)
        {
            mapId = ctorMatch.Groups[2].Value;
            displayName = ctorMatch.Groups[3].Value;
            width = int.Parse(ctorMatch.Groups[4].Value);
            height = int.Parse(ctorMatch.Groups[5].Value);
            tileSize = int.Parse(ctorMatch.Groups[6].Value);
        }
        else
        {
            // Legacy: base("map_id", "Display Name", width, height, tileSize, ...)
            var legacy = new Regex(@"base\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)");
            var legacyMatch = legacy.Match(csSource);
            if (!legacyMatch.Success)
                throw new InvalidOperationException("Could not parse MapDefinition constructor");

            mapId = legacyMatch.Groups[1].Value;
            displayName = legacyMatch.Groups[2].Value;
            width = int.Parse(legacyMatch.Groups[3].Value);
            height = int.Parse(legacyMatch.Groups[4].Value);
            tileSize = int.Parse(legacyMatch.Groups[5].Value);
        }

        // Parse BaseTileData = [ ... ];
        var basePattern = new Regex(@"BaseTileData\s*=\s*\[([\s\S]*?)\];");
        var baseMatch = basePattern.Match(csSource);
        int[] baseTiles = Array.Empty<int>();
        if (baseMatch.Success)
        {
            var numbers = Regex.Matches(baseMatch.Groups[1].Value, @"-?\d+");
            baseTiles = new int[numbers.Count];
            for (int i = 0; i < numbers.Count; i++)
                baseTiles[i] = int.Parse(numbers[i].Value);
        }

        // Parse OverlayTileData = [ ... ];
        var overlayPattern = new Regex(@"OverlayTileData\s*=\s*\[([\s\S]*?)\];");
        var overlayMatch = overlayPattern.Match(csSource);
        int?[] overlayTiles = new int?[width * height];
        if (overlayMatch.Success)
        {
            var parts = overlayMatch.Groups[1].Value.Split(',');
            for (int i = 0; i < Math.Min(parts.Length, overlayTiles.Length); i++)
            {
                var val = parts[i].Trim();
                overlayTiles[i] = val == "null" || string.IsNullOrWhiteSpace(val) ? null : int.Parse(val);
            }
        }

        return new MapData3D
        {
            MapId = mapId,
            DisplayName = displayName,
            Width = width,
            Height = height,
            TileSize = tileSize,
            BaseTiles = baseTiles,
            OverlayTiles = overlayTiles,
        };
    }

    /// <summary>
    /// Load registry from a .cs file on disk.
    /// </summary>
    public static Dictionary<int, Tile3D> LoadRegistryFile(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<int, Tile3D>();
        return ParseRegistry(File.ReadAllText(path));
    }

    /// <summary>
    /// Load map from a .g.cs file on disk.
    /// </summary>
    public static MapData3D LoadMapFile(string path)
    {
        return ParseMap(File.ReadAllText(path));
    }
}
