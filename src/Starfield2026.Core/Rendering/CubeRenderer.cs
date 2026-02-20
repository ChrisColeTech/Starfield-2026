using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering;

/// <summary>
/// Renders a 3D cube with per-face coloring to represent the player.
/// </summary>
public class CubeRenderer
{
    private BasicEffect _effect = null!;
    private VertexBuffer _vertexBuffer = null!;
    private IndexBuffer _indexBuffer = null!;

    /// <summary>
    /// Initialize the cube mesh on the GPU.
    /// </summary>
    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        BuildCube(device);
    }

    private void BuildCube(GraphicsDevice device)
    {
        // 6 faces × 4 vertices = 24 vertices (each face has unique colors/normals)
        var verts = new VertexPositionColor[24];

        // Half-size
        float s = 0.5f;

        // Face colors - vibrant neon-ish palette
        var front = new Color(0, 220, 255);    // cyan
        var back = new Color(255, 50, 150);     // pink
        var top = new Color(100, 255, 100);     // green  
        var bottom = new Color(255, 160, 0);    // orange
        var left = new Color(150, 100, 255);    // purple
        var right = new Color(255, 255, 80);    // yellow

        // Front face (Z+)
        verts[0] = new VertexPositionColor(new Vector3(-s, -s, s), front);
        verts[1] = new VertexPositionColor(new Vector3(s, -s, s), front);
        verts[2] = new VertexPositionColor(new Vector3(s, s, s), front);
        verts[3] = new VertexPositionColor(new Vector3(-s, s, s), front);

        // Back face (Z-)
        verts[4] = new VertexPositionColor(new Vector3(s, -s, -s), back);
        verts[5] = new VertexPositionColor(new Vector3(-s, -s, -s), back);
        verts[6] = new VertexPositionColor(new Vector3(-s, s, -s), back);
        verts[7] = new VertexPositionColor(new Vector3(s, s, -s), back);

        // Top face (Y+)
        verts[8] = new VertexPositionColor(new Vector3(-s, s, s), top);
        verts[9] = new VertexPositionColor(new Vector3(s, s, s), top);
        verts[10] = new VertexPositionColor(new Vector3(s, s, -s), top);
        verts[11] = new VertexPositionColor(new Vector3(-s, s, -s), top);

        // Bottom face (Y-)
        verts[12] = new VertexPositionColor(new Vector3(-s, -s, -s), bottom);
        verts[13] = new VertexPositionColor(new Vector3(s, -s, -s), bottom);
        verts[14] = new VertexPositionColor(new Vector3(s, -s, s), bottom);
        verts[15] = new VertexPositionColor(new Vector3(-s, -s, s), bottom);

        // Left face (X-)
        verts[16] = new VertexPositionColor(new Vector3(-s, -s, -s), left);
        verts[17] = new VertexPositionColor(new Vector3(-s, -s, s), left);
        verts[18] = new VertexPositionColor(new Vector3(-s, s, s), left);
        verts[19] = new VertexPositionColor(new Vector3(-s, s, -s), left);

        // Right face (X+)
        verts[20] = new VertexPositionColor(new Vector3(s, -s, s), right);
        verts[21] = new VertexPositionColor(new Vector3(s, -s, -s), right);
        verts[22] = new VertexPositionColor(new Vector3(s, s, -s), right);
        verts[23] = new VertexPositionColor(new Vector3(s, s, s), right);

        _vertexBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, 24, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(verts);

        // 6 faces × 2 triangles × 3 indices = 36
        var indices = new short[]
        {
            0,1,2, 0,2,3,       // front
            4,5,6, 4,6,7,       // back
            8,9,10, 8,10,11,    // top
            12,13,14, 12,14,15, // bottom
            16,17,18, 16,18,19, // left
            20,21,22, 20,22,23, // right
        };

        _indexBuffer = new IndexBuffer(device, typeof(short), 36, BufferUsage.WriteOnly);
        _indexBuffer.SetData(indices);
    }

    /// <summary>
    /// Draw the cube at a given position with rotation and scale.
    /// </summary>
    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY = 0f, float scale = 1f)
    {
        var world = Matrix.CreateScale(scale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position);

        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);
        }
    }

    private VertexBuffer? _solidVb;
    private IndexBuffer? _solidIb;
    private BasicEffect? _solidEffect;
    private Color _lastSolidColor;

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY, float scale, Color color)
    {
        if (_solidEffect == null)
        {
            _solidEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
            };
        }

        // Rebuild vertex buffer when color changes
        if (_solidVb == null || color != _lastSolidColor)
        {
            BuildSolidCube(device, color);
            _lastSolidColor = color;
        }

        var world = Matrix.CreateScale(scale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position);

        _solidEffect.World = world;
        _solidEffect.View = view;
        _solidEffect.Projection = projection;

        device.SetVertexBuffer(_solidVb);
        device.Indices = _solidIb;

        foreach (var pass in _solidEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);
        }
    }

    private void BuildSolidCube(GraphicsDevice device, Color solidColor)
    {
        float s = 0.5f;
        var verts = new VertexPositionColor[24];

        verts[0] = new VertexPositionColor(new Vector3(-s, -s, s), solidColor);
        verts[1] = new VertexPositionColor(new Vector3(s, -s, s), solidColor);
        verts[2] = new VertexPositionColor(new Vector3(s, s, s), solidColor);
        verts[3] = new VertexPositionColor(new Vector3(-s, s, s), solidColor);

        verts[4] = new VertexPositionColor(new Vector3(s, -s, -s), solidColor);
        verts[5] = new VertexPositionColor(new Vector3(-s, -s, -s), solidColor);
        verts[6] = new VertexPositionColor(new Vector3(-s, s, -s), solidColor);
        verts[7] = new VertexPositionColor(new Vector3(s, s, -s), solidColor);

        verts[8] = new VertexPositionColor(new Vector3(-s, s, s), solidColor);
        verts[9] = new VertexPositionColor(new Vector3(s, s, s), solidColor);
        verts[10] = new VertexPositionColor(new Vector3(s, s, -s), solidColor);
        verts[11] = new VertexPositionColor(new Vector3(-s, s, -s), solidColor);

        verts[12] = new VertexPositionColor(new Vector3(-s, -s, -s), solidColor);
        verts[13] = new VertexPositionColor(new Vector3(s, -s, -s), solidColor);
        verts[14] = new VertexPositionColor(new Vector3(s, -s, s), solidColor);
        verts[15] = new VertexPositionColor(new Vector3(-s, -s, s), solidColor);

        verts[16] = new VertexPositionColor(new Vector3(-s, -s, -s), solidColor);
        verts[17] = new VertexPositionColor(new Vector3(-s, -s, s), solidColor);
        verts[18] = new VertexPositionColor(new Vector3(-s, s, s), solidColor);
        verts[19] = new VertexPositionColor(new Vector3(-s, s, -s), solidColor);

        verts[20] = new VertexPositionColor(new Vector3(s, -s, s), solidColor);
        verts[21] = new VertexPositionColor(new Vector3(s, -s, -s), solidColor);
        verts[22] = new VertexPositionColor(new Vector3(s, s, -s), solidColor);
        verts[23] = new VertexPositionColor(new Vector3(s, s, s), solidColor);

        _solidVb = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, 24, BufferUsage.WriteOnly);
        _solidVb.SetData(verts);

        var indices = new short[]
        {
            0,1,2, 0,2,3,
            4,5,6, 4,6,7,
            8,9,10, 8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23,
        };

        _solidIb = new IndexBuffer(device, typeof(short), 36, BufferUsage.WriteOnly);
        _solidIb.SetData(indices);
    }
}
