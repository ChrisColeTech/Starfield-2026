using MiniToolbox.Spica.Formats.MTFramework.Shader;

namespace MiniToolbox.Spica.Formats.MTFramework.Model
{
    public class MTMaterial
    {
        public uint NameHash;

        public MTAlphaBlend   AlphaBlend;
        public MTDepthStencil DepthStencil;

        public string Texture0Name;
        public string Name;
    }
}
