#nullable enable
namespace Starfield2026.ModelLoader.Maps;

public record TileDefinition(
    int Id,
    string Name,
    bool Walkable,
    string Color,
    TileCategory Category,
    string? OverlayBehavior = null,
    int? EntityId = null,
    string? SpriteName = null,
    int AnimationFrames = 0,
    float Height = 0f,
    string? ModelId = null,
    string? TexturePath = null,
    float BaselineSize = 1f,
    float Scale = 1f,
    bool AlphaCutout = true
);
