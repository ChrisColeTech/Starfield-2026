using MiniToolbox.Spica.Formats.Common;
using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrGfx.Model.Material
{
    [TypeChoice(0x80000001u, typeof(GfxShaderReference))]
    public class GfxShaderReference : GfxObject
    {
        private string _Path;

        public string Path
        {
            get => _Path;
            set => _Path = value ?? throw Exceptions.GetNullException("Path");
        }

        private uint ShaderPtr;
    }
}
