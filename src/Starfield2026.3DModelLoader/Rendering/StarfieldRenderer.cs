#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Rendering;

public class StarfieldRenderer
{
    private BasicEffect _effect = null!;
    private VertexPositionColor[] _stars = null!;
    private int _starCount;
    private float _time;

    public int StarCount { get; set; } = 2000;
    public float Spread { get; set; } = 500f;
    public float TwinkleSpeed { get; set; } = 2f;
    public Color BaseColor { get; set; } = Color.White;

    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        RegenerateStars();
    }

    public void RegenerateStars()
    {
        var rand = new Random(42);
        _stars = new VertexPositionColor[StarCount];

        for (int i = 0; i < StarCount; i++)
        {
            float x = ((float)rand.NextDouble() * 2f - 1f) * Spread;
            float y = ((float)rand.NextDouble() * 2f - 1f) * Spread;
            float z = ((float)rand.NextDouble() * 2f - 1f) * Spread;

            float brightness = 0.3f + (float)rand.NextDouble() * 0.7f;
            var color = new Color(brightness, brightness, brightness * 1.1f);

            _stars[i] = new VertexPositionColor(new Vector3(x, y, z), color);
        }

        _starCount = StarCount;
    }

    public void Update(float dt)
    {
        _time += dt * TwinkleSpeed;
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        if (_stars == null || _starCount == 0) return;

        var oldDepth = device.DepthStencilState;
        device.DepthStencilState = DepthStencilState.None;

        _effect.World = Matrix.CreateTranslation(cameraPosition);
        _effect.View = view;
        _effect.Projection = projection;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.PointList, _stars, 0, _starCount);
        }

        device.DepthStencilState = oldDepth;
    }
}
