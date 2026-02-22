using MiniToolbox.Spica.PICA.Commands;

namespace MiniToolbox.Spica.Formats.CtrGfx
{
    public class GfxFragLightLUT
    {
        public PICALUTInput Input;
        public PICALUTScale Scale;

        public readonly GfxLUTReference Sampler;

        public GfxFragLightLUT()
        {
            Sampler = new GfxLUTReference();
        }
    }
}
