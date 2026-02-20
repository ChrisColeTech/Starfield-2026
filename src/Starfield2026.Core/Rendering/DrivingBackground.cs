using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering;

public class DrivingBackground : StarfieldBase
{
    public DrivingBackground(int starCount = 300) : base(starCount)
    {
        SpreadRadius = 80f;
        DepthRange = 150f;
    }

    public override void Update(float dt, float speed, Vector3 center)
    {
        Elapsed += dt;
        
        float drift = Math.Abs(speed) * dt * 0.02f;
        
        for (int i = 0; i < Count; i++)
        {
            var pos = Stars[i].Position;
            pos.Z += drift;
            
            if (pos.Z > DepthRange)
            {
                pos.Z = -DepthRange;
                RecycleStarXY(ref pos);
            }
            
            Stars[i].Position = pos;
        }
    }

    public override void Draw(GraphicsDevice device, Matrix view, Matrix proj, Vector3 center, float speed = 0f)
    {
        DrawDotsOnly(device, view, proj, center);
    }
}
