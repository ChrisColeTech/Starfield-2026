using Starfield2026.Core.Encounters;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Maps.Generated;

/// <summary>
/// The overworld grid — a large 500x500 open area rendered as the cyan wireframe
/// floor by GridRenderer. No walls, no boundaries visible — the classic sci-fi holodeck look.
/// Contains a warp door to Home Base Center near the center.
/// </summary>
public sealed class OverworldGrid : MapDefinition
{
    // Tile IDs used:
    //  5  = TechFloor (walkable, the grid floor — rendered by GridRenderer, not MapRenderer3D)
    // 32  = Door (walkable, interactive — warp to HomeBaseCenter)
    // 72  = NebulaZone (walkable, encounter zone)
    // 116 = PlayerSpawn (walkable, spawn)

    /// <summary>Whether this map should use the wireframe GridRenderer instead of MapRenderer3D.</summary>
    public override bool UseWireframeGrid => true;

    private static readonly int[] BaseTileData = BuildGrid();

    private static int[] BuildGrid()
    {
        const int W = 500, H = 500;
        int[] data = new int[W * H];

        // Fill everything with TechFloor (walkable, transparent — GridRenderer draws the visuals)
        for (int i = 0; i < data.Length; i++)
            data[i] = 5;

        // Player spawn one tile north of center
        data[249 * W + 250] = 116;

        // Building entrance (door) at dead center — warps to Home Base Center
        data[250 * W + 250] = 32;

        // Encounter zones (NebulaZone) — scattered 8x8 areas
        // Northeast
        for (int y = 100; y < 108; y++)
            for (int x = 350; x < 358; x++)
                data[y * W + x] = 72;
        // Southwest
        for (int y = 375; y < 383; y++)
            for (int x = 125; x < 133; x++)
                data[y * W + x] = 72;

        return data;
    }

    private static readonly int?[] OverlayTileData = new int?[500 * 500];

    // Everything is walkable on the grid — no walls
    private static readonly int[] WalkableTileIds = [5, 32, 33, 72, 116];

    private static readonly EncounterTable[] _encounterGroupsData =
    [
        new() { EncounterType = "wild_encounter", BaseEncounterRate = 15, Entries =
        [
            new() { SpeciesId = 1, MinLevel = 1, MaxLevel = 3, Weight = 60 },
            new() { SpeciesId = 2, MinLevel = 2, MaxLevel = 5, Weight = 30 },
            new() { SpeciesId = 3, MinLevel = 3, MaxLevel = 6, Weight = 10 },
        ] },
    ];

    // Instance MUST be declared AFTER all static data fields to avoid null references
    public static OverworldGrid Instance { get; } = new();

    private OverworldGrid()
        : base("home_base", "overworld_grid", "Overworld Grid",
               500, 500, 1,
               BaseTileData, OverlayTileData, WalkableTileIds,
               warps: [new WarpConnection(250, 250, "home_base_center", 8, 1, WarpTrigger.Step)],
               connections: null,
               worldX: 0, worldY: 0,
               encounterGroups: _encounterGroupsData,
               progressMultiplier: 0.1f)
    { }
}
