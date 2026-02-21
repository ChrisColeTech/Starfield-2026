using System;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering.Battle;

public class BattleMeshData
{
    public VertexBuffer VertexBuffer { get; set; } = null!;
    public IndexBuffer IndexBuffer { get; set; } = null!;
    public Texture2D? Texture { get; set; }
    public int PrimitiveCount { get; set; }

    public void Dispose()
    {
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
        Texture?.Dispose();
    }


}
