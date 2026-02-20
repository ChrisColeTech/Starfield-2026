using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Battle;

public class SkeletalModelData
{
    public Vector3 BoundsMin { get; set; }
    public Vector3 BoundsMax { get; set; }
    
    public void Update(double totalSeconds) {}
    public void Draw(GraphicsDevice device, Effect effect) {}
    public void PlayIndex(int index) {}
}
