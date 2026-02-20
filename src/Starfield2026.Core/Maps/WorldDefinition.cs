namespace Starfield2026.Core.Maps;

/// <summary>
/// Defines a world (location/level) with its default spawn point.
/// </summary>
/// <param name="Id">Unique world identifier, matches the folder name.</param>
/// <param name="Name">Display name shown in UI.</param>
/// <param name="SpawnMapId">Default map when entering this world.</param>
/// <param name="SpawnX">Default spawn tile X on the spawn map.</param>
/// <param name="SpawnY">Default spawn tile Y on the spawn map.</param>
public record WorldDefinition(
    string Id,
    string Name,
    string SpawnMapId,
    int SpawnX,
    int SpawnY
);
