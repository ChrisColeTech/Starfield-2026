namespace Starfield2026.Core.Maps;

/// <summary>
/// Categories for tile classification in the map system.
/// </summary>
public enum TileCategory
{
    /// <summary>Base terrain tiles like metal floor, grass, sand.</summary>
    Terrain,

    /// <summary>Visual decoration tiles like crystals, pillars, crates.</summary>
    Decoration,

    /// <summary>Tiles that trigger actions like doors, warps, terminals.</summary>
    Interactive,

    /// <summary>NPC entity tiles.</summary>
    Entity,

    /// <summary>Trainer tiles with directional facing.</summary>
    Trainer,

    /// <summary>Wild encounter zones (nebula, asteroid field, etc.).</summary>
    Encounter,

    /// <summary>Structural tiles like walls, cliffs, ramps.</summary>
    Structure,

    /// <summary>Item pickup tiles.</summary>
    Item,

    /// <summary>Transition tiles for map edge crossings.</summary>
    Transition,

    /// <summary>Spawn point tiles for players, enemies, bosses.</summary>
    Spawn
}
