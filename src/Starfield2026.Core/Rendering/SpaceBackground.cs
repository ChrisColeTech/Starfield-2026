using System;
using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Rendering;

public class SpaceBackground : StarfieldBase
{
    public SpaceBackground(int starCount = 600) : base(starCount)
    {
        SpreadRadius = 60f;
        DepthRange = 120f;
    }

    public override void Update(float dt, float speed, Vector3 center)
    {
        Elapsed += dt;
        
        for (int i = 0; i < Count; i++)
        {
            var pos = Stars[i].Position;
            pos.Z += speed * dt * 0.5f;
            
            if (pos.Z > DepthRange)
            {
                pos.Z = -DepthRange;
                RecycleStarXY(ref pos);
            }
            else if (pos.Z < -DepthRange)
            {
                pos.Z = DepthRange;
                RecycleStarXY(ref pos);
            }
            
            Stars[i].Position = pos;
        }
    }
}
