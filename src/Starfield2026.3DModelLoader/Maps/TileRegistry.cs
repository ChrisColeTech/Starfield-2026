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
        // ── Terrain (0-1) ──
        [0] = new TileDefinition(0, "Empty", true, "#1a1a2e", TileCategory.Terrain),
        [1] = new TileDefinition(1, "Grass", true, "#4a7c3f", TileCategory.Terrain, TexturePath: "base_tiles/terrain/Grass.png"),

        // ── Anime Forest — Rocks (2-5) ──
        [2] = new TileDefinition(2, "Mountain01", false, "#6b5b4f", TileCategory.Structure, Height: 2f, ModelId: "Mountain01", BaselineSize: 8f),
        [3] = new TileDefinition(3, "Rock01", false, "#7a7a7a", TileCategory.Decoration, Height: 1f, ModelId: "Rock01"),
        [4] = new TileDefinition(4, "Rock02", false, "#8a8a7a", TileCategory.Decoration, Height: 1f, ModelId: "Rock02"),
        [5] = new TileDefinition(5, "Pebbles01", true, "#9a9080", TileCategory.Decoration, ModelId: "Pebbles01"),

        // ── Anime Forest — Vegetation (6-9) ──
        [6] = new TileDefinition(6, "Tree01", false, "#2d6b30", TileCategory.Decoration, Height: 2f, ModelId: "Tree01", BaselineSize: 6.0f),
        [7] = new TileDefinition(7, "Bush01", false, "#3d8b40", TileCategory.Decoration, Height: 0.8f, ModelId: "Bush01"),
        [8] = new TileDefinition(8, "Flower01", true, "#e074a8", TileCategory.Decoration, ModelId: "Flower01"),
        [9] = new TileDefinition(9, "Flowers01", true, "#d4689c", TileCategory.Decoration, ModelId: "Flowers01"),

        // ── Anime Forest — Structures (10) ──
        [10] = new TileDefinition(10, "Bridge01", true, "#8b6b3d", TileCategory.Structure, ModelId: "Bridge01"),

        // ── Base Tiles — Paths (12-13) ──
        [12] = new TileDefinition(12, "Path01", true, "#d4a574", TileCategory.Terrain, TexturePath: "base_tiles/terrain/Path.png"),
        [13] = new TileDefinition(13, "Path02", true, "#c89b6a", TileCategory.Terrain, TexturePath: "base_tiles/terrain/Path.png"),

        // ── Base Tiles — Indoor (14) ──
        [14] = new TileDefinition(14, "IndoorFloor", true, "#8b7d6b", TileCategory.Terrain, TexturePath: "base_tiles/terrain/Indoor_Floor.png"),

        // ── Stylized Rocks pack (19-22) ──
        [19] = new TileDefinition(19, "StylizedRock01", false, "#7a6b5f", TileCategory.Decoration, Height: 1f, ModelId: "Rock_1", BaselineSize: 1.5f),
        [20] = new TileDefinition(20, "StylizedRock02", false, "#6e6055", TileCategory.Decoration, Height: 1f, ModelId: "Rock_2", BaselineSize: 1.5f),
        [21] = new TileDefinition(21, "StylizedRock03", false, "#62554b", TileCategory.Decoration, Height: 1.2f, ModelId: "Rock_3", BaselineSize: 2f),
        [22] = new TileDefinition(22, "StylizedRock04", false, "#564a41", TileCategory.Decoration, Height: 1.2f, ModelId: "Rock_4", BaselineSize: 2f),

        // ── Anime Trees pack — Trees (45-52) ──
        [45] = new TileDefinition(45, "AnimeTree01", false, "#2d8a30", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_01", BaselineSize: 5.0f),
        [46] = new TileDefinition(46, "AnimeTree02", false, "#258025", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_02", BaselineSize: 5.0f),
        [47] = new TileDefinition(47, "AnimeTree03", false, "#1d7520", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_03", BaselineSize: 5.0f),
        [48] = new TileDefinition(48, "AnimeTree04", false, "#2d6a2d", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_04", BaselineSize: 5.0f),
        [49] = new TileDefinition(49, "AnimeTree05", false, "#1a5c2a", TileCategory.Decoration, Height: 2.5f, ModelId: "AnimeTree_05", BaselineSize: 3f),
        [50] = new TileDefinition(50, "AnimeTree06", false, "#166024", TileCategory.Decoration, Height: 2.5f, ModelId: "AnimeTree_06", BaselineSize: 3f),
        [51] = new TileDefinition(51, "AnimeTree07", false, "#207030", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_07", BaselineSize: 2.5f),
        [52] = new TileDefinition(52, "AnimeTree08", false, "#309035", TileCategory.Decoration, Height: 2f, ModelId: "AnimeTree_08", BaselineSize: 2.5f),

        // ── Anime Trees pack — Bushes (53-56) ──
        [53] = new TileDefinition(53, "AnimeBush01", false, "#3d9040", TileCategory.Decoration, Height: 0.5f, ModelId: "AnimeBush_01", BaselineSize: 1f),
        [54] = new TileDefinition(54, "AnimeBush02", false, "#358538", TileCategory.Decoration, Height: 0.5f, ModelId: "AnimeBush_02", BaselineSize: 1f),
        [55] = new TileDefinition(55, "AnimeBush03", false, "#2d7a30", TileCategory.Decoration, Height: 0.5f, ModelId: "AnimeBush_03", BaselineSize: 1f),
        [56] = new TileDefinition(56, "AnimeBush04", false, "#257025", TileCategory.Decoration, Height: 0.5f, ModelId: "AnimeBush_04", BaselineSize: 1f),

        // ── Anime Trees pack — Grass (57-59) ──
        [57] = new TileDefinition(57, "TallGrass", true, "#5ca050", TileCategory.Encounter, ModelId: "Grass01", BaselineSize: 1.0f),
        [58] = new TileDefinition(58, "AnimeGrassMesh", true, "#54964a", TileCategory.Encounter, ModelId: "GrassMesh", BaselineSize: 0.8f),
        [59] = new TileDefinition(59, "ShortGrass", true, "#6ab85a", TileCategory.Decoration, ModelId: "Grass", BaselineSize: 0.3f, AlphaCutout: false),

        // ── RPG Free — Nature (60-88) ──
        [60] = new TileDefinition(60, "RpgBush01", false, "#3a8040", TileCategory.Decoration, Height: 0.6f, ModelId: "rpgpp_lt_bush_01", BaselineSize: 1f),
        [61] = new TileDefinition(61, "RpgBush02", false, "#358538", TileCategory.Decoration, Height: 0.6f, ModelId: "rpgpp_lt_bush_02", BaselineSize: 1f),
        [62] = new TileDefinition(62, "RpgCloud01", true, "#d0d8e0", TileCategory.Decoration, ModelId: "rpgpp_lt_cloud_01", BaselineSize: 3f),
        [63] = new TileDefinition(63, "RpgCloud02", true, "#c8d0d8", TileCategory.Decoration, ModelId: "rpgpp_lt_cloud_02", BaselineSize: 3f),
        [64] = new TileDefinition(64, "RpgFlower01", true, "#e07090", TileCategory.Decoration, ModelId: "rpgpp_lt_flower_01", BaselineSize: 0.4f),
        [65] = new TileDefinition(65, "RpgFlower02", true, "#d06888", TileCategory.Decoration, ModelId: "rpgpp_lt_flower_02", BaselineSize: 0.4f),
        [66] = new TileDefinition(66, "RpgFlower03", true, "#c06080", TileCategory.Decoration, ModelId: "rpgpp_lt_flower_03", BaselineSize: 0.4f),
        [67] = new TileDefinition(67, "RpgGrassSmall01a", true, "#5ca050", TileCategory.Decoration, ModelId: "rpgpp_lt_grass_small_01a", BaselineSize: 0.3f),
        [68] = new TileDefinition(68, "RpgGrassSmall01b", true, "#54964a", TileCategory.Decoration, ModelId: "rpgpp_lt_grass_small_01b", BaselineSize: 0.3f),
        [69] = new TileDefinition(69, "RpgHillSmall01", false, "#6b7b5f", TileCategory.Structure, Height: 1.5f, ModelId: "rpgpp_lt_hill_small_01", BaselineSize: 3f),
        [70] = new TileDefinition(70, "RpgHillSmall02", false, "#5f6f53", TileCategory.Structure, Height: 1.5f, ModelId: "rpgpp_lt_hill_small_02", BaselineSize: 3f),
        [71] = new TileDefinition(71, "RpgMountain01", false, "#6b5b4f", TileCategory.Structure, Height: 3f, ModelId: "rpgpp_lt_mountain_01", BaselineSize: 6f),
        [72] = new TileDefinition(72, "RpgPlant01", true, "#4a9040", TileCategory.Decoration, ModelId: "rpgpp_lt_plant_01", BaselineSize: 0.5f),
        [73] = new TileDefinition(73, "RpgPlant02", true, "#408838", TileCategory.Decoration, ModelId: "rpgpp_lt_plant_02", BaselineSize: 0.5f),
        [74] = new TileDefinition(74, "RpgRock01", false, "#7a7a6a", TileCategory.Decoration, Height: 0.8f, ModelId: "rpgpp_lt_rock_01", BaselineSize: 1.2f),
        [75] = new TileDefinition(75, "RpgRock02", false, "#6e6e5e", TileCategory.Decoration, Height: 0.8f, ModelId: "rpgpp_lt_rock_02", BaselineSize: 1.2f),
        [76] = new TileDefinition(76, "RpgRock03", false, "#626252", TileCategory.Decoration, Height: 0.8f, ModelId: "rpgpp_lt_rock_03", BaselineSize: 1.2f),
        [77] = new TileDefinition(77, "RpgRockSmall01", true, "#8a8a7a", TileCategory.Decoration, ModelId: "rpgpp_lt_rock_small_01", BaselineSize: 0.5f),
        [78] = new TileDefinition(78, "RpgRockSmall02", true, "#7e7e6e", TileCategory.Decoration, ModelId: "rpgpp_lt_rock_small_02", BaselineSize: 0.5f),
        [79] = new TileDefinition(79, "RpgRocksTiny01", true, "#9a9080", TileCategory.Decoration, ModelId: "rpgpp_lt_rocks_tiny_01", BaselineSize: 0.3f),
        [80] = new TileDefinition(80, "RpgSky01", true, "#88b8e0", TileCategory.Decoration, ModelId: "rpgpp_lt_sky_01", BaselineSize: 10f),
        [81] = new TileDefinition(81, "RpgTerrainGrass01", true, "#4a7c3f", TileCategory.Terrain, ModelId: "rpgpp_lt_terrain_grass_01", BaselineSize: 1f),
        [82] = new TileDefinition(82, "RpgTerrainGrass02", true, "#3e7035", TileCategory.Terrain, ModelId: "rpgpp_lt_terrain_grass_02", BaselineSize: 1f),
        [83] = new TileDefinition(83, "RpgTerrainPath01a", true, "#d4a574", TileCategory.Terrain, ModelId: "rpgpp_lt_terrain_path_01a", BaselineSize: 1f),
        [84] = new TileDefinition(84, "RpgTerrainPath01b", true, "#c89b6a", TileCategory.Terrain, ModelId: "rpgpp_lt_terrain_path_01b", BaselineSize: 1f),
        [85] = new TileDefinition(85, "RpgTerrainSand01", true, "#d4c090", TileCategory.Terrain, ModelId: "rpgpp_lt_terrain_sand_01", BaselineSize: 1f),
        [86] = new TileDefinition(86, "RpgTree01", false, "#2d7030", TileCategory.Decoration, Height: 2f, ModelId: "rpgpp_lt_tree_01", BaselineSize: 4f),
        [87] = new TileDefinition(87, "RpgTree02", false, "#256828", TileCategory.Decoration, Height: 2f, ModelId: "rpgpp_lt_tree_02", BaselineSize: 4f),
        [88] = new TileDefinition(88, "RpgTreePine01", false, "#1d5520", TileCategory.Decoration, Height: 2.5f, ModelId: "rpgpp_lt_tree_pine_01", BaselineSize: 4f),

        // ── RPG Free — Exterior (89-98) ──
        [89] = new TileDefinition(89, "RpgAwning01a", true, "#a08060", TileCategory.Structure, ModelId: "rpgpp_lt_awning_standing_01a", BaselineSize: 2f),
        [90] = new TileDefinition(90, "RpgAwning01b", true, "#988058", TileCategory.Structure, ModelId: "rpgpp_lt_awning_standing_01b", BaselineSize: 2f),
        [91] = new TileDefinition(91, "RpgFenceWood01a", false, "#8b6b3d", TileCategory.Structure, Height: 0.8f, ModelId: "rpgpp_lt_fence_wood_01a", BaselineSize: 1f),
        [92] = new TileDefinition(92, "RpgFenceWood01b", false, "#836535", TileCategory.Structure, Height: 0.8f, ModelId: "rpgpp_lt_fence_wood_01b", BaselineSize: 1f),
        [93] = new TileDefinition(93, "RpgFenceWood01Corner", false, "#7b5f2d", TileCategory.Structure, Height: 0.8f, ModelId: "rpgpp_lt_fence_wood_01_corner_a", BaselineSize: 1f),
        [94] = new TileDefinition(94, "RpgFenceWood02a", false, "#8b6b3d", TileCategory.Structure, Height: 0.8f, ModelId: "rpgpp_lt_fence_wood_02a", BaselineSize: 1f),
        [95] = new TileDefinition(95, "RpgFenceWood02b", false, "#836535", TileCategory.Structure, Height: 0.8f, ModelId: "rpgpp_lt_fence_wood_02b", BaselineSize: 1f),
        [96] = new TileDefinition(96, "RpgFenceWood02c", false, "#7b5f2d", TileCategory.Structure, Height: 0.8f, ModelId: "rpgpp_lt_fence_wood_02c", BaselineSize: 1f),
        [97] = new TileDefinition(97, "RpgShedWood01", false, "#7a6040", TileCategory.Structure, Height: 2f, ModelId: "rpgpp_lt_shed_wood_01", BaselineSize: 3f),
        [98] = new TileDefinition(98, "RpgShedWood02", false, "#725838", TileCategory.Structure, Height: 2f, ModelId: "rpgpp_lt_shed_wood_02", BaselineSize: 3f),

        // ── RPG Free — Props (99-146) ──
        [99] = new TileDefinition(99, "RpgBanner01a", true, "#c04040", TileCategory.Decoration, ModelId: "rpgpp_lt_banner_01a", BaselineSize: 1.5f),
        [100] = new TileDefinition(100, "RpgBanner01b", true, "#b03838", TileCategory.Decoration, ModelId: "rpgpp_lt_banner_01b", BaselineSize: 1.5f),
        [101] = new TileDefinition(101, "RpgBarrel01", false, "#8b6b3d", TileCategory.Decoration, Height: 0.6f, ModelId: "rpgpp_lt_barrel_01", BaselineSize: 0.8f),
        [102] = new TileDefinition(102, "RpgBarrel02", false, "#836535", TileCategory.Decoration, Height: 0.6f, ModelId: "rpgpp_lt_barrel_02", BaselineSize: 0.8f),
        [103] = new TileDefinition(103, "RpgBasket01", true, "#a08858", TileCategory.Decoration, ModelId: "rpgpp_lt_basket_01", BaselineSize: 0.5f),
        [104] = new TileDefinition(104, "RpgBasket02", true, "#988050", TileCategory.Decoration, ModelId: "rpgpp_lt_basket_02", BaselineSize: 0.5f),
        [105] = new TileDefinition(105, "RpgBathtubWood01", false, "#7a6040", TileCategory.Decoration, Height: 0.5f, ModelId: "rpgpp_lt_bathtub_wood_01", BaselineSize: 1.5f),
        [106] = new TileDefinition(106, "RpgBenchWood01", true, "#8b6b3d", TileCategory.Decoration, ModelId: "rpgpp_lt_bench_wood_01", BaselineSize: 1.2f),
        [107] = new TileDefinition(107, "RpgBenchWood02", true, "#836535", TileCategory.Decoration, ModelId: "rpgpp_lt_bench_wood_02", BaselineSize: 1.2f),
        [108] = new TileDefinition(108, "RpgBenchWood03", true, "#7b5f2d", TileCategory.Decoration, ModelId: "rpgpp_lt_bench_wood_03", BaselineSize: 1.2f),
        [109] = new TileDefinition(109, "RpgBirdHouse01", true, "#a09070", TileCategory.Decoration, ModelId: "rpgpp_lt_bird_house_01", BaselineSize: 0.8f),
        [110] = new TileDefinition(110, "RpgBowlMetal01", true, "#808080", TileCategory.Decoration, ModelId: "rpgpp_lt_bowl_metal_01", BaselineSize: 0.3f),
        [111] = new TileDefinition(111, "RpgBoxWood01", true, "#8b7050", TileCategory.Decoration, ModelId: "rpgpp_lt_box_wood_01", BaselineSize: 0.6f),
        [112] = new TileDefinition(112, "RpgBroom01", true, "#7a6040", TileCategory.Decoration, ModelId: "rpgpp_lt_broom_01", BaselineSize: 0.8f),
        [113] = new TileDefinition(113, "RpgBucket01", true, "#808080", TileCategory.Decoration, ModelId: "rpgpp_lt_bucket_01", BaselineSize: 0.5f),
        [114] = new TileDefinition(114, "RpgChair01a", true, "#8b6b3d", TileCategory.Decoration, ModelId: "rpgpp_lt_chair_01a", BaselineSize: 0.8f),
        [115] = new TileDefinition(115, "RpgChair01b", true, "#836535", TileCategory.Decoration, ModelId: "rpgpp_lt_chair_01b", BaselineSize: 0.8f),
        [116] = new TileDefinition(116, "RpgCrate01", false, "#8b7050", TileCategory.Decoration, Height: 0.5f, ModelId: "rpgpp_lt_crate_01", BaselineSize: 0.8f),
        [117] = new TileDefinition(117, "RpgCrate02", false, "#836848", TileCategory.Decoration, Height: 0.5f, ModelId: "rpgpp_lt_crate_02", BaselineSize: 0.8f),
        [118] = new TileDefinition(118, "RpgCrate03", false, "#7b6040", TileCategory.Decoration, Height: 0.5f, ModelId: "rpgpp_lt_crate_03", BaselineSize: 0.8f),
        [119] = new TileDefinition(119, "RpgHangerClothes01", true, "#a09080", TileCategory.Decoration, ModelId: "rpgpp_lt_hanger_clothes_01", BaselineSize: 1f),
        [120] = new TileDefinition(120, "RpgHangerWood01", true, "#8b6b3d", TileCategory.Decoration, ModelId: "rpgpp_lt_hanger_wood_01", BaselineSize: 1f),
        [121] = new TileDefinition(121, "RpgHangerWood02", true, "#836535", TileCategory.Decoration, ModelId: "rpgpp_lt_hanger_wood_02", BaselineSize: 1f),
        [122] = new TileDefinition(122, "RpgJug01", true, "#a08060", TileCategory.Decoration, ModelId: "rpgpp_lt_jug_01", BaselineSize: 0.4f),
        [123] = new TileDefinition(123, "RpgLadder01", true, "#8b6b3d", TileCategory.Decoration, ModelId: "rpgpp_lt_ladder_01", BaselineSize: 1.5f),
        [124] = new TileDefinition(124, "RpgLogWood01", true, "#7a5830", TileCategory.Decoration, ModelId: "rpgpp_lt_log_wood_01", BaselineSize: 1f),
        [125] = new TileDefinition(125, "RpgLogWood02a", true, "#725028", TileCategory.Decoration, ModelId: "rpgpp_lt_log_wood_02a", BaselineSize: 1f),
        [126] = new TileDefinition(126, "RpgLogWood02b", true, "#6a4820", TileCategory.Decoration, ModelId: "rpgpp_lt_log_wood_02b", BaselineSize: 1f),
        [127] = new TileDefinition(127, "RpgPackage01", true, "#c0a878", TileCategory.Decoration, ModelId: "rpgpp_lt_package_01", BaselineSize: 0.5f),
        [128] = new TileDefinition(128, "RpgRake01", true, "#7a6040", TileCategory.Decoration, ModelId: "rpgpp_lt_rake_01", BaselineSize: 0.8f),
        [129] = new TileDefinition(129, "RpgSack01", true, "#b09868", TileCategory.Decoration, ModelId: "rpgpp_lt_sack_01", BaselineSize: 0.5f),
        [130] = new TileDefinition(130, "RpgSack02", true, "#a89060", TileCategory.Decoration, ModelId: "rpgpp_lt_sack_02", BaselineSize: 0.5f),
        [131] = new TileDefinition(131, "RpgSack02Set", true, "#a08858", TileCategory.Decoration, ModelId: "rpgpp_lt_sack_02_set", BaselineSize: 0.8f),
        [132] = new TileDefinition(132, "RpgSackOpen01", true, "#b09868", TileCategory.Decoration, ModelId: "rpgpp_lt_sack_open_01", BaselineSize: 0.5f),
        [133] = new TileDefinition(133, "RpgShieldWall01a", false, "#808080", TileCategory.Decoration, Height: 0.6f, ModelId: "rpgpp_lt_shield_wall_01a", BaselineSize: 0.8f),
        [134] = new TileDefinition(134, "RpgShieldWall01b", false, "#787878", TileCategory.Decoration, Height: 0.6f, ModelId: "rpgpp_lt_shield_wall_01b", BaselineSize: 0.8f),
        [135] = new TileDefinition(135, "RpgStones01", true, "#8a8a7a", TileCategory.Decoration, ModelId: "rpgpp_lt_stones_01", BaselineSize: 0.5f),
        [136] = new TileDefinition(136, "RpgTable01", false, "#8b6b3d", TileCategory.Decoration, Height: 0.6f, ModelId: "rpgpp_lt_table_01", BaselineSize: 1.5f),
        [137] = new TileDefinition(137, "RpgTrough01", true, "#7a6040", TileCategory.Decoration, ModelId: "rpgpp_lt_trough_01", BaselineSize: 1f),
        [138] = new TileDefinition(138, "RpgVase01", true, "#a08060", TileCategory.Decoration, ModelId: "rpgpp_lt_vase_01", BaselineSize: 0.5f),
        [139] = new TileDefinition(139, "RpgVase02", true, "#988058", TileCategory.Decoration, ModelId: "rpgpp_lt_vase_02", BaselineSize: 0.5f),
        [140] = new TileDefinition(140, "RpgVase03", true, "#908050", TileCategory.Decoration, ModelId: "rpgpp_lt_vase_03", BaselineSize: 0.5f),
        [141] = new TileDefinition(141, "RpgWagon01", false, "#8b6b3d", TileCategory.Structure, Height: 1f, ModelId: "rpgpp_lt_wagon_01", BaselineSize: 2.5f),
        [142] = new TileDefinition(142, "RpgWell01", false, "#7a7a7a", TileCategory.Structure, Height: 1f, ModelId: "rpgpp_lt_well_01", BaselineSize: 1.5f),
        [143] = new TileDefinition(143, "RpgWoodPath01a", true, "#8b6b3d", TileCategory.Terrain, ModelId: "rpgpp_lt_wood_path_01a", BaselineSize: 1f),
        [144] = new TileDefinition(144, "RpgWoodPath01b", true, "#836535", TileCategory.Terrain, ModelId: "rpgpp_lt_wood_path_01b", BaselineSize: 1f),

        // ── RPG Free — Structures (145-149) ──
        [145] = new TileDefinition(145, "RpgBuilding01", false, "#8b7860", TileCategory.Structure, Height: 3f, ModelId: "rpgpp_lt_building_01", BaselineSize: 4f),
        [146] = new TileDefinition(146, "RpgBuilding02", false, "#837058", TileCategory.Structure, Height: 3f, ModelId: "rpgpp_lt_building_02", BaselineSize: 4f),
        [147] = new TileDefinition(147, "RpgBuilding03", false, "#7b6850", TileCategory.Structure, Height: 3f, ModelId: "rpgpp_lt_building_03", BaselineSize: 4f),
        [148] = new TileDefinition(148, "RpgBuilding04", false, "#736048", TileCategory.Structure, Height: 3f, ModelId: "rpgpp_lt_building_04", BaselineSize: 4f),
        [149] = new TileDefinition(149, "RpgBuilding05", false, "#6b5840", TileCategory.Structure, Height: 3f, ModelId: "rpgpp_lt_building_05", BaselineSize: 4f),
    };

    public static TileDefinition? GetTile(int id) =>
        _tiles.TryGetValue(id, out var tile) ? tile : null;

    public static IEnumerable<TileDefinition> GetTilesByCategory(TileCategory category) =>
        _tiles.Values.Where(t => t.Category == category);

    public static IEnumerable<TileDefinition> AllTiles => _tiles.Values;

    public static int Count => _tiles.Count;
}
