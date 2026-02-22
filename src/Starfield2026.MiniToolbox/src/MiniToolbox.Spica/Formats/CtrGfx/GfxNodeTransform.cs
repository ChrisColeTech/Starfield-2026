using MiniToolbox.Spica.Math3D;

using System.Numerics;

namespace MiniToolbox.Spica.Formats.CtrGfx
{
    public class GfxNodeTransform : GfxNode
    {
        public Vector3 TransformScale;
        public Vector3 TransformRotation;
        public Vector3 TransformTranslation;

        public Matrix3x4 LocalTransform;
        public Matrix3x4 WorldTransform;
    }
}
