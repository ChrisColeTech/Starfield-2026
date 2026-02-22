using MiniToolbox.Spica.Formats.Common;
using MiniToolbox.Spica.Formats.CtrGfx.Camera;
using MiniToolbox.Spica.Formats.CtrGfx.Light;
using MiniToolbox.Spica.Formats.CtrGfx.LUT;
using MiniToolbox.Spica.Formats.CtrGfx.Model;
using MiniToolbox.Spica.Formats.CtrGfx.Model.Material;
using MiniToolbox.Spica.Formats.CtrGfx.Model.Mesh;
using MiniToolbox.Spica.Formats.CtrGfx.Texture;
using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrGfx
{
    [TypeChoice(0x01000000, typeof(GfxMesh))]
    [TypeChoice(0x02000000, typeof(GfxSkeleton))]
    [TypeChoice(0x04000000, typeof(GfxLUT))]
    [TypeChoice(0x08000000, typeof(GfxMaterial))]
    [TypeChoice(0x10000001, typeof(GfxShape))]
    [TypeChoice(0x20000004, typeof(GfxTextureReference))]
    [TypeChoice(0x20000009, typeof(GfxTextureCube))]
    [TypeChoice(0x20000011, typeof(GfxTextureImage))]
    [TypeChoice(0x4000000a, typeof(GfxCamera))]
    [TypeChoice(0x40000012, typeof(GfxModel))]
    [TypeChoice(0x40000092, typeof(GfxModelSkeletal))]
    [TypeChoice(0x400000a2, typeof(GfxFragmentLight))]
    [TypeChoice(0x40000122, typeof(GfxHemisphereLight))]
    [TypeChoice(0x40000222, typeof(GfxVertexLight))]
    [TypeChoice(0x40000422, typeof(GfxAmbientLight))]
    [TypeChoice(0x80000001, typeof(GfxShaderReference))]
    public class GfxObject : INamed
    {
        protected GfxRevHeader Header;

        private string _Name;

        public string Name
        {
            get => _Name;
            set => _Name = value ?? throw Exceptions.GetNullException("Name");
        }

        public readonly GfxDict<GfxMetaData> MetaData;

        public GfxObject()
        {
            MetaData = new GfxDict<GfxMetaData>();
        }
    }
}
