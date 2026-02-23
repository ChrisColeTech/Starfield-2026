#nullable enable
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Rendering;

internal sealed class MeshBatch
{
    public required int StartIndex { get; init; }
    public required int PrimitiveCount { get; init; }
    public Texture2D? Texture { get; init; }
    public bool IsFace { get; init; }
}
