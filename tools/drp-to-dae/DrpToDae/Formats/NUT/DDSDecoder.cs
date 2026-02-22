using System;

namespace DrpToDae.Formats.NUT
{
    public static class DDSDecoder
    {
        public static byte[] Decode(byte[] data, int width, int height, NutFormat format)
        {
            byte[] pixels = new byte[width * height * 4];

            switch (format)
            {
                case NutFormat.Dxt1:
                    DecodeDxt1(pixels, data, width, height);
                    break;
                case NutFormat.Dxt3:
                    DecodeDxt3(pixels, data, width, height);
                    break;
                case NutFormat.Dxt5:
                    DecodeDxt5(pixels, data, width, height);
                    break;
                case NutFormat.Rgb565:
                    DecodeRgb565(pixels, data, width, height);
                    break;
                case NutFormat.Rgba16:
                    DecodeRgba16(pixels, data, width, height);
                    break;
                case NutFormat.Rgba32:
                case NutFormat.Abgr32:
                case NutFormat.Rgba32Alt:
                    DecodeRgba32(pixels, data, width, height, format);
                    break;
                case NutFormat.Bc4:
                    DecodeBc4(pixels, data, width, height);
                    break;
                case NutFormat.Bc5:
                    DecodeBc5(pixels, data, width, height);
                    break;
                default:
                    throw new NotImplementedException($"Format {format} not supported for decoding");
            }

            return pixels;
        }

        public static void DecodeDxt1(byte[] pixels, byte[] data, int width, int height)
        {
            int x = 0, y = 0;
            int p = 0;

            while (true)
            {
                byte[] block = new byte[8];
                int blockp = 0;
                for (int i = 0; i < 8; i++)
                    block[i] = data[p++];

                int[] pal = new int[4];
                pal[0] = MakeColor565(block[blockp++] & 0xFF, block[blockp++] & 0xFF);
                pal[1] = MakeColor565(block[blockp++] & 0xFF, block[blockp++] & 0xFF);

                int r = (2 * GetRed(pal[0]) + GetRed(pal[1])) / 3;
                int g = (2 * GetGreen(pal[0]) + GetGreen(pal[1])) / 3;
                int b = (2 * GetBlue(pal[0]) + GetBlue(pal[1])) / 3;

                pal[2] = (0xFF << 24) | (r << 16) | (g << 8) | (b);

                r = (2 * GetRed(pal[1]) + GetRed(pal[0])) / 3;
                g = (2 * GetGreen(pal[1]) + GetGreen(pal[0])) / 3;
                b = (2 * GetBlue(pal[1]) + GetBlue(pal[0])) / 3;

                pal[3] = (0xFF << 24) | (r << 16) | (g << 8) | (b);

                int[] index = new int[16];
                int indexp = 0;
                for (int i = 0; i < 4; i++)
                {
                    int by = block[blockp++] & 0xFF;
                    index[indexp++] = (by & 0x03);
                    index[indexp++] = (by & 0x0C) >> 2;
                    index[indexp++] = (by & 0x30) >> 4;
                    index[indexp++] = (by & 0xC0) >> 6;
                }

                for (int h = 0; h < 4; h++)
                {
                    for (int w = 0; w < 4; w++)
                    {
                        if (x + w >= width || y + h >= height) continue;
                        int color = (0xFF << 24) | (pal[index[(w) + (h) * 4]] & 0x00FFFFFF);
                        int pixelIdx = ((w + x) + (h + y) * width) * 4;
                        pixels[pixelIdx + 0] = (byte)((color >> 16) & 0xFF);
                        pixels[pixelIdx + 1] = (byte)((color >> 8) & 0xFF);
                        pixels[pixelIdx + 2] = (byte)(color & 0xFF);
                        pixels[pixelIdx + 3] = 0xFF;
                    }
                }

                x += 4;
                if (x >= width)
                {
                    x = 0;
                    y += 4;
                }
                if (y >= height)
                    break;
            }
        }

