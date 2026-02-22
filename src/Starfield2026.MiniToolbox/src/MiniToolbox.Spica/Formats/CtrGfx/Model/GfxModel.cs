using MiniToolbox.Spica.Formats.CtrGfx.Model.Material;
using MiniToolbox.Spica.Formats.CtrGfx.Model.Mesh;
using MiniToolbox.Spica.PICA.Commands;
using MiniToolbox.Spica.Serialization.Attributes;

using System.Collections.Generic;

namespace MiniToolbox.Spica.Formats.CtrGfx.Model
{
    [TypeChoice(0x40000012u, typeof(GfxModel))]
    [TypeChoice(0x40000092u, typeof(GfxModelSkeletal))]
    public class GfxModel : GfxNodeTransform
    {
        public readonly List<GfxMesh> Meshes;

        public readonly GfxDict<GfxMaterial> Materials;

        public readonly List<GfxShape> Shapes;

        public readonly GfxDict<GfxMeshNodeVisibility> MeshNodeVisibilities;

        public GfxModelFlags Flags;

        public PICAFaceCulling FaceCulling;

        public int LayerId;

        public GfxModel()
        {
            Meshes = new List<GfxMesh>();

            Materials = new GfxDict<GfxMaterial>();

            Shapes = new List<GfxShape>();

            MeshNodeVisibilities = new GfxDict<GfxMeshNodeVisibility>();
        }
    }
}
