using Starfield2026.Core.Encounters;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Maps.Generated;

/// <summary>
/// Large open world map for the FreeRoamScreen.
/// The grid is split into 4 color-coded quadrants:
///   Top-left (NW)  = Grass       (#7ec850, green)
///   Top-right (NE) = Ice         (#b0e0f8, blue)
///   Bottom-left (SW) = Sand      (#e8d8a0, yellow)
///   Bottom-right (SE) = MetalFloor (#808090, grey)
/// Player spawns at dead center.
/// </summary>
public sealed class FreeRoamArena : MapDefinition
{
    // Tile IDs used:
    //   1 = Grass    (green)
    //   6 = Sand     (yellow)
    //   8 = Ice      (blue)
    //   3 = MetalFloor (grey)
    // 116 = PlayerSpawn

    public override bool UseWireframeGrid => true;

    private static readonly int[] BaseTileData = BuildGrid();

    private static int[] BuildGrid()
    {
        const int W = 500, H = 500;
        int[] data = new int[W * H];

        int halfW = W / 2; // 250
        int halfH = H / 2; // 250

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (y < halfH && x < halfW)
                    data[y * W + x] = 1;  // NW = Grass (green)
                else if (y < halfH && x >= halfW)
                    data[y * W + x] = 8;  // NE = Ice (blue)
                else if (y >= halfH && x < halfW)
                    data[y * W + x] = 6;  // SW = Sand (yellow)
                else
                    data[y * W + x] = 3;  // SE = MetalFloor (grey)
            }
        }

        // Player spawn at center
        data[250 * W + 250] = 116;

        return data;
    }

    private static readonly int?[] OverlayTileData = new int?[500 * 500];

    private static readonly int[] WalkableTileIds = [1, 3, 6, 8, 116];

    private static readonly EncounterTable[] _encounterGroupsData = [];

    // Instance MUST be declared AFTER all static data fields
    public static FreeRoamArena Instance { get; } = new();

    private FreeRoamArena()
        : base("freeroam", "free_roam_arena", "Free Roam Arena",
               500, 500, 1,
               BaseTileData, OverlayTileData, WalkableTileIds,
               warps: null,
               connections: null,
               worldX: 0, worldY: 0,
               encounterGroups: _encounterGroupsData,
               progressMultiplier: 0f)
    { }
}
