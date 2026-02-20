using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering;

public abstract class StarfieldBase
{
    protected BasicEffect Effect = null!;
    protected VertexPositionColorTexture[] Stars = null!;
    protected VertexPositionColorTexture[] QuadVerts = null!;
    protected int[] QuadIndices = null!;
    protected VertexPositionColorTexture[] StreakVerts = null!;
    protected Texture2D GlowTexture = null!;
    protected readonly int Count;
    protected readonly Random Rng = new();
    protected float Elapsed;
    
    public float SpreadRadius { get; set; } = 60f;
    public float DepthRange { get; set; } = 120f;
    public float StarSize { get; set; } = 0.3f;
    
    private static readonly DepthStencilState NoDepth = new()
    {
        DepthBufferEnable = false,
        DepthBufferWriteEnable = false,
    };
    
    protected StarfieldBase(int starCount)
    {
        Count = starCount;
    }
    
    public virtual void Initialize(GraphicsDevice device)
    {
        GlowTexture = CreateGlowTexture(device, 32);
        
        Effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            TextureEnabled = true,
            LightingEnabled = false,
            Texture = GlowTexture,
        };
        
        Stars = new VertexPositionColorTexture[Count];
        QuadVerts = new VertexPositionColorTexture[Count * 4];
        QuadIndices = new int[Count * 6];
        StreakVerts = new VertexPositionColorTexture[Count * 2];
        
        for (int i = 0; i < Count; i++)
        {
            Stars[i] = CreateStar();
            
            int vi = i * 4;
            int ii = i * 6;
            QuadIndices[ii + 0] = vi + 0;
            QuadIndices[ii + 1] = vi + 1;
            QuadIndices[ii + 2] = vi + 2;
            QuadIndices[ii + 3] = vi + 0;
            QuadIndices[ii + 4] = vi + 2;
            QuadIndices[ii + 5] = vi + 3;
        }
    }
    
    private static Texture2D CreateGlowTexture(GraphicsDevice device, int size)
    {
        var texture = new Texture2D(device, size, size);
        var pixels = new Color[size * size];
        float center = size / 2f;
        float maxDist = center;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy) / maxDist;
                
                // Gaussian-ish falloff: bright center, soft edges
                float alpha = Math.Max(0f, 1f - dist * dist);
                alpha *= alpha; // Sharper falloff
                
                byte a = (byte)(alpha * 255);
                pixels[y * size + x] = new Color(a, a, a, a);
            }
        }
        
        texture.SetData(pixels);
        return texture;
    }
    
    protected VertexPositionColorTexture CreateStar()
    {
        float x = (float)(Rng.NextDouble() * 2 - 1) * SpreadRadius;
        float y = (float)(Rng.NextDouble() * 2 - 1) * SpreadRadius;
        float z = (float)(Rng.NextDouble() * 2 - 1) * DepthRange;
        
        float brightness = 0.4f + (float)Rng.NextDouble() * 0.6f;
        Color color = PickStarColor(brightness);
        
        return new VertexPositionColorTexture(new Vector3(x, y, z), color, Vector2.Zero);
    }
    
    protected virtual Color PickStarColor(float brightness)
    {
        double roll = Rng.NextDouble();
        if (roll < 0.05)
            return new Color((byte)(255 * brightness), (byte)(220 * brightness), (byte)(150 * brightness));
        if (roll < 0.10)
            return new Color((byte)(255 * brightness), (byte)(200 * brightness), (byte)(180 * brightness));
        return new Color((byte)(200 * brightness), (byte)(215 * brightness), (byte)(255 * brightness));
    }
    
    protected void RecycleStarXY(ref Vector3 pos)
    {
        pos.X = (float)(Rng.NextDouble() * 2 - 1) * SpreadRadius;
        pos.Y = (float)(Rng.NextDouble() * 2 - 1) * SpreadRadius;
    }
    
    public abstract void Update(float dt, float speed, Vector3 center);
    
    public virtual void Draw(GraphicsDevice device, Matrix view, Matrix proj, Vector3 center, float speed = 0f)
    {
        BeginStarDraw(device);
        
        Effect.World = Matrix.CreateTranslation(center);
        Effect.View = view;
        Effect.Projection = proj;
        
        float absSpeed = Math.Abs(speed);
        
        foreach (var pass in Effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            
            if (absSpeed > 5f)
            {
                float streakLength = Math.Min(absSpeed * 0.03f, 2.5f);
                BuildStreaks(streakLength, speed);
                Effect.TextureEnabled = false;
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList, StreakVerts, 0, Count);
                Effect.TextureEnabled = true;
            }
            else
            {
                BuildQuads(view);
                device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    QuadVerts, 0, Count * 4,
                    QuadIndices, 0, Count * 2);
            }
        }
        
        EndStarDraw(device);
    }
    
    protected void DrawDotsOnly(GraphicsDevice device, Matrix view, Matrix proj, Vector3 center)
    {
        BeginStarDraw(device);
        
        Effect.World = Matrix.CreateTranslation(center);
        Effect.View = view;
        Effect.Projection = proj;
        
        foreach (var pass in Effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            BuildQuads(view);
            device.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                QuadVerts, 0, Count * 4,
                QuadIndices, 0, Count * 2);
        }
        
        EndStarDraw(device);
    }
    
    private DepthStencilState _prevDepth = null!;
    private BlendState _prevBlend = null!;
    private RasterizerState _prevRaster = null!;
    
    private void BeginStarDraw(GraphicsDevice device)
    {
        _prevDepth = device.DepthStencilState;
        _prevBlend = device.BlendState;
        _prevRaster = device.RasterizerState;
        device.DepthStencilState = NoDepth;
        device.BlendState = BlendState.Additive;
        device.RasterizerState = RasterizerState.CullNone;
    }
    
    private void EndStarDraw(GraphicsDevice device)
    {
        device.DepthStencilState = _prevDepth;
        device.BlendState = _prevBlend;
        device.RasterizerState = _prevRaster;
    }
    
    private void BuildQuads(Matrix view)
    {
        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);
        
        float half = StarSize * 0.5f;
        var dx = right * half;
        var dy = up * half;
        
        for (int i = 0; i < Count; i++)
        {
            var pos = Stars[i].Position;
            var color = Stars[i].Color;
            
            int vi = i * 4;
            QuadVerts[vi + 0] = new VertexPositionColorTexture(pos - dx - dy, color, new Vector2(0, 1));
            QuadVerts[vi + 1] = new VertexPositionColorTexture(pos + dx - dy, color, new Vector2(1, 1));
            QuadVerts[vi + 2] = new VertexPositionColorTexture(pos + dx + dy, color, new Vector2(1, 0));
            QuadVerts[vi + 3] = new VertexPositionColorTexture(pos - dx + dy, color, new Vector2(0, 0));
        }
    }
    
    private void BuildStreaks(float length, float speed)
    {
        for (int i = 0; i < Count; i++)
        {
            var pos = Stars[i].Position;
            var color = Stars[i].Color;
            
            var tail = pos + new Vector3(0, 0, Math.Sign(speed) * length);
            var dimColor = new Color(
                (byte)(color.R * 0.3f),
                (byte)(color.G * 0.3f),
                (byte)(color.B * 0.3f));
            
            StreakVerts[i * 2] = new VertexPositionColorTexture(pos, color, Vector2.Zero);
            StreakVerts[i * 2 + 1] = new VertexPositionColorTexture(tail, dimColor, Vector2.Zero);
        }
    }
}
