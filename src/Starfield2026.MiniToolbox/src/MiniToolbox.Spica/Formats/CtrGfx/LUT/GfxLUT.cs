using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrGfx.LUT
{
    [TypeChoice(0x04000000u, typeof(GfxLUT))]
    public class GfxLUT : GfxObject
    {
        public readonly GfxDict<GfxLUTSampler> Samplers;

        public GfxLUT()
        {
            Samplers = new GfxDict<GfxLUTSampler>();
        }
    }
}
