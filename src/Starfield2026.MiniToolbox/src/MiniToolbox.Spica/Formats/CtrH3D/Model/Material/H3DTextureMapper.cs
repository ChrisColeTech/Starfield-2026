using MiniToolbox.Spica.Math3D;
using MiniToolbox.Spica.PICA.Commands;
using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrH3D.Model.Material
{
    public struct H3DTextureMapper
    {
        public byte SamplerType;

        private byte _WrapU;
        private byte _WrapV;

        public PICATextureWrap WrapU
        {
            get => (PICATextureWrap)_WrapU;
            set => _WrapU = (byte)value;
        }

        public PICATextureWrap WrapV
        {
            get => (PICATextureWrap)_WrapV;
            set => _WrapV = (byte)value;
        }

        public H3DTextureMagFilter MagFilter;
        public H3DTextureMinFilter MinFilter;

        [Padding(4)] public byte MinLOD;

        public float LODBias;

        public RGBA BorderColor;
    }
}
