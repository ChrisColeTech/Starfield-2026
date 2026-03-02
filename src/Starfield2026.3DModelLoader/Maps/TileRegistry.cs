#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Starfield2026.ModelLoader.Maps;

/// <summary>
/// Default tile registry for the 3D map editor pipeline.
/// Tile IDs match the editor's default.json registry.
/// </summary>
public static class TileRegistry
{
    private static readonly Dictionary<int, TileDefinition> _tiles = new()
    {
        // Terrain (0-1)
        [0] = new TileDefinition(0, "Empty", true, "#1a1a2e", TileCategory.Terrain),
        [1] = new TileDefinition(1, "Grass", true, "#4a7c3f", TileCategory.Terrain, TexturePath: "base_tiles/terrain/Grass.png"),

        // Rocks (2-5)
        [2] = new TileDefinition(2, "Mountain01", false, "#6b5b4f", TileCategory.Structure, Height: 2f, ModelId: "Mountain01", BaselineSize: 8f),
        [3] = new TileDefinition(3, "Rock01", false, "#7a7a7a", TileCategory.Decoration, Height: 1f, ModelId: "Rock01"),
        [4] = new TileDefinition(4, "Rock02", false, "#8a8a7a", TileCategory.Decoration, Height: 1f, ModelId: "Rock02"),
        [5] = new TileDefinition(5, "Pebbles01", true, "#9a9080", TileCategory.Decoration, ModelId: "Pebbles01"),

        // Vegetation (6-9)
        [6] = new TileDefinition(6, "Tree01", false, "#2d6b30", TileCategory.Decoration, Height: 2f, ModelId: "Tree01", BaselineSize: 6.0f),
        [7] = new TileDefinition(7, "Bush01", false, "#3d8b40", TileCategory.Decoration, Height: 0.8f, ModelId: "Bush01"),
        [8] = new TileDefinition(8, "Flower01", true, "#e074a8", TileCategory.Decoration, ModelId: "Flower01"),
        [9] = new TileDefinition(9, "Flowers01", true, "#d4689c", TileCategory.Decoration, ModelId: "Flowers01"),

        // Structures (10)
        [10] = new TileDefinition(10, "Bridge01", true, "#8b6b3d", TileCategory.Structure, ModelId: "Bridge01"),

        // Paths (12-13)
        [12] = new TileDefinition(12, "Path01", true, "#d4a574", TileCategory.Terrain, TexturePath: "base_tiles/terrain/Path.png"),
        [13] = new TileDefinition(13, "Path02", true, "#c89b6a", TileCategory.Terrain, TexturePath: "base_tiles/terrain/Path.png"),

        // Indoor (14)
        [14] = new TileDefinition(14, "IndoorFloor", true, "#8b7d6b", TileCategory.Terrain, TexturePath: "base_tiles/terrain/Indoor_Floor.png"),

        // Stylized Nature pack (19-20, 30-38)
        [19] = new TileDefinition(19, "StylizedTree01", false, "#2d8b2d", TileCategory.Decoration, Height: 2f, ModelId: "tree_a", BaselineSize: 2.5f),
        [20] = new TileDefinition(20, "StylizedTree02", false, "#258025", TileCategory.Decoration, Height: 2f, ModelId: "tree_b", BaselineSize: 2.5f),
        [30] = new TileDefinition(30, "StylizedTree03", false, "#2d7a2d", TileCategory.Decoration, Height: 2f, ModelId: "tree_c", BaselineSize: 2.5f),
        [31] = new TileDefinition(31, "StylizedTree04", false, "#257025", TileCategory.Decoration, Height: 2f, ModelId: "tree_d", BaselineSize: 2.5f),
        [32] = new TileDefinition(32, "StylizedTree05", false, "#2d6a2d", TileCategory.Decoration, Height: 2f, ModelId: "tree_e", BaselineSize: 2.5f),
        [33] = new TileDefinition(33, "StylizedTree06", false, "#256025", TileCategory.Decoration, Height: 2f, ModelId: "tree_f", BaselineSize: 2.5f),
        [34] = new TileDefinition(34, "StylizedTree07", false, "#2d5a2d", TileCategory.Decoration, Height: 2f, ModelId: "tree_g", BaselineSize: 2.5f),
        [35] = new TileDefinition(35, "StylizedTree08", false, "#255025", TileCategory.Decoration, Height: 2f, ModelId: "tree_h", BaselineSize: 2.5f),
        [36] = new TileDefinition(36, "StylizedTree09", false, "#2d4a2d", TileCategory.Decoration, Height: 2f, ModelId: "tree_i", BaselineSize: 2.5f),
        [37] = new TileDefinition(37, "StylizedTree10", false, "#254025", TileCategory.Decoration, Height: 2f, ModelId: "tree_j", BaselineSize: 2.5f),
        [38] = new TileDefinition(38, "StylizedTree11", false, "#2d3a2d", TileCategory.Decoration, Height: 2f, ModelId: "tree_k", BaselineSize: 2.5f),

        // Rocks/Boulders pack (39-44)
        [39] = new TileDefinition(39, "RockFree01", false, "#7a6b5f", TileCategory.Decoration, Height: 1f, ModelId: "rock1_LOD0", BaselineSize: 1.5f),
        [40] = new TileDefinition(40, "RockFree02", false, "#6e6055", TileCategory.Decoration, Height: 1f, ModelId: "rock2_LOD0", BaselineSize: 1.5f),
        [41] = new TileDefinition(41, "RockFree03", false, "#62554b", TileCategory.Decoration, Height: 1f, ModelId: "rock3_LOD0", BaselineSize: 1.5f),
        [42] = new TileDefinition(42, "RockFree04", false, "#564a41", TileCategory.Decoration, Height: 1f, ModelId: "rock4_LOD0", BaselineSize: 1.5f),
        [43] = new TileDefinition(43, "RockFree05", false, "#4a3f37", TileCategory.Decoration, Height: 1f, ModelId: "rock5_LOD0", BaselineSize: 1.5f),
        [44] = new TileDefinition(44, "RockFree06", false, "#3e342d", TileCategory.Decoration, Height: 1f, ModelId: "rock6_LOD0", BaselineSize: 1.5f),

        // Anime Trees pack — Trees (45-52)
        [45] = new TileDefinition(45, "AnimeTree01", false, "#2d8a30", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_01", BaselineSize: 5.0f),
        [46] = new TileDefinition(46, "AnimeTree02", false, "#258025", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_02", BaselineSize: 5.0f),
        [47] = new TileDefinition(47, "AnimeTree03", false, "#1d7520", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_03", BaselineSize: 5.0f),
        [48] = new TileDefinition(48, "AnimeTree04", false, "#2d6a2d", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_04", BaselineSize: 5.0f),
        [49] = new TileDefinition(49, "AnimeTree05", false, "#1a5c2a", TileCategory.Decoration, Height: 2.5f, ModelId: "AnimeTree_05", BaselineSize: 3f),
        [50] = new TileDefinition(50, "AnimeTree06", false, "#166024", TileCategory.Decoration, Height: 2.5f, ModelId: "AnimeTree_06", BaselineSize: 3f),
        [51] = new TileDefinition(51, "AnimeTree07", false, "#207030", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_07", BaselineSize: 2.5f),
        [52] = new TileDefinition(52, "AnimeTree08", false, "#309035", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_08", BaselineSize: 2.5f),

        // Anime Trees pack — Bushes (53-56)
        [53] = new TileDefinition(53, "AnimeBush01", false, "#3d9040", TileCategory.Decoration, Height: 0.5f, ModelId: "AnimeBush_01", BaselineSize: 1f),
        [54] = new TileDefinition(54, "AnimeBush02", false, "#358538", TileCategory.Decoration, Height: 0.5f, ModelId: "AnimeBush_02", BaselineSize: 1f),
        [55] = new TileDefinition(55, "AnimeBush03", false, "#2d7a30", TileCategory.Decoration, Height: 0.5f, ModelId: "AnimeBush_03", BaselineSize: 1f),
        [56] = new TileDefinition(56, "AnimeBush04", false, "#257025", TileCategory.Decoration, Height: 0.5f, ModelId: "AnimeBush_04", BaselineSize: 1f),

        // Anime Trees pack — Grass (57-58)
        [57] = new TileDefinition(57, "AnimeGrass", true, "#5ca050", TileCategory.Encounter, ModelId: "Grass", BaselineSize: 0.5f),
        [58] = new TileDefinition(58, "AnimeGrassMesh", true, "#54964a", TileCategory.Encounter, ModelId: "GrassMesh", BaselineSize: 0.8f),
    };

    public static TileDefinition? GetTile(int id) =>
        _tiles.TryGetValue(id, out var tile) ? tile : null;

    public static IEnumerable<TileDefinition> GetTilesByCategory(TileCategory category) =>
        _tiles.Values.Where(t => t.Category == category);

    public static IEnumerable<TileDefinition> AllTiles => _tiles.Values;

    public static int Count => _tiles.Count;
}
