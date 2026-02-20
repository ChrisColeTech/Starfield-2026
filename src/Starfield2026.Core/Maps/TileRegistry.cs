using System.Collections.Generic;
using System.Linq;

namespace Starfield2026.Core.Maps;

public static class TileRegistry
{
    private static readonly Dictionary<int, TileDefinition> _tiles = new()
    {
        // Terrain (0-15)
        [0] = new TileDefinition(0, "Water", false, "#3890f8", TileCategory.Terrain),
        [1] = new TileDefinition(1, "Grass", true, "#7ec850", TileCategory.Terrain),
        [2] = new TileDefinition(2, "Path", true, "#d4a574", TileCategory.Terrain),
        [3] = new TileDefinition(3, "MetalFloor", true, "#808090", TileCategory.Terrain),
        [4] = new TileDefinition(4, "Bridge", true, "#8b7355", TileCategory.Terrain),
        [5] = new TileDefinition(5, "DeepWater", false, "#2060c0", TileCategory.Terrain),
        [6] = new TileDefinition(6, "Sand", true, "#e8d8a0", TileCategory.Terrain),
        [7] = new TileDefinition(7, "Snow", true, "#f0f8ff", TileCategory.Terrain),
        [8] = new TileDefinition(8, "Ice", true, "#b0e0f8", TileCategory.Terrain, "slippery"),
        [9] = new TileDefinition(9, "Mud", true, "#6b4423", TileCategory.Terrain, "slow"),
        [10] = new TileDefinition(10, "Lava", false, "#ff4500", TileCategory.Terrain),
        [11] = new TileDefinition(11, "Void", false, "#000000", TileCategory.Terrain),
        [12] = new TileDefinition(12, "AsteroidGround", true, "#606060", TileCategory.Terrain),
        [13] = new TileDefinition(13, "CargoBayFloor", true, "#a0522d", TileCategory.Terrain),
        [14] = new TileDefinition(14, "EngineRoom", true, "#8b0000", TileCategory.Terrain),
        [15] = new TileDefinition(15, "TechFloor", true, "#d3d3d3", TileCategory.Terrain),

        // Decoration (16-31)
        [16] = new TileDefinition(16, "Tree", false, "#228b22", TileCategory.Decoration, Height: 2f),
        [17] = new TileDefinition(17, "Rock", false, "#808080", TileCategory.Decoration, Height: 1f),
        [18] = new TileDefinition(18, "Crystal", true, "#ff69b4", TileCategory.Decoration),
        [19] = new TileDefinition(19, "Antenna", false, "#a9a9a9", TileCategory.Decoration, Height: 3f),
        [20] = new TileDefinition(20, "Bush", false, "#2e8b57", TileCategory.Decoration, Height: 0.8f),
        [21] = new TileDefinition(21, "DebrisPile", false, "#8b4513", TileCategory.Decoration, Height: 0.5f),
        [22] = new TileDefinition(22, "Boulder", false, "#696969", TileCategory.Decoration, Height: 1.5f),
        [23] = new TileDefinition(23, "Sign", false, "#deb887", TileCategory.Decoration, "readable", Height: 1f),
        [24] = new TileDefinition(24, "Fence", false, "#cd853f", TileCategory.Decoration, Height: 1f),
        [25] = new TileDefinition(25, "Beacon", false, "#ffa500", TileCategory.Decoration, Height: 2f, AnimationFrames: 4),
        [26] = new TileDefinition(26, "SupplyCrate", false, "#8b4513", TileCategory.Decoration, "openable", Height: 1f),
        [27] = new TileDefinition(27, "Barrel", false, "#a0522d", TileCategory.Decoration, Height: 1f),
        [28] = new TileDefinition(28, "Crate", false, "#d2691e", TileCategory.Decoration, Height: 1f),
        [29] = new TileDefinition(29, "FuelTank", false, "#cd5c5c", TileCategory.Decoration, Height: 1.2f),
        [30] = new TileDefinition(30, "Terminal", false, "#8b4513", TileCategory.Decoration, "readable", Height: 1f),
        [31] = new TileDefinition(31, "Workbench", false, "#deb887", TileCategory.Decoration, Height: 0.9f),

        // Interactive (32-47)
        [32] = new TileDefinition(32, "Door", true, "#8b4513", TileCategory.Interactive, "door", Height: 2f),
        [33] = new TileDefinition(33, "Warp", true, "#9400d3", TileCategory.Interactive, "warp"),
        [34] = new TileDefinition(34, "Computer", false, "#4169e1", TileCategory.Interactive, "pc", Height: 1f),
        [35] = new TileDefinition(35, "RepairStation", false, "#ff6b6b", TileCategory.Interactive, "heal", Height: 1f),
        [36] = new TileDefinition(36, "ShopTerminal", false, "#4682b4", TileCategory.Interactive, "shop", Height: 1f),
        [37] = new TileDefinition(37, "PushBlock", false, "#708090", TileCategory.Interactive, "strength", Height: 1f),
        [38] = new TileDefinition(38, "Barricade", false, "#556b2f", TileCategory.Interactive, "cut", Height: 1.5f),
        [39] = new TileDefinition(39, "WeakWall", false, "#778899", TileCategory.Interactive, "rock_smash", Height: 2f),
        [40] = new TileDefinition(40, "ElevatorUp", true, "#4682b4", TileCategory.Interactive, "stairs_up"),
        [41] = new TileDefinition(41, "ElevatorDown", true, "#1e90ff", TileCategory.Interactive, "stairs_down"),
        [42] = new TileDefinition(42, "Switch", true, "#daa520", TileCategory.Interactive, "switch"),
        [43] = new TileDefinition(43, "PressurePlate", true, "#c0c0c0", TileCategory.Interactive, "pressure_plate"),
        [44] = new TileDefinition(44, "LockedDoor", false, "#654321", TileCategory.Interactive, "locked_door", Height: 2f),
        [45] = new TileDefinition(45, "HiddenItem", true, "#00000000", TileCategory.Interactive, "hidden_item"),
        [46] = new TileDefinition(46, "ItemPickup", true, "#ff0000", TileCategory.Interactive, "item_ball"),
        [47] = new TileDefinition(47, "Teleporter", true, "#00ffff", TileCategory.Interactive, "teleport"),

        // Entity NPCs (48-55)
        [48] = new TileDefinition(48, "Mechanic", false, "#ffd700", TileCategory.Entity, "npc", EntityId: 506),
        [49] = new TileDefinition(49, "Vendor", false, "#ffa500", TileCategory.Entity, "service_npc", EntityId: 103),
        [50] = new TileDefinition(50, "Medic", false, "#ffb6c1", TileCategory.Entity, "nurse", EntityId: 100),
        [51] = new TileDefinition(51, "ShopKeeper", false, "#87ceeb", TileCategory.Entity, "clerk", EntityId: 102),
        [52] = new TileDefinition(52, "Commander", false, "#98fb98", TileCategory.Entity, "commander", EntityId: 300),
        [53] = new TileDefinition(53, "Rival", false, "#ff4500", TileCategory.Entity, "rival", EntityId: 202),
        [54] = new TileDefinition(54, "Scientist", false, "#f5f5dc", TileCategory.Entity, "professor", EntityId: 200),
        [55] = new TileDefinition(55, "Ally", false, "#ffb6c1", TileCategory.Entity, "ally", EntityId: 205),

        // Trainers (56-71) — enemy pilots / challengers
        [56] = new TileDefinition(56, "PilotRookie", false, "#dc143c", TileCategory.Trainer, "trainer", EntityId: 0),
        [57] = new TileDefinition(57, "PilotScout", false, "#dc143c", TileCategory.Trainer, "trainer", EntityId: 10),
        [58] = new TileDefinition(58, "PilotRanger", false, "#dc143c", TileCategory.Trainer, "trainer", EntityId: 12),
        [59] = new TileDefinition(59, "PilotVeteran", false, "#dc143c", TileCategory.Trainer, "trainer", EntityId: 13),
        [60] = new TileDefinition(60, "PilotAce", false, "#b22222", TileCategory.Trainer, "trainer", EntityId: 15),
        [61] = new TileDefinition(61, "SquadLeader", false, "#8b0000", TileCategory.Trainer, "gym_leader", EntityId: 300),
        [62] = new TileDefinition(62, "ElitePilot", false, "#4b0082", TileCategory.Trainer, "elite_four", EntityId: 302),
        [63] = new TileDefinition(63, "FleetAdmiral", false, "#ffd700", TileCategory.Trainer, "champion", EntityId: 303),
        [64] = new TileDefinition(64, "PirateGrunt", false, "#2f4f4f", TileCategory.Trainer, "pirate", EntityId: 400),
        [65] = new TileDefinition(65, "PirateGunner", false, "#00bfff", TileCategory.Trainer, "pirate_b", EntityId: 401),
        [66] = new TileDefinition(66, "PirateCaptain", false, "#ff4500", TileCategory.Trainer, "pirate_boss", EntityId: 402),
        [67] = new TileDefinition(67, "ResearcherDrone", false, "#483d8b", TileCategory.Trainer, "drone", EntityId: 105),
        [68] = new TileDefinition(68, "BountyHunter", false, "#87cefa", TileCategory.Trainer, "bounty", EntityId: 109),
        [69] = new TileDefinition(69, "Smuggler", false, "#ff6347", TileCategory.Trainer, "smuggler", EntityId: 107),
        [70] = new TileDefinition(70, "Cadet", false, "#2f2f2f", TileCategory.Trainer, "cadet", EntityId: 500),
        [71] = new TileDefinition(71, "Admiral", false, "#ff1493", TileCategory.Trainer, "admiral", EntityId: 502),

        // Encounter zones (72-79)
        [72] = new TileDefinition(72, "NebulaZone", true, "#5a9c3a", TileCategory.Encounter, "wild_encounter"),
        [73] = new TileDefinition(73, "AsteroidField", true, "#4a8c2a", TileCategory.Encounter, "rare_encounter"),
        [74] = new TileDefinition(74, "DarkNebula", true, "#3a7c1a", TileCategory.Encounter, "double_encounter"),
        [75] = new TileDefinition(75, "CaveEncounter", true, "#505050", TileCategory.Encounter, "cave_encounter"),
        [76] = new TileDefinition(76, "SpaceDebrisField", false, "#3890f8", TileCategory.Encounter, "water_encounter"),
        [77] = new TileDefinition(77, "GasCloud", false, "#4090ff", TileCategory.Encounter, "surf_encounter"),
        [78] = new TileDefinition(78, "MiningSpot", false, "#2080e8", TileCategory.Encounter, "fishing"),
        [79] = new TileDefinition(79, "SalvagePoint", false, "#228b22", TileCategory.Encounter, "headbutt"),

        // Structure (80-95)
        [80] = new TileDefinition(80, "Wall", false, "#404040", TileCategory.Structure, Height: 2f),
        [81] = new TileDefinition(81, "LedgeDown", true, "#7ec850", TileCategory.Structure, "ledge_down"),
        [82] = new TileDefinition(82, "LedgeLeft", true, "#7ec850", TileCategory.Structure, "ledge_left"),
        [83] = new TileDefinition(83, "LedgeRight", true, "#7ec850", TileCategory.Structure, "ledge_right"),
        [84] = new TileDefinition(84, "Blocked", false, "#303030", TileCategory.Structure, Height: 2f),
        [85] = new TileDefinition(85, "ConveyorUp", true, "#a0a0a0", TileCategory.Structure, "spin_up"),
        [86] = new TileDefinition(86, "ConveyorDown", true, "#a0a0a0", TileCategory.Structure, "spin_down"),
        [87] = new TileDefinition(87, "ConveyorLeft", true, "#a0a0a0", TileCategory.Structure, "spin_left"),
        [88] = new TileDefinition(88, "ConveyorRight", true, "#a0a0a0", TileCategory.Structure, "spin_right"),
        [89] = new TileDefinition(89, "StairsUp", true, "#8b7355", TileCategory.Structure, "stairs_up"),
        [90] = new TileDefinition(90, "StairsDown", true, "#8b7355", TileCategory.Structure, "stairs_down"),
        [91] = new TileDefinition(91, "Ladder", true, "#a0522d", TileCategory.Structure, "ladder"),
        [92] = new TileDefinition(92, "Ramp", true, "#9b8b7b", TileCategory.Structure, "ramp"),
        [93] = new TileDefinition(93, "Cliff", false, "#5a5a5a", TileCategory.Structure, Height: 3f),
        [94] = new TileDefinition(94, "Waterfall", false, "#4090ff", TileCategory.Structure, "waterfall_hm", AnimationFrames: 4),
        [95] = new TileDefinition(95, "EnergyBarrier", false, "#3080e8", TileCategory.Structure, "barrier", AnimationFrames: 4, Height: 2f),

        // Items (96-111)
        [96] = new TileDefinition(96, "AmmoPickup", true, "#ff0000", TileCategory.Item, "item", EntityId: 0),
        [97] = new TileDefinition(97, "HealthPack", true, "#ff0000", TileCategory.Item, "item", EntityId: 1),
        [98] = new TileDefinition(98, "ShieldBoost", true, "#ff0000", TileCategory.Item, "item", EntityId: 2),
        [99] = new TileDefinition(99, "SpecialWeapon", true, "#ff0000", TileCategory.Item, "item", EntityId: 3),
        [100] = new TileDefinition(100, "RepairKit", true, "#9370db", TileCategory.Item, "item", EntityId: 100),
        [101] = new TileDefinition(101, "FuelCell", true, "#9370db", TileCategory.Item, "item", EntityId: 101),
        [102] = new TileDefinition(102, "BoostModule", true, "#9370db", TileCategory.Item, "item", EntityId: 102),
        [103] = new TileDefinition(103, "FullRepair", true, "#9370db", TileCategory.Item, "item", EntityId: 104),
        [104] = new TileDefinition(104, "FireCrystal", true, "#ffa500", TileCategory.Item, "item", EntityId: 300),
        [105] = new TileDefinition(105, "IceCrystal", true, "#00bfff", TileCategory.Item, "item", EntityId: 301),
        [106] = new TileDefinition(106, "LightningCrystal", true, "#ffff00", TileCategory.Item, "item", EntityId: 302),
        [107] = new TileDefinition(107, "NatureCrystal", true, "#00ff00", TileCategory.Item, "item", EntityId: 303),
        [108] = new TileDefinition(108, "DarkCrystal", true, "#c0c0c0", TileCategory.Item, "item", EntityId: 304),
        [109] = new TileDefinition(109, "StarCrystal", true, "#ffd700", TileCategory.Item, "item", EntityId: 305),
        [110] = new TileDefinition(110, "Ration", true, "#ff6b6b", TileCategory.Item, "item", EntityId: 200),
        [111] = new TileDefinition(111, "EnergyCell", true, "#ff0000", TileCategory.Item, "item", EntityId: 500),

        // Transition (112-115)
        [112] = new TileDefinition(112, "Transition North", true, "#00cc88", TileCategory.Transition),
        [113] = new TileDefinition(113, "Transition South", true, "#00aaff", TileCategory.Transition),
        [114] = new TileDefinition(114, "Transition West", true, "#ff8800", TileCategory.Transition),
        [115] = new TileDefinition(115, "Transition East", true, "#cc44ff", TileCategory.Transition),

        // Spawn points (116-119)
        [116] = new TileDefinition(116, "PlayerSpawn", true, "#00ff00", TileCategory.Spawn, "player_spawn"),
        [117] = new TileDefinition(117, "EnemySpawn", true, "#ff0000", TileCategory.Spawn, "enemy_spawn"),
        [118] = new TileDefinition(118, "BossSpawn", true, "#ff00ff", TileCategory.Spawn, "boss_spawn"),
        [119] = new TileDefinition(119, "ItemSpawn", true, "#ffff00", TileCategory.Spawn, "item_spawn"),
    };

    public static TileDefinition? GetTile(int id) =>
        _tiles.TryGetValue(id, out var tile) ? tile : null;

    public static IEnumerable<TileDefinition> GetTilesByCategory(TileCategory category) =>
        _tiles.Values.Where(t => t.Category == category);

    public static IEnumerable<TileDefinition> AllTiles => _tiles.Values;

    public static int Count => _tiles.Count;
}
