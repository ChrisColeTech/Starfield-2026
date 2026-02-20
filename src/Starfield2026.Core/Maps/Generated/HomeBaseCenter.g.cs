using Starfield2026.Core.Encounters;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Maps.Generated;

/// <summary>
/// Home Base center map — 16x16 metal floor with walls, a warp to the hangar,
/// and an encounter zone in the northeast corner.
/// </summary>
public sealed class HomeBaseCenter : MapDefinition
{
    // Tile IDs used:
    //  3  = MetalFloor (walkable)
    // 80  = Wall (non-walkable, height 2)
    // 32  = Door (walkable, interactive)
    // 72  = NebulaZone (walkable, encounter)
    // 34  = Computer (non-walkable, interactive)
    // 116 = PlayerSpawn (walkable, spawn)

    private static readonly int[] BaseTileData =
    [
        // Row 0 (top wall with door to grid at col 8)
        80, 80, 80, 80, 80, 80, 80, 80, 32, 80, 80, 80, 80, 80, 80, 80,
        // Row 1
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 2
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 3
        80,  3,  3, 34,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 4
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 72, 72,  3, 80,
        // Row 5
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 72, 72,  3, 80,
        // Row 6
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 7
        80,  3,  3,  3,  3,  3,  3,116,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 8
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 9
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 10
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 11
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 12
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 13
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 14
        80,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, 80,
        // Row 15 (bottom wall with door to hangar at center)
        80, 80, 80, 80, 80, 80, 80, 32, 80, 80, 80, 80, 80, 80, 80, 80,
    ];

    private static readonly int?[] OverlayTileData = new int?[16 * 16];

    private static readonly int[] WalkableTileIds = [3, 32, 33, 72, 116];

    private static readonly EncounterTable[] _encounterGroupsData =
    [
        new() { EncounterType = "wild_encounter", BaseEncounterRate = 26, Entries =
        [
            new() { SpeciesId = 1, MinLevel = 1, MaxLevel = 3, Weight = 50 },
            new() { SpeciesId = 2, MinLevel = 2, MaxLevel = 4, Weight = 30 },
        ] },
    ];

    // Instance MUST be declared AFTER all static data fields to avoid null references
    public static HomeBaseCenter Instance { get; } = new();

    private HomeBaseCenter()
        : base("home_base", "home_base_center", "Home Base - Center",
               16, 16, 1,
               BaseTileData, OverlayTileData, WalkableTileIds,
               warps: [
                   new WarpConnection(7, 15, "home_base_hangar", 7, 0, WarpTrigger.Step),
                   new WarpConnection(8, 0, "overworld_grid", 16, 30, WarpTrigger.Step),
               ],
               connections: null,
               worldX: 0, worldY: 0,
               encounterGroups: _encounterGroupsData,
               progressMultiplier: 0.3f)
    { }
}
