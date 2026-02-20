using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Maps.Generated;

/// <summary>
/// Home Base hangar map — 16x16, connected south of the center.
/// Cargo bay themed with crates and a door back to center.
/// </summary>
public sealed class HomeBaseHangar : MapDefinition
{
    // Tile IDs used:
    // 13  = CargoBayFloor (walkable)
    // 80  = Wall (non-walkable)
    // 32  = Door (walkable, interactive)
    // 28  = Crate (non-walkable, decoration)
    // 27  = Barrel (non-walkable, decoration)

    private static readonly int[] BaseTileData =
    [
        // Row 0 (top wall with door from center)
        80, 80, 80, 80, 80, 80, 80, 32, 80, 80, 80, 80, 80, 80, 80, 80,
        // Row 1
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 2
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 3
        80, 13, 28, 28, 13, 13, 13, 13, 13, 13, 13, 13, 28, 28, 13, 80,
        // Row 4
        80, 13, 28, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 28, 13, 80,
        // Row 5
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 6
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 7
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 8
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 9
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 10
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 11
        80, 13, 27, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 27, 13, 80,
        // Row 12
        80, 13, 27, 27, 13, 13, 13, 13, 13, 13, 13, 13, 27, 27, 13, 80,
        // Row 13
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 14
        80, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 80,
        // Row 15 (bottom wall)
        80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80,
    ];

    private static readonly int?[] OverlayTileData = new int?[16 * 16];

    private static readonly int[] WalkableTileIds = [13, 32, 33];

    // Instance MUST be declared AFTER all static data fields to avoid null references
    public static HomeBaseHangar Instance { get; } = new();

    private HomeBaseHangar()
        : base("home_base", "home_base_hangar", "Home Base - Hangar",
               16, 16, 1,
               BaseTileData, OverlayTileData, WalkableTileIds,
               warps: [new WarpConnection(7, 0, "home_base_center", 7, 14, WarpTrigger.Step)],
               connections: null,
               worldX: 0, worldY: 1)
    { }
}