        public static void DecodeDxt3(byte[] pixels, byte[] data, int width, int height)
        {
            int x = 0, y = 0;
            int p = 0;

            while (true)
            {
                byte[] alphaBlock = new byte[8];
                for (int i = 0; i < 8; i++)
                    alphaBlock[i] = data[p++];

                byte[] alpha = new byte[16];
                for (int i = 0; i < 8; i++)
                {
                    alpha[i * 2] = (byte)((alphaBlock[i] & 0x0F) * 17);
                    alpha[i * 2 + 1] = (byte)((alphaBlock[i] >> 4) * 17);
                }

                byte[] block = new byte[8];
                int blockp = 0;
                for (int i = 0; i < 8; i++)
                    block[i] = data[p++];

                int[] pal = new int[4];
                pal[0] = MakeColor565(block[blockp++] & 0xFF, block[blockp++] & 0xFF);
                pal[1] = MakeColor565(block[blockp++] & 0xFF, block[blockp++] & 0xFF);

                int r = (2 * GetRed(pal[0]) + GetRed(pal[1])) / 3;
                int g = (2 * GetGreen(pal[0]) + GetGreen(pal[1])) / 3;
                int b = (2 * GetBlue(pal[0]) + GetBlue(pal[1])) / 3;

                pal[2] = (0xFF << 24) | (r << 16) | (g << 8) | (b);

                r = (2 * GetRed(pal[1]) + GetRed(pal[0])) / 3;
                g = (2 * GetGreen(pal[1]) + GetGreen(pal[0])) / 3;
                b = (2 * GetBlue(pal[1]) + GetBlue(pal[0])) / 3;

                pal[3] = (0xFF << 24) | (r << 16) | (g << 8) | (b);

                int[] index = new int[16];
                int indexp = 0;
                for (int i = 0; i < 4; i++)
                {
                    int by = block[blockp++] & 0xFF;
                    index[indexp++] = (by & 0x03);
                    index[indexp++] = (by & 0x0C) >> 2;
                    index[indexp++] = (by & 0x30) >> 4;
                    index[indexp++] = (by & 0xC0) >> 6;
                }

                for (int h = 0; h < 4; h++)
                {
                    for (int w = 0; w < 4; w++)
                    {
                        if (x + w >= width || y + h >= height) continue;
                        int colorIdx = index[(w) + (h) * 4];
                        int color = pal[colorIdx] & 0x00FFFFFF;
                        int pixelIdx = ((w + x) + (h + y) * width) * 4;
                        pixels[pixelIdx + 0] = (byte)((color >> 16) & 0xFF);
                        pixels[pixelIdx + 1] = (byte)((color >> 8) & 0xFF);
                        pixels[pixelIdx + 2] = (byte)(color & 0xFF);
                        pixels[pixelIdx + 3] = alpha[(w) + (h) * 4];
                    }
                }

                x += 4;
                if (x >= width)
                {
                    x = 0;
                    y += 4;
                }
                if (y >= height)
                    break;
            }
        }

        public static void DecodeDxt5(byte[] pixels, byte[] data, int width, int height)
        {
            int x = 0, y = 0;
            int p = 0;

            while (true)
            {
                byte[] block = new byte[8];
                int blockp = 0;

                for (int i = 0; i < 8; i++)
                    block[i] = data[p++];

                int a1 = block[blockp++] & 0xFF;
                int a2 = block[blockp++] & 0xFF;

                int aWord1 = (block[blockp++] & 0xFF) | ((block[blockp++] & 0xFF) << 8) | ((block[blockp++] & 0xFF) << 16);
                int aWord2 = (block[blockp++] & 0xFF) | ((block[blockp++] & 0xFF) << 8) | ((block[blockp++] & 0xFF) << 16);

                int[] a = new int[16];

                for (int i = 0; i < 16; i++)
                {
                    if (i < 8)
                    {
                        int code = (int)(aWord1 & 0x7);
                        aWord1 >>= 3;
                        a[i] = GetDxtaWord(code, a1, a2) & 0xFF;
                    }
                    else
                    {
                        int code = (int)(aWord2 & 0x7);
                        aWord2 >>= 3;
                        a[i] = GetDxtaWord(code, a1, a2) & 0xFF;
                    }
                }

                block = new byte[8];
                blockp = 0;
                for (int i = 0; i < 8; i++)
                    block[i] = data[p++];

                int[] pal = new int[4];
                pal[0] = MakeColor565(block[blockp++] & 0xFF, block[blockp++] & 0xFF);
                pal[1] = MakeColor565(block[blockp++] & 0xFF, block[blockp++] & 0xFF);

                int r = (2 * GetRed(pal[0]) + GetRed(pal[1])) / 3;
                int g = (2 * GetGreen(pal[0]) + GetGreen(pal[1])) / 3;
                int b = (2 * GetBlue(pal[0]) + GetBlue(pal[1])) / 3;

                pal[2] = (0xFF << 24) | (r << 16) | (g << 8) | (b);

                r = (2 * GetRed(pal[1]) + GetRed(pal[0])) / 3;
                g = (2 * GetGreen(pal[1]) + GetGreen(pal[0])) / 3;
                b = (2 * GetBlue(pal[1]) + GetBlue(pal[0])) / 3;

                pal[3] = (0xFF << 24) | (r << 16) | (g << 8) | (b);

                int[] index = new int[16];
                int indexp = 0;
                for (int i = 0; i < 4; i++)
                {
                    int by = block[blockp++] & 0xFF;
                    index[indexp++] = (by & 0x03);
                    index[indexp++] = (by & 0x0C) >> 2;
                    index[indexp++] = (by & 0x30) >> 4;
                    index[indexp++] = (by & 0xC0) >> 6;
                }

                for (int h = 0; h < 4; h++)
                {
                    for (int w = 0; w < 4; w++)
                    {
                        if (x + w >= width || y + h >= height) continue;
                        int color = (a[(w) + (h) * 4] << 24) | (pal[index[(w) + (h) * 4]] & 0x00FFFFFF);
                        int pixelIdx = ((w + x) + (h + y) * width) * 4;
                        pixels[pixelIdx + 0] = (byte)((color >> 16) & 0xFF);
                        pixels[pixelIdx + 1] = (byte)((color >> 8) & 0xFF);
                        pixels[pixelIdx + 2] = (byte)(color & 0xFF);
                        pixels[pixelIdx + 3] = (byte)((color >> 24) & 0xFF);
                    }
                }

                x += 4;
                if (x >= width)
                {
                    x = 0;
                    y += 4;
                }
                if (y >= height)
                    break;
            }
        }

