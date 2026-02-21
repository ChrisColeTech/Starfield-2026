using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering.Battle;

public class BattleModelData : IDisposable
{
    public List<BattleMeshData> Meshes { get; } = new();
    public int TotalVertices { get; set; }
    public int TotalIndices { get; set; }
    public int TexturedMeshCount { get; set; }
    public Vector3 BoundsMin { get; set; } = new(float.MaxValue);
    public Vector3 BoundsMax { get; set; } = new(float.MinValue);

    public void Draw(GraphicsDevice device, AlphaTestEffect effect)
    {
        foreach (var mesh in Meshes)
        {
            if (mesh.Texture != null)
                effect.Texture = mesh.Texture;

            device.SetVertexBuffer(mesh.VertexBuffer);
            device.Indices = mesh.IndexBuffer;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.SamplerStates[0] = SamplerState.PointClamp;
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
            }
        }
    }

    public void Dispose()
    {
        foreach (var mesh in Meshes)
            mesh.Dispose();
    }
}
