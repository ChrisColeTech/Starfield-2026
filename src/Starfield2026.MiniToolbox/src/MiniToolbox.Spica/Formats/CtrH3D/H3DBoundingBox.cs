using MiniToolbox.Spica.Math3D;

using System.Numerics;

namespace MiniToolbox.Spica.Formats.CtrH3D
{
    public struct H3DBoundingBox
    {
        public Vector3   Center;
        public Matrix3x3 Orientation;
        public Vector3   Size;
    }
}
