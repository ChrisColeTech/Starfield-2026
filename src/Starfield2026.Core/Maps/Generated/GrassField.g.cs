using Starfield2026.Core.Encounters;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Maps.Generated;

/// <summary>
/// Grass Field — a 32x32 outdoor area with grass, trees, rocks,
/// and a building entrance that warps to Home Base Center.
/// </summary>
public sealed class GrassField : MapDefinition
{
    // Tile IDs used:
    //  1  = Grass (walkable, green)
    //  5  = TechFloor (walkable, dark blue-gray)
    // 16  = Tree (non-walkable, decoration)
    // 17  = Rock (non-walkable, decoration)
    // 32  = Door (walkable, interactive)
    // 72  = NebulaZone (walkable, encounter)
    // 80  = Wall (non-walkable, structure)
    // 116 = PlayerSpawn (walkable, spawn)

    private static readonly int[] BaseTileData = BuildGrid();

    private static int[] BuildGrid()
    {
        const int W = 32, H = 32;
        int[] data = new int[W * H];

        // Fill everything with grass
        for (int i = 0; i < data.Length; i++)
            data[i] = 1; // Grass

        // Border walls
        for (int x = 0; x < W; x++)
        {
            data[0 * W + x] = 80;      // Top wall
            data[(H - 1) * W + x] = 80; // Bottom wall
        }
        for (int y = 0; y < H; y++)
        {
            data[y * W + 0] = 80;      // Left wall
            data[y * W + (W - 1)] = 80; // Right wall
        }

        // Player spawn at center
        data[16 * W + 16] = 116;

        // Building entrance (door) at bottom-center — warps to Home Base Center
        data[(H - 1) * W + 16] = 32;

        // Tech floor path leading to the building entrance (2-wide path)
        for (int y = 20; y < H - 1; y++)
        {
            data[y * W + 15] = 5;
            data[y * W + 16] = 5;
        }

        // Tech floor entrance pad
        data[19 * W + 14] = 5;
        data[19 * W + 15] = 5;
        data[19 * W + 16] = 5;
        data[19 * W + 17] = 5;

        // Decorative trees (scattered)
        int[] treePositions = [
            3 * W + 3,  3 * W + 10,  3 * W + 22,  3 * W + 28,
            8 * W + 5,  8 * W + 26,
            13 * W + 3, 13 * W + 28,
            18 * W + 5, 18 * W + 26,
            25 * W + 3, 25 * W + 10, 25 * W + 22, 25 * W + 28,
        ];
        foreach (int pos in treePositions)
            data[pos] = 16;

        // Decorative rocks
        int[] rockPositions = [
            5 * W + 7,  5 * W + 24,
            10 * W + 14, 10 * W + 18,
            22 * W + 7,  22 * W + 24,
        ];
        foreach (int pos in rockPositions)
            data[pos] = 17;

        // Encounter zone (NebulaZone) — 4x4 area in the northeast
        for (int y = 4; y < 8; y++)
            for (int x = 24; x < 28; x++)
                data[y * W + x] = 72;

        return data;
    }

    private static readonly int?[] OverlayTileData = new int?[32 * 32];

    private static readonly int[] WalkableTileIds = [1, 5, 32, 33, 72, 116];

    private static readonly EncounterTable[] _encounterGroupsData =
    [
        new() { EncounterType = "wild_encounter", BaseEncounterRate = 20, Entries =
        [
            new() { SpeciesId = 1, MinLevel = 1, MaxLevel = 3, Weight = 60 },
            new() { SpeciesId = 2, MinLevel = 2, MaxLevel = 5, Weight = 30 },
            new() { SpeciesId = 3, MinLevel = 3, MaxLevel = 6, Weight = 10 },
        ] },
    ];

    // Instance MUST be declared AFTER all static data fields to avoid null references
    public static GrassField Instance { get; } = new();

    private GrassField()
        : base("home_base", "grass_field", "Grass Field",
               32, 32, 1,
               BaseTileData, OverlayTileData, WalkableTileIds,
               warps: [new WarpConnection(16, 31, "home_base_center", 8, 1, WarpTrigger.Step)],
               connections: null,
               worldX: 0, worldY: -1,
               encounterGroups: _encounterGroupsData,
               progressMultiplier: 0.2f)
    { }
}
