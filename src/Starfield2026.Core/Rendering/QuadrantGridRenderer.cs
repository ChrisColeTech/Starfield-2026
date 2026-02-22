#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering;

/// <summary>
/// Renders a scrolling wireframe grid split into 4 color-coded quadrants
/// anchored to world origin. Quadrant colors stay fixed in world space
/// regardless of scroll position.
/// NW = green, NE = blue, SW = yellow/gold, SE = purple.
/// </summary>
public class QuadrantGridRenderer
{
    private BasicEffect _effect = null!;
    private VertexPositionColor[] _vertices = null!;
    private int _lineCount;

    public float Spacing { get; set; } = 2f;
    public int GridHalfSize { get; set; } = 60;
    public float PlaneOffset { get; set; } = 0f;
    public Vector3 ScrollOffset { get; set; }

    public Color NorthWestColor { get; set; } = new Color(40, 200, 80, 150);   // green
    public Color NorthEastColor { get; set; } = new Color(60, 140, 220, 150);  // blue
    public Color SouthWestColor { get; set; } = new Color(220, 180, 40, 150);  // gold
    public Color SouthEastColor { get; set; } = new Color(160, 60, 200, 150);  // purple

    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        int total = GridHalfSize * 2 + 1;
        int maxLines = total * 4;
        _vertices = new VertexPositionColor[maxLines * 2];
    }

    private Color GetColor(float worldX, float worldZ)
    {
        if (worldX < 0)
            return worldZ < 0 ? NorthWestColor : SouthWestColor;
        else
            return worldZ < 0 ? NorthEastColor : SouthEastColor;
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        if (_vertices == null || _vertices.Length == 0) return;

        // Rebuild vertex colors every frame based on world-space positions.
        // The grid is drawn at ScrollOffset, so a local coordinate x maps
        // to world coordinate x + ScrollOffset.X.
        float ox = ScrollOffset.X;
        float oz = ScrollOffset.Z;
        float extent = GridHalfSize * Spacing;
        int idx = 0;

        // Lines parallel to Z axis — split each at world Z=0
        for (int i = -GridHalfSize; i <= GridHalfSize; i++)
        {
            float localX = i * Spacing;
            float worldX = localX + ox;

            float localZMin = -extent;
            float localZMax = extent;
            float worldZMin = localZMin + oz;
            float worldZMax = localZMax + oz;

            // Does world Z=0 cross this line segment?
            float splitLocalZ = -oz; // local Z where world Z = 0

            if (splitLocalZ > localZMin && splitLocalZ < localZMax)
            {
                // Negative-Z segment (world Z < 0)
                Color cNeg = GetColor(worldX, -1);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, localZMin), cNeg);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, splitLocalZ), cNeg);

                // Positive-Z segment (world Z > 0)
                Color cPos = GetColor(worldX, 1);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, splitLocalZ), cPos);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, localZMax), cPos);
            }
            else
            {
                // Entire line is on one side of world Z=0
                Color c = GetColor(worldX, worldZMin);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, localZMin), c);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, localZMax), c);
            }
        }

        // Lines parallel to X axis — split each at world X=0
        for (int i = -GridHalfSize; i <= GridHalfSize; i++)
        {
            float localZ = i * Spacing;
            float worldZ = localZ + oz;

            float localXMin = -extent;
            float localXMax = extent;
            float worldXMin = localXMin + ox;
            float worldXMax = localXMax + ox;

            float splitLocalX = -ox; // local X where world X = 0

            if (splitLocalX > localXMin && splitLocalX < localXMax)
            {
                Color cNeg = GetColor(-1, worldZ);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localXMin, PlaneOffset, localZ), cNeg);
                _vertices[idx++] = new VertexPositionColor(new Vector3(splitLocalX, PlaneOffset, localZ), cNeg);

                Color cPos = GetColor(1, worldZ);
                _vertices[idx++] = new VertexPositionColor(new Vector3(splitLocalX, PlaneOffset, localZ), cPos);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localXMax, PlaneOffset, localZ), cPos);
            }
            else
            {
                Color c = GetColor(worldXMin, worldZ);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localXMin, PlaneOffset, localZ), c);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localXMax, PlaneOffset, localZ), c);
            }
        }

        _lineCount = idx / 2;

        _effect.World = Matrix.CreateTranslation(ScrollOffset);
        _effect.View = view;
        _effect.Projection = projection;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.LineList, _vertices, 0, _lineCount);
        }
    }
}
