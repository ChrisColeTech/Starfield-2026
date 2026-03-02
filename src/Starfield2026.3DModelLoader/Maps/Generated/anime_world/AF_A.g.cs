#nullable enable

// Tile Legend:
//    1 = Grass01 (Terrain)
//    6 = Tree01 (Decoration)
//   45 = AnimeTree01 (Decoration)
//   46 = AnimeTree02 (Decoration)
//   47 = AnimeTree03 (Decoration)
//   48 = AnimeTree04 (Decoration)
namespace Starfield2026.ModelLoader.Maps;

public sealed class AnimeForestA : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null,   45, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null,   46, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,   47, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null,   48, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,    6, null, null, null, 
        null, null, null, null, null,    6, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1
    ];

    public static AnimeForestA Instance { get; } = new();

    private AnimeForestA()
        : base("anime_world", "af_a", "Anime Forest A", 20, 20, 24, BaseTileData, OverlayTileData, WalkableTileIds)
    {
    }
}
