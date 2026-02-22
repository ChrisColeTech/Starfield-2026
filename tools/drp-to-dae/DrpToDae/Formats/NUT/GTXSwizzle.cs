using System;

namespace DrpToDae.Formats.NUT
{
    public static class GTXSwizzle
    {
        public enum Gx2SurfaceFormat
        {
            Gx2SurfaceFormatTBc1Unorm = 0x31,
            Gx2SurfaceFormatTBc1Srgb = 0x431,
            Gx2SurfaceFormatTBc2Unorm = 0x32,
            Gx2SurfaceFormatTBc2Srgb = 0x432,
            Gx2SurfaceFormatTBc3Unorm = 0x33,
            Gx2SurfaceFormatTBc3Srgb = 0x433,
            Gx2SurfaceFormatTBc4Unorm = 0x34,
            Gx2SurfaceFormatTBc4Snorm = 0x234,
            Gx2SurfaceFormatTBc5Unorm = 0x35,
            Gx2SurfaceFormatTBc5Snorm = 0x235,
            Gx2SurfaceFormatTcsR8G8B8A8Unorm = 0x1A,
            Gx2SurfaceFormatTcsR8G8B8A8Srgb = 0x41A,
        }

        public enum AddrTileMode
        {
            AddrTmLinearGeneral = 0x0,
            AddrTmLinearAligned = 0x1,
            AddrTm1DTiledThin1 = 0x2,
            AddrTm1DTiledThick = 0x3,
            AddrTm2DTiledThin1 = 0x4,
            AddrTm2DTiledThin2 = 0x5,
            AddrTm2DTiledThin4 = 0x6,
            AddrTm2DTiledThick = 0x7,
            AddrTm2BTiledThin1 = 0x8,
            AddrTm2BTiledThin2 = 0x9,
            AddrTm2BTiledThin4 = 0x0A,
            AddrTm2BTiledThick = 0x0B,
            AddrTm3DTiledThin1 = 0x0C,
            AddrTm3DTiledThick = 0x0D,
            AddrTm3BTiledThin1 = 0x0E,
            AddrTm3BTiledThick = 0x0F,
        }

        public static byte[] SwizzleBc(byte[] data, int width, int height, int format, int tileMode, int pitch, int swizzle)
        {
            return SwizzleSurface(data, width, height, format, tileMode, pitch, swizzle,
                (Gx2SurfaceFormat)format != Gx2SurfaceFormat.Gx2SurfaceFormatTcsR8G8B8A8Unorm &&
                (Gx2SurfaceFormat)format != Gx2SurfaceFormat.Gx2SurfaceFormatTcsR8G8B8A8Srgb);
        }

        public static int GetBpp(int format)
        {
            switch ((Gx2SurfaceFormat)format)
            {
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc1Unorm:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc4Unorm:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc1Srgb:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc4Snorm:
                    return 0x40;
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc2Unorm:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc3Unorm:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc5Unorm:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc2Srgb:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc3Srgb:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTBc5Snorm:
                    return 0x80;
                case Gx2SurfaceFormat.Gx2SurfaceFormatTcsR8G8B8A8Unorm:
                case Gx2SurfaceFormat.Gx2SurfaceFormatTcsR8G8B8A8Srgb:
                    return 0x20;
            }
            return -1;
        }

        private static byte[] SwizzleSurface(byte[] data, int width, int height, int format, int tileMode, int pitch, int swizzle, bool isCompressed)
        {
            byte[] original = new byte[data.Length];
            data.CopyTo(original, 0);

            byte[] result = new byte[data.Length];

            int swizzleVal = ((swizzle >> 8) & 1) + (((swizzle >> 9) & 3) << 1);
            int blockSize;
            int w = width;
            int h = height;

            int bpp = GetBpp(format);

            if (isCompressed)
            {
                w /= 4;
                h /= 4;

                if ((Gx2SurfaceFormat)format == Gx2SurfaceFormat.Gx2SurfaceFormatTBc1Unorm ||
                    (Gx2SurfaceFormat)format == Gx2SurfaceFormat.Gx2SurfaceFormatTBc1Srgb ||
                    (Gx2SurfaceFormat)format == Gx2SurfaceFormat.Gx2SurfaceFormatTBc4Unorm ||
                    (Gx2SurfaceFormat)format == Gx2SurfaceFormat.Gx2SurfaceFormatTBc4Snorm)
                {
                    blockSize = 8;
                }
                else
                {
                    blockSize = 16;
                }
            }
            else
            {
                blockSize = bpp / 8;
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int pos = SurfaceAddrFromCoordMacroTiled(x, y, bpp, pitch, swizzleVal);
                    int size = (y * w + x) * blockSize;

                    for (int k = 0; k < blockSize; k++)
                    {
                        if (pos + k >= original.Length || size + k >= result.Length)
                            break;
                        result[size + k] = original[pos + k];
                    }
                }
            }
            return result;
        }

