using MiniToolbox.Spica.Math3D;

using System.Numerics;

namespace MiniToolbox.Spica.Formats.CtrGfx.Light
{
    public class GfxAmbientLight : GfxLight
    {
        private Vector4 ColorF;

        public RGBA Color;

        private bool IsDirty;
    }
}
