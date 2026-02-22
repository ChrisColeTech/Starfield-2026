#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Rendering;

public class QuadrantGridRenderer
{
    private BasicEffect _effect = null!;
    private VertexPositionColor[] _vertices = null!;
    private int _lineCount;

    public float Spacing { get; set; } = 2f;
    public int GridHalfSize { get; set; } = 60;
    public float PlaneOffset { get; set; } = 0f;
    public Vector3 ScrollOffset { get; set; }

    public Color NorthWestColor { get; set; } = new Color(40, 200, 80, 150);
    public Color NorthEastColor { get; set; } = new Color(60, 140, 220, 150);
    public Color SouthWestColor { get; set; } = new Color(220, 180, 40, 150);
    public Color SouthEastColor { get; set; } = new Color(160, 60, 200, 150);

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

        float ox = ScrollOffset.X;
        float oz = ScrollOffset.Z;
        float extent = GridHalfSize * Spacing;
        int idx = 0;

        for (int i = -GridHalfSize; i <= GridHalfSize; i++)
        {
            float localX = i * Spacing;
            float worldX = localX + ox;
            float localZMin = -extent;
            float localZMax = extent;
            float splitLocalZ = -oz;

            if (splitLocalZ > localZMin && splitLocalZ < localZMax)
            {
                Color cNeg = GetColor(worldX, -1);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, localZMin), cNeg);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, splitLocalZ), cNeg);

                Color cPos = GetColor(worldX, 1);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, splitLocalZ), cPos);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, localZMax), cPos);
            }
            else
            {
                float worldZMin = localZMin + oz;
                Color c = GetColor(worldX, worldZMin);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, localZMin), c);
                _vertices[idx++] = new VertexPositionColor(new Vector3(localX, PlaneOffset, localZMax), c);
            }
        }

        for (int i = -GridHalfSize; i <= GridHalfSize; i++)
        {
            float localZ = i * Spacing;
            float worldZ = localZ + oz;
            float localXMin = -extent;
            float localXMax = extent;
            float splitLocalX = -ox;

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
                float worldXMin = localXMin + ox;
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
