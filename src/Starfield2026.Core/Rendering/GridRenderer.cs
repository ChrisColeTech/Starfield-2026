using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering;

/// <summary>
/// Which plane the grid is drawn on.
/// </summary>
public enum GridOrientation
{
    /// <summary>XZ plane (horizontal floor/ceiling)</summary>
    Horizontal,
    /// <summary>YZ plane (vertical wall, offset along X)</summary>
    WallX,
    /// <summary>XY plane (vertical wall, offset along Z)</summary>
    WallZ,
}

/// <summary>
/// Renders a scrolling grid of lines, used for both the space starfield grid
/// and the ground plane in the overworld. Supports horizontal and vertical orientations.
/// </summary>
public class GridRenderer
{
    private BasicEffect _effect = null!;
    private VertexPositionColor[] _vertices = null!;
    private int _lineCount;

    /// <summary>Grid spacing between lines.</summary>
    public float Spacing { get; set; } = 2f;

    /// <summary>Number of grid lines in each direction from center.</summary>
    public int GridHalfSize { get; set; } = 40;

    /// <summary>Scrolling offset applied to the grid (for movement illusion).</summary>
    public Vector3 ScrollOffset { get; set; }

    /// <summary>Offset position on the fixed axis (Y for horizontal, X for WallX, Z for WallZ).</summary>
    public float PlaneOffset { get; set; } = 0f;

    /// <summary>Grid line color.</summary>
    public Color GridColor { get; set; } = new Color(0, 180, 255, 120);

    /// <summary>Orientation of the grid plane.</summary>
    public GridOrientation Orientation { get; set; } = GridOrientation.Horizontal;

    /// <summary>Initialize GPU resources.</summary>
    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        RebuildGrid();
    }

    /// <summary>Rebuild the grid vertex array based on current settings.</summary>
    public void RebuildGrid()
    {
        int total = GridHalfSize * 2 + 1;
        _lineCount = total * 2;
        _vertices = new VertexPositionColor[_lineCount * 2];

        float extent = GridHalfSize * Spacing;
        int idx = 0;

        switch (Orientation)
        {
            case GridOrientation.Horizontal:
                // XZ plane at Y = PlaneOffset
                for (int i = -GridHalfSize; i <= GridHalfSize; i++)
                {
                    float x = i * Spacing;
                    _vertices[idx++] = new VertexPositionColor(new Vector3(x, PlaneOffset, -extent), GridColor);
                    _vertices[idx++] = new VertexPositionColor(new Vector3(x, PlaneOffset, extent), GridColor);
                }
                for (int i = -GridHalfSize; i <= GridHalfSize; i++)
                {
                    float z = i * Spacing;
                    _vertices[idx++] = new VertexPositionColor(new Vector3(-extent, PlaneOffset, z), GridColor);
                    _vertices[idx++] = new VertexPositionColor(new Vector3(extent, PlaneOffset, z), GridColor);
                }
                break;

            case GridOrientation.WallX:
                // YZ plane at X = PlaneOffset (side wall)
                for (int i = -GridHalfSize; i <= GridHalfSize; i++)
                {
                    float y = i * Spacing;
                    _vertices[idx++] = new VertexPositionColor(new Vector3(PlaneOffset, y, -extent), GridColor);
                    _vertices[idx++] = new VertexPositionColor(new Vector3(PlaneOffset, y, extent), GridColor);
                }
                for (int i = -GridHalfSize; i <= GridHalfSize; i++)
                {
                    float z = i * Spacing;
                    _vertices[idx++] = new VertexPositionColor(new Vector3(PlaneOffset, -extent, z), GridColor);
                    _vertices[idx++] = new VertexPositionColor(new Vector3(PlaneOffset, extent, z), GridColor);
                }
                break;

            case GridOrientation.WallZ:
                // XY plane at Z = PlaneOffset (front/back wall)
                for (int i = -GridHalfSize; i <= GridHalfSize; i++)
                {
                    float x = i * Spacing;
                    _vertices[idx++] = new VertexPositionColor(new Vector3(x, -extent, PlaneOffset), GridColor);
                    _vertices[idx++] = new VertexPositionColor(new Vector3(x, extent, PlaneOffset), GridColor);
                }
                for (int i = -GridHalfSize; i <= GridHalfSize; i++)
                {
                    float y = i * Spacing;
                    _vertices[idx++] = new VertexPositionColor(new Vector3(-extent, y, PlaneOffset), GridColor);
                    _vertices[idx++] = new VertexPositionColor(new Vector3(extent, y, PlaneOffset), GridColor);
                }
                break;
        }
    }

    /// <summary>Draw the grid with scroll offset applied.</summary>
    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        if (_vertices == null || _vertices.Length == 0) return;

        // ScrollOffset is a direct world translation.
        // The caller snaps to grid-cell boundaries for seamless tiling.
        var translation = ScrollOffset;

        _effect.World = Matrix.CreateTranslation(translation);
        _effect.View = view;
        _effect.Projection = projection;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.LineList, _vertices, 0, _lineCount);
        }
    }
}