        public static void DecodeRgb565(byte[] pixels, byte[] data, int width, int height)
        {
            int p = 0;
            for (int i = 0; i < width * height; i++)
            {
                ushort pixel = (ushort)(data[p] | (data[p + 1] << 8));
                p += 2;

                int r = (pixel >> 11) & 0x1F;
                int g = (pixel >> 5) & 0x3F;
                int b = pixel & 0x1F;

                r = (r << 3) | (r >> 2);
                g = (g << 2) | (g >> 4);
                b = (b << 3) | (b >> 2);

                int idx = i * 4;
                pixels[idx + 0] = (byte)r;
                pixels[idx + 1] = (byte)g;
                pixels[idx + 2] = (byte)b;
                pixels[idx + 3] = 0xFF;
            }
        }

        public static void DecodeRgba16(byte[] pixels, byte[] data, int width, int height)
        {
            int p = 0;
            for (int i = 0; i < width * height; i++)
            {
                ushort pixel = (ushort)(data[p] | (data[p + 1] << 8));
                p += 2;

                int a = (pixel >> 15) & 0x01;
                int r = (pixel >> 10) & 0x1F;
                int g = (pixel >> 5) & 0x1F;
                int b = pixel & 0x1F;

                r = (r << 3) | (r >> 2);
                g = (g << 3) | (g >> 2);
                b = (b << 3) | (b >> 2);
                a = a * 255;

                int idx = i * 4;
                pixels[idx + 0] = (byte)r;
                pixels[idx + 1] = (byte)g;
                pixels[idx + 2] = (byte)b;
                pixels[idx + 3] = (byte)a;
            }
        }

        public static void DecodeRgba32(byte[] pixels, byte[] data, int width, int height, NutFormat format)
        {
            for (int i = 0; i < width * height; i++)
            {
                int idx = i * 4;
                int dataIdx = i * 4;

                if (format == NutFormat.Abgr32)
                {
                    pixels[idx + 0] = data[dataIdx + 3];
                    pixels[idx + 1] = data[dataIdx + 2];
                    pixels[idx + 2] = data[dataIdx + 1];
                    pixels[idx + 3] = data[dataIdx + 0];
                }
                else
                {
                    pixels[idx + 0] = data[dataIdx + 0];
                    pixels[idx + 1] = data[dataIdx + 1];
                    pixels[idx + 2] = data[dataIdx + 2];
                    pixels[idx + 3] = data[dataIdx + 3];
                }
            }
        }

        public static void DecodeBc4(byte[] pixels, byte[] data, int width, int height)
        {
            int x = 0, y = 0;
            int p = 0;

            while (true)
            {
                byte r0 = data[p++];
                byte r1 = data[p++];

                byte[] r = new byte[16];
                ulong rIndices = 0;
                for (int i = 0; i < 6; i++)
                {
                    rIndices |= (ulong)data[p++] << (i * 8);
                }

                for (int i = 0; i < 16; i++)
                {
                    uint idx = (uint)((rIndices >> (3 * i)) & 0x7);
                    r[i] = GetBc4Value(idx, r0, r1);
                }

                for (int h = 0; h < 4; h++)
                {
                    for (int w = 0; w < 4; w++)
                    {
                        if (x + w >= width || y + h >= height) continue;
                        int pixelIdx = ((w + x) + (h + y) * width) * 4;
                        byte val = r[(w) + (h) * 4];
                        pixels[pixelIdx + 0] = val;
                        pixels[pixelIdx + 1] = val;
                        pixels[pixelIdx + 2] = val;
                        pixels[pixelIdx + 3] = 0xFF;
                    }
                }

                x += 4;
                if (x >= width)
                {
                    x = 0;
                    y += 4;
                }
                if (y >= height)
                    break;
            }
        }

