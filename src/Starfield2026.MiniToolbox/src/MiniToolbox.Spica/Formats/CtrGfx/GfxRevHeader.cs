using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrGfx
{
    public struct GfxRevHeader
    {
        public uint MagicNumber;

        [Version]
        public uint Revision;
    }
}
