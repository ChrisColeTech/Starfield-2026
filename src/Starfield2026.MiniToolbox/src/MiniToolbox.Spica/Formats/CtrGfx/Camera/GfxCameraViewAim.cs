using System.Numerics;

namespace MiniToolbox.Spica.Formats.CtrGfx.Camera
{
    public class GfxCameraViewAim : GfxCameraView
    {
        public GfxCameraViewAimFlags Flags;

        public Vector3 Target;

        public float Twist;
    }
}
