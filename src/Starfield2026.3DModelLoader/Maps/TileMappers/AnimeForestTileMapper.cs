#nullable enable
using System.Collections.Generic;

namespace Starfield2026.ModelLoader.Maps.TileMappers;

public sealed record AnimeForestTileAsset(string? TexturePath, string? ModelPath);

public static class AnimeForestTileMapper
{
    private static readonly IReadOnlyDictionary<int, AnimeForestTileAsset> _map =
        new Dictionary<int, AnimeForestTileAsset>
        {
            [1] = new("base_tiles/terrain/Grass.png", null),

            [45] = new(null, "anime_forest/models/AnimeTree_01.fbx"),
            [46] = new(null, "anime_forest/models/AnimeTree_02.fbx"),
            [47] = new(null, "anime_forest/models/AnimeTree_03.fbx"),
            [48] = new(null, "anime_forest/models/AnimeTree_04.fbx"),
            [49] = new(null, "anime_forest/models/AnimeTree_05.fbx"),
            [50] = new(null, "anime_forest/models/AnimeTree_06.fbx"),
            [51] = new(null, "anime_forest/models/AnimeTree_07.fbx"),
            [52] = new(null, "anime_forest/models/AnimeTree_08.fbx"),

            [53] = new(null, "anime_forest/models/AnimeBush_01.fbx"),
            [54] = new(null, "anime_forest/models/AnimeBush_02.fbx"),
            [55] = new(null, "anime_forest/models/AnimeBush_03.fbx"),
            [56] = new(null, "anime_forest/models/AnimeBush_04.fbx"),

            [57] = new("anime_forest/textures/Grass.png", "anime_forest/models/Grass.fbx"),
            [58] = new("anime_forest/textures/Grass.png", "anime_forest/models/GrassMesh.fbx"),
        };

    public static bool TryGetAsset(int tileId, out AnimeForestTileAsset asset) =>
        _map.TryGetValue(tileId, out asset!);

    public static string? ResolveTexturePath(int tileId) =>
        _map.TryGetValue(tileId, out var asset) ? asset.TexturePath : null;

    public static string? ResolveModelPath(int tileId) =>
        _map.TryGetValue(tileId, out var asset) ? asset.ModelPath : null;
}
