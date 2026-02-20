namespace Starfield2026.Core.Maps;

/// <summary>
/// Defines when a warp is triggered.
/// </summary>
public enum WarpTrigger
{
    /// <summary>Triggers when the player steps onto the tile.</summary>
    Step,

    /// <summary>Triggers when the player faces the tile and presses interact.</summary>
    Interact
}

/// <summary>
/// Defines a connection from a tile position on one map to a position on another map.
/// </summary>
/// <param name="X">Source tile X position on this map.</param>
/// <param name="Y">Source tile Y position on this map.</param>
/// <param name="TargetMapId">The map ID to transition to.</param>
/// <param name="TargetX">Arrival tile X position on the target map.</param>
/// <param name="TargetY">Arrival tile Y position on the target map.</param>
/// <param name="Trigger">How the warp is activated (step or interact).</param>
public record WarpConnection(
    int X, int Y,
    string TargetMapId,
    int TargetX, int TargetY,
    WarpTrigger Trigger = WarpTrigger.Step
);

/// <summary>
/// Cardinal direction for map edge connections.
/// </summary>
public enum MapEdge
{
    North,
    South,
    East,
    West
}

/// <summary>
/// Defines an edge-based connection between two maps.
/// When the player walks off this map's edge, they appear on the target map.
/// </summary>
/// <param name="Edge">Which edge of this map the connection is on.</param>
/// <param name="TargetMapId">The map ID to transition to.</param>
/// <param name="Offset">Position offset along the perpendicular axis.</param>
public record MapConnection(
    MapEdge Edge,
    string TargetMapId,
    int Offset = 0
);