        private static int SurfaceAddrFromCoordMacroTiled(int x, int y, int bpp, int pitch, int swizzle)
        {
            int pixelIndex = ComputePixelIndexWithinMicroTile(x, y, bpp);
            int elemOffset = (bpp * pixelIndex) >> 3;

            int pipe = ComputePipeFromCoordWoRotation(x, y);
            int bank = ComputeBankFromCoordWoRotation(x, y);
            int bankPipe = ((pipe + 2 * bank) ^ swizzle) % 9;

            pipe = bankPipe % 2;
            bank = bankPipe / 2;

            int macroTileBytes = (bpp * 512 + 7) >> 3;
            int macroTileOffset = (x / 32 + pitch / 32 * (y / 16)) * macroTileBytes;

            int unk1 = elemOffset + (macroTileOffset >> 3);
            int unk2 = unk1 & ~0xFF;

            return (unk2 << 3) | (0xFF & unk1) | (pipe << 8) | (bank << 9);
        }

        private static int ComputePixelIndexWithinMicroTile(int x, int y, int bpp)
        {
            int bits = ((x & 4) << 1) | ((y & 2) << 3) | ((y & 4) << 3);

            if (bpp == 0x20 || bpp == 0x60)
            {
                bits |= (x & 1) | (x & 2) | ((y & 1) << 2);
            }
            else if (bpp == 0x40)
            {
                bits |= (x & 1) | ((y & 1) << 1) | ((x & 2) << 1);
            }
            else if (bpp == 0x80)
            {
                bits |= (y & 1) | ((x & 1) << 1) | ((x & 2) << 1);
            }

            return bits;
        }

        private static int ComputePipeFromCoordWoRotation(int x, int y)
        {
            return ((y >> 3) ^ (x >> 3)) & 1;
        }

        private static int ComputeBankFromCoordWoRotation(int x, int y)
        {
            int bankBit0 = ((y / (16 * 2)) ^ (x >> 3)) & 1;
            return bankBit0 | 2 * (((y / (8 * 2)) ^ (x >> 4)) & 1);
        }

        public static int GetFormatBpp(int format)
        {
            return format switch
            {
                0x31 or 0x34 or 0x234 or 0x431 => 64,
                0x32 or 0x33 or 0x35 or 0x432 or 0x433 or 0x235 => 128,
                0x1A or 0x41A => 32,
                _ => 0
            };
        }

        public static int ComputeSurfaceThickness(AddrTileMode tileMode)
        {
            return tileMode switch
            {
                AddrTileMode.AddrTm1DTiledThick or
                AddrTileMode.AddrTm2DTiledThick or
                AddrTileMode.AddrTm2BTiledThick or
                AddrTileMode.AddrTm3DTiledThick or
                AddrTileMode.AddrTm3BTiledThick => 4,
                _ => 1
            };
        }

        public static int ComputeSurfaceRotationFromTileMode(AddrTileMode tileMode)
        {
            return (int)tileMode switch
            {
                >= 4 and <= 11 => 2,
                >= 12 and <= 15 => 1,
                _ => 0
            };
        }

        public static int IsThickMacroTiled(AddrTileMode tileMode)
        {
            return tileMode switch
            {
                AddrTileMode.AddrTm2DTiledThick or
                AddrTileMode.AddrTm2BTiledThick or
                AddrTileMode.AddrTm3DTiledThick or
                AddrTileMode.AddrTm3BTiledThick => 1,
                _ => 0
            };
        }

        public static int IsBankSwappedTileMode(AddrTileMode tileMode)
        {
            return tileMode switch
            {
                AddrTileMode.AddrTm2BTiledThin1 or
                AddrTileMode.AddrTm2BTiledThin2 or
                AddrTileMode.AddrTm2BTiledThin4 or
                AddrTileMode.AddrTm2BTiledThick or
                AddrTileMode.AddrTm3BTiledThin1 or
                AddrTileMode.AddrTm3BTiledThick => 1,
                _ => 0
            };
        }

        public static int ComputeMacroTileAspectRatio(AddrTileMode tileMode)
        {
            return tileMode switch
            {
                AddrTileMode.AddrTm2BTiledThin1 or
                AddrTileMode.AddrTm3DTiledThin1 or
                AddrTileMode.AddrTm3BTiledThin1 => 1,
                AddrTileMode.AddrTm2DTiledThin2 or
                AddrTileMode.AddrTm2BTiledThin2 => 2,
                AddrTileMode.AddrTm2DTiledThin4 or
                AddrTileMode.AddrTm2BTiledThin4 => 4,
                _ => 1
            };
        }
    }
}
