using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering;

public class CoinRenderer
{
    private BasicEffect _effect = null!;
    private VertexPositionColor[] _vertices = null!;
    private short[] _indices = null!;
    private int _primitiveCount;

    private const int Segments = 16;
    private const float Radius = 0.5f;
    private const float Thickness = 0.12f;

    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        BuildCoinGeometry();
    }

    private void BuildCoinGeometry()
    {
        int topCenterIdx = 0;
        int topRingStart = 1;
        int bottomCenterIdx = 1 + Segments;
        int bottomRingStart = 2 + Segments;
        int edgeStart = 2 + 2 * Segments;
        int vertCount = 2 + 2 * Segments + 2 * Segments;

        _vertices = new VertexPositionColor[vertCount];
        float halfH = Thickness / 2f;

        // Top center and ring
        _vertices[topCenterIdx] = new VertexPositionColor(new Vector3(0, halfH, 0), Color.White);
        for (int i = 0; i < Segments; i++)
        {
            float angle = MathHelper.TwoPi * i / Segments;
            float x = (float)Math.Cos(angle) * Radius;
            float z = (float)Math.Sin(angle) * Radius;
            _vertices[topRingStart + i] = new VertexPositionColor(new Vector3(x, halfH, z), Color.White);
        }

        // Bottom center and ring
        _vertices[bottomCenterIdx] = new VertexPositionColor(new Vector3(0, -halfH, 0), Color.White);
        for (int i = 0; i < Segments; i++)
        {
            float angle = MathHelper.TwoPi * i / Segments;
            float x = (float)Math.Cos(angle) * Radius;
            float z = (float)Math.Sin(angle) * Radius;
            _vertices[bottomRingStart + i] = new VertexPositionColor(new Vector3(x, -halfH, z), Color.White);
        }

        // Edge strip
        for (int i = 0; i < Segments; i++)
        {
            float angle = MathHelper.TwoPi * i / Segments;
            float x = (float)Math.Cos(angle) * Radius;
            float z = (float)Math.Sin(angle) * Radius;
            _vertices[edgeStart + i * 2] = new VertexPositionColor(new Vector3(x, halfH, z), Color.White);
            _vertices[edgeStart + i * 2 + 1] = new VertexPositionColor(new Vector3(x, -halfH, z), Color.White);
        }

        // Build indices
        _primitiveCount = Segments * 4; // top + bottom + 2*edge
        _indices = new short[_primitiveCount * 3];
        int idx = 0;

        // Top fan
        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            _indices[idx++] = (short)topCenterIdx;
            _indices[idx++] = (short)(topRingStart + i);
            _indices[idx++] = (short)(topRingStart + next);
        }

        // Bottom fan
        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            _indices[idx++] = (short)bottomCenterIdx;
            _indices[idx++] = (short)(bottomRingStart + next);
            _indices[idx++] = (short)(bottomRingStart + i);
        }

        // Edge strip
        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            int e0 = edgeStart + i * 2;
            int e1 = edgeStart + i * 2 + 1;
            int e2 = edgeStart + next * 2;
            int e3 = edgeStart + next * 2 + 1;

            _indices[idx++] = (short)e0;
            _indices[idx++] = (short)e1;
            _indices[idx++] = (short)e2;
            _indices[idx++] = (short)e2;
            _indices[idx++] = (short)e1;
            _indices[idx++] = (short)e3;
        }
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY, float scale, Color color)
    {
        var topColor = color;
        var bottomColor = new Color(color.R * 7 / 10, color.G * 7 / 10, color.B * 7 / 10);
        var edgeColor = new Color(color.R * 8 / 10, color.G * 8 / 10, color.B * 8 / 10);

        // Update vertex colors
        int topCenterIdx = 0;
        int topRingStart = 1;
        int bottomCenterIdx = 1 + Segments;
        int bottomRingStart = 2 + Segments;
        int edgeStart = 2 + 2 * Segments;

        _vertices[topCenterIdx].Color = topColor;
        for (int i = 0; i < Segments; i++)
            _vertices[topRingStart + i].Color = topColor;

        _vertices[bottomCenterIdx].Color = bottomColor;
        for (int i = 0; i < Segments; i++)
            _vertices[bottomRingStart + i].Color = bottomColor;

        for (int i = 0; i < Segments; i++)
        {
            _vertices[edgeStart + i * 2].Color = edgeColor;
            _vertices[edgeStart + i * 2 + 1].Color = edgeColor;
        }

        var world = Matrix.CreateScale(scale)
            * Matrix.CreateRotationX(MathHelper.PiOver2)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position);

        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertices.Length, _indices, 0, _primitiveCount);
        }
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY = 0f, float scale = 1f)
    {
        Draw(device, view, projection, position, rotationY, scale, Color.Gold);
    }
}
