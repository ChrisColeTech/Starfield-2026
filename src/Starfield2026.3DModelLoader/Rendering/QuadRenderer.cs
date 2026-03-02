#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Rendering;

public class QuadRenderer
{
    private BasicEffect _effect = null!;
    private VertexBuffer _vertexBuffer = null!;
    private IndexBuffer _indexBuffer = null!;

    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = false,
            LightingEnabled = false,
        };

        float s = 0.5f; // 1 unit
        var verts = new[]
        {
            new VertexPositionNormalTexture(new Vector3(-s, 0, -s), Vector3.Up, new Vector2(0, 0)),
            new VertexPositionNormalTexture(new Vector3(s, 0, -s), Vector3.Up, new Vector2(1, 0)),
            new VertexPositionNormalTexture(new Vector3(s, 0, s), Vector3.Up, new Vector2(1, 1)),
            new VertexPositionNormalTexture(new Vector3(-s, 0, s), Vector3.Up, new Vector2(0, 1))
        };

        _vertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, 4, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(verts);

        var indices = new short[] { 0, 1, 2, 0, 2, 3 };
        _indexBuffer = new IndexBuffer(device, typeof(short), 6, BufferUsage.WriteOnly);
        _indexBuffer.SetData(indices);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection, Texture2D texture,
        Vector3 position, float rotationY = 0f, float scale = 1f)
    {
        var world = Matrix.CreateScale(scale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position);

        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;
        _effect.Texture = texture;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    }
}
