using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrGfx.Texture
{
    public class GfxTextureImageData
    {
        public int Height;
        public int Width;

        [Section((uint)GfxSectionId.Image)] public byte[] RawBuffer;

        private uint DynamicAlloc;

        public int BitsPerPixel;

        private uint LocationPtr;
        private uint MemoryArea;
    }
}