        public static void DecodeBc5(byte[] pixels, byte[] data, int width, int height)
        {
            int x = 0, y = 0;
            int p = 0;

            while (true)
            {
                byte r0 = data[p++];
                byte r1 = data[p++];
                byte[] r = new byte[16];
                ulong rIndices = 0;
                for (int i = 0; i < 6; i++)
                    rIndices |= (ulong)data[p++] << (i * 8);
                for (int i = 0; i < 16; i++)
                {
                    uint idx = (uint)((rIndices >> (3 * i)) & 0x7);
                    r[i] = GetBc4Value(idx, r0, r1);
                }

                byte g0 = data[p++];
                byte g1 = data[p++];
                byte[] g = new byte[16];
                ulong gIndices = 0;
                for (int i = 0; i < 6; i++)
                    gIndices |= (ulong)data[p++] << (i * 8);
                for (int i = 0; i < 16; i++)
                {
                    uint idx = (uint)((gIndices >> (3 * i)) & 0x7);
                    g[i] = GetBc4Value(idx, g0, g1);
                }

                for (int h = 0; h < 4; h++)
                {
                    for (int w = 0; w < 4; w++)
                    {
                        if (x + w >= width || y + h >= height) continue;
                        int pixelIdx = ((w + x) + (h + y) * width) * 4;
                        int idx = (w) + (h) * 4;
                        pixels[pixelIdx + 0] = r[idx];
                        pixels[pixelIdx + 1] = g[idx];
                        pixels[pixelIdx + 2] = 0xFF;
                        pixels[pixelIdx + 3] = 0xFF;
                    }
                }

                x += 4;
                if (x >= width)
                {
                    x = 0;
                    y += 4;
                }
                if (y >= height)
                    break;
            }
        }

        private static byte GetBc4Value(uint idx, byte v0, byte v1)
        {
            if (v0 > v1)
            {
                return idx switch
                {
                    0 => v0,
                    1 => v1,
                    2 => (byte)((6 * v0 + 1 * v1) / 7),
                    3 => (byte)((5 * v0 + 2 * v1) / 7),
                    4 => (byte)((4 * v0 + 3 * v1) / 7),
                    5 => (byte)((3 * v0 + 4 * v1) / 7),
                    6 => (byte)((2 * v0 + 5 * v1) / 7),
                    7 => (byte)((1 * v0 + 6 * v1) / 7),
                    _ => 0
                };
            }
            else
            {
                return idx switch
                {
                    0 => v0,
                    1 => v1,
                    2 => (byte)((4 * v0 + 1 * v1) / 5),
                    3 => (byte)((3 * v0 + 2 * v1) / 5),
                    4 => (byte)((2 * v0 + 3 * v1) / 5),
                    5 => (byte)((1 * v0 + 4 * v1) / 5),
                    6 => 0,
                    7 => 255,
                    _ => 0
                };
            }
        }

        private static int GetDxtaWord(int code, int alpha0, int alpha1)
        {
            if (alpha0 > alpha1)
            {
                return code switch
                {
                    0 => alpha0,
                    1 => alpha1,
                    2 => (6 * alpha0 + 1 * alpha1) / 7,
                    3 => (5 * alpha0 + 2 * alpha1) / 7,
                    4 => (4 * alpha0 + 3 * alpha1) / 7,
                    5 => (3 * alpha0 + 4 * alpha1) / 7,
                    6 => (2 * alpha0 + 5 * alpha1) / 7,
                    7 => (1 * alpha0 + 6 * alpha1) / 7,
                    _ => 0
                };
            }
            else
            {
                return code switch
                {
                    0 => alpha0,
                    1 => alpha1,
                    2 => (4 * alpha0 + 1 * alpha1) / 5,
                    3 => (3 * alpha0 + 2 * alpha1) / 5,
                    4 => (2 * alpha0 + 3 * alpha1) / 5,
                    5 => (1 * alpha0 + 4 * alpha1) / 5,
                    6 => 0,
                    7 => 255,
                    _ => 0
                };
            }
        }

        private static int GetRed(int c) => (c & 0x00FF0000) >> 16;
        private static int GetGreen(int c) => (c & 0x0000FF00) >> 8;
        private static int GetBlue(int c) => c & 0x000000FF;

        private static int MakeColor565(int b1, int b2)
        {
            int bt = (b2 << 8) | b1;

            int r = (bt >> 11) & 0x1F;
            int g = (bt >> 5) & 0x3F;
            int b = bt & 0x1F;

            r = (r << 3) | (r >> 2);
            g = (g << 2) | (g >> 4);
            b = (b << 3) | (b >> 2);

            return (0xFF << 24) | (r << 16) | (g << 8) | b;
        }
    }
}
