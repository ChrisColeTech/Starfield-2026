// Auto-generated test map for Map3D screen integration testing.
// Uses flat array format parseable by the standalone MapParser.
// Tile IDs reference TileRegistry.cs ModelId values.

// Constructor: base("overworld", "mountain_trail", "Mountain Trail", 16, 16, 2, ...)

// BaseTileData:
// Tile  1 = Grass (walkable, no model)
// Tile 16 = Tree  (ModelId: Tree01)
// Tile 17 = Rock  (ModelId: Rock01)
// Tile 18 = Crystal (ModelId: Flower01)
// Tile 20 = Bush  (ModelId: Bush01)
// Tile 22 = Boulder (ModelId: Rock02)
// Tile 80 = Wall  (ModelId: Mountain01)
// Tile 93 = Cliff (ModelId: Mountain01)
// Tile 116 = PlayerSpawn

// Width=16, Height=16, TileSize=2

private static readonly int[] BaseTileData = [
    80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80,
    80,  1,  1,  1,  1, 16,  1,  1,  1,  1,  1,  1, 16,  1,  1, 80,
    80,  1, 17,  1,  1,  1,  1, 20,  1,  1,  1,  1,  1,  1,  1, 80,
    80,  1,  1,  1, 16,  1,  1,  1,  1, 22,  1,  1,  1, 17,  1, 80,
    80,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, 16,  1,  1,  1, 80,
    80, 16,  1,  1,  1,  1, 18,  1,  1,  1,  1,  1,  1,  1, 20, 80,
    80,  1,  1,  1,  1,  1,  1,  1,  1,  1, 17,  1,  1,  1,  1, 80,
    80,  1, 20,  1,  1,  1,  1,116,  1,  1,  1,  1, 22,  1,  1, 80,
    80,  1,  1,  1, 17,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, 80,
    80,  1,  1,  1,  1,  1,  1,  1,  1, 16,  1,  1,  1,  1, 17, 80,
    80,  1, 16,  1,  1,  1,  1,  1,  1,  1,  1,  1, 20,  1,  1, 80,
    80,  1,  1,  1,  1, 22,  1,  1, 18,  1,  1,  1,  1,  1,  1, 80,
    80,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, 16,  1,  1,  1, 80,
    80, 17,  1,  1,  1,  1, 20,  1,  1,  1,  1,  1,  1, 17,  1, 80,
    80,  1,  1, 16,  1,  1,  1,  1,  1, 22,  1,  1,  1,  1,  1, 80,
    80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80
];

private static readonly int?[] OverlayTileData = new int?[16 * 16];
