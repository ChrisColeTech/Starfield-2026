using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering;

public class SpaceBackground
{
    private BasicEffect _effect = null!;
    private VertexPositionColor[] _verts = null!;
    private readonly int _count;
    private readonly Random _random = new();
    
    public float SpreadRadius { get; set; } = 60f;
    public float DepthRange { get; set; } = 120f;

    public SpaceBackground(int starCount = 600)
    {
        _count = starCount;
    }

    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        _verts = new VertexPositionColor[_count];
        for (int i = 0; i < _count; i++)
        {
            _verts[i] = CreateStar();
        }
    }

    private VertexPositionColor CreateStar(float zOffset = 0f)
    {
        float x = (float)(_random.NextDouble() * 2 - 1) * SpreadRadius;
        float y = (float)(_random.NextDouble() * 2 - 1) * SpreadRadius;
        float z = zOffset != 0f ? zOffset : (float)(_random.NextDouble() * 2 - 1) * DepthRange;
        
        float brightness = 0.4f + (float)_random.NextDouble() * 0.6f;
        var color = new Color((byte)(200 * brightness), (byte)(220 * brightness), 255);
        
        return new VertexPositionColor(new Vector3(x, y, z), color);
    }

    public void Update(float dt, float speed, Vector3 center)
    {
        for (int i = 0; i < _count; i++)
        {
            var pos = _verts[i].Position;
            pos.Z += speed * dt * 0.5f;
            
            if (pos.Z > DepthRange)
            {
                pos.Z = -DepthRange;
                pos.X = (float)(_random.NextDouble() * 2 - 1) * SpreadRadius;
                pos.Y = (float)(_random.NextDouble() * 2 - 1) * SpreadRadius;
            }
            
            _verts[i].Position = pos;
        }
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix proj, Vector3 center)
    {
        _effect.World = Matrix.CreateTranslation(center);
        _effect.View = view;
        _effect.Projection = proj;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.PointList, _verts, 0, _count);
        }
    }
}
