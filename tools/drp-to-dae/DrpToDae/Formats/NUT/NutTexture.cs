using System;
using System.Collections.Generic;

namespace DrpToDae.Formats.NUT
{
    public enum NutFormat
    {
        Dxt1 = 0x00,
        Dxt3 = 0x01,
        Dxt5 = 0x02,
        Rgb565 = 0x08,
        Rgba16 = 0x0C,
        Rgba32 = 0x0E,
        Abgr32 = 0x10,
        Rgba32Alt = 0x11,
        Bc4 = 0x15,
        Bc5 = 0x16
    }

    public class TextureSurface
    {
        public List<byte[]> mipmaps = new List<byte[]>();
        public uint cubemapFace = 0;
    }

    public class NutTexture
    {
        public List<TextureSurface> surfaces = new List<TextureSurface>();
        public int MipMapsPerSurface => surfaces.Count > 0 ? surfaces[0].mipmaps.Count : 0;
        public uint HashId { get; set; }
        public int Width;
        public int Height;
        public NutFormat Format;

        public List<byte[]> GetAllMipmaps()
        {
            List<byte[]> mipmaps = new List<byte[]>();
            foreach (TextureSurface surface in surfaces)
            {
                foreach (byte[] mipmap in surface.mipmaps)
                {
                    mipmaps.Add(mipmap);
                }
            }
            return mipmaps;
        }

        public void SwapChannelOrderUp()
        {
            foreach (byte[] mip in GetAllMipmaps())
            {
                for (int t = 0; t < mip.Length; t += 4)
                {
                    byte t1 = mip[t];
                    mip[t] = mip[t + 1];
                    mip[t + 1] = mip[t + 2];
                    mip[t + 2] = mip[t + 3];
                    mip[t + 3] = t1;
                }
            }
        }

        public void SwapChannelOrderDown()
        {
            foreach (byte[] mip in GetAllMipmaps())
            {
                for (int t = 0; t < mip.Length; t += 4)
                {
                    byte t1 = mip[t + 3];
                    mip[t + 3] = mip[t + 2];
                    mip[t + 2] = mip[t + 1];
                    mip[t + 1] = mip[t];
                    mip[t] = t1;
                }
            }
        }

        public static NutFormat GetFormatFromNutFormat(int typet)
        {
            return typet switch
            {
                0x00 => NutFormat.Dxt1,
                0x01 => NutFormat.Dxt3,
                0x02 => NutFormat.Dxt5,
                0x08 => NutFormat.Rgb565,
                0x0C => NutFormat.Rgba16,
                0x0E => NutFormat.Rgba32,
                0x10 => NutFormat.Abgr32,
                0x11 => NutFormat.Rgba32Alt,
                0x15 => NutFormat.Bc4,
                0x16 => NutFormat.Bc5,
                _ => throw new NotImplementedException($"Unknown NUT texture format 0x{typet:X}")
            };
        }

        public static bool IsBlockCompressed(NutFormat format)
        {
            return format == NutFormat.Dxt1 || format == NutFormat.Dxt3 ||
                   format == NutFormat.Dxt5 || format == NutFormat.Bc4 ||
                   format == NutFormat.Bc5;
        }
    }
}
