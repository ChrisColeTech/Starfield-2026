using System.Runtime.InteropServices;

namespace MiniToolbox.Core.Texture
{
    /// <summary>
    /// P/Invoke wrapper for tegra_swizzle_x64.dll (Rust native library).
    /// Handles Tegra X1 GPU block-linear deswizzling for Switch textures.
    /// Ported from Switch-Toolbox TegraX1Swizzle.cs — stripped of all UI/GL/32-bit code.
    /// </summary>
    public static class TegraSwizzle
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct BlockDim
        {
            public ulong width;
            public ulong height;
            public ulong depth;
        }

        [DllImport("tegra_swizzle_x64", EntryPoint = "deswizzle_surface")]
        private static unsafe extern void DeswizzleSurface(ulong width, ulong height, ulong depth,
            byte* source, ulong sourceLength,
            byte* destination, ulong destinationLength,
            BlockDim blockDim, ulong blockHeightMip0, ulong bytesPerPixel,
            ulong mipmapCount, ulong arrayCount);

        [DllImport("tegra_swizzle_x64", EntryPoint = "deswizzle_block_linear")]
        private static unsafe extern void DeswizzleBlockLinear(ulong width, ulong height, ulong depth,
            byte* source, ulong sourceLength,
            byte* destination, ulong destinationLength,
            ulong blockHeight, ulong bytesPerPixel);

        [DllImport("tegra_swizzle_x64", EntryPoint = "swizzled_surface_size")]
        private static extern ulong SwizzledSurfaceSize(ulong width, ulong height, ulong depth,
            BlockDim blockDim, ulong blockHeightMip0, ulong bytesPerPixel,
            ulong mipmapCount, ulong arrayCount);

        [DllImport("tegra_swizzle_x64", EntryPoint = "block_height_mip0")]
        private static extern ulong BlockHeightMip0(ulong height);

        [DllImport("tegra_swizzle_x64", EntryPoint = "mip_block_height")]
        private static extern ulong MipBlockHeight(ulong mipHeight, ulong blockHeightMip0);

        /// <summary>
        /// Deswizzle a Tegra X1 block-linear or pitch-linear texture surface.
        /// This is the primary entry point, matching Switch-Toolbox's TegraX1Swizzle.deswizzle().
        /// </summary>
        public static byte[] Deswizzle(uint width, uint height, uint depth,
            uint blkWidth, uint blkHeight, uint blkDepth,
            int roundPitch, uint bpp, uint tileMode, int blockHeightLog2, byte[] data)
        {
            if (tileMode == 1)
                return DeswizzlePitchLinear(width, height, depth, blkWidth, blkHeight, blkDepth, roundPitch, bpp, data);
            else
                return DeswizzleBlockLinearSurface(width, height, depth, blkWidth, blkHeight, blkDepth, bpp, blockHeightLog2, data);
        }

        private static unsafe byte[] DeswizzleBlockLinearSurface(uint width, uint height, uint depth,
            uint blkWidth, uint blkHeight, uint blkDepth, uint bpp, int blockHeightLog2, byte[] data)
        {
            // tegra_swizzle only allows block heights supported by the TRM (1,2,4,8,16,32).
            var blockHeightMip0 = (ulong)(1 << Math.Max(Math.Min(blockHeightLog2, 5), 0));

            // Convert to block dimensions for block compressed formats
            uint w = DivRoundUp(width, blkWidth);
            uint h = DivRoundUp(height, blkHeight);
            uint d = DivRoundUp(depth, blkDepth);

            var output = new byte[w * h * d * bpp];

            fixed (byte* dataPtr = data)
            fixed (byte* outputPtr = output)
            {
                DeswizzleBlockLinear(w, h, d, dataPtr, (ulong)data.Length,
                    outputPtr, (ulong)output.Length, blockHeightMip0, bpp);
            }

            return output;
        }

        private static byte[] DeswizzlePitchLinear(uint width, uint height, uint depth,
            uint blkWidth, uint blkHeight, uint blkDepth, int roundPitch, uint bpp, byte[] data)
        {
            width = DivRoundUp(width, blkWidth);
            height = DivRoundUp(height, blkHeight);
            depth = DivRoundUp(depth, blkDepth);

            uint pitch = width * bpp;
            if (roundPitch == 1)
                pitch = RoundUp(pitch, 32);

            uint surfSize = pitch * height;
            var result = new byte[surfSize];

            for (uint z = 0; z < depth; z++)
            {
                for (uint y = 0; y < height; y++)
                {
                    for (uint x = 0; x < width; x++)
                    {
                        uint pos = y * pitch + x * bpp;
                        uint pos_ = (y * width + x) * bpp;

                        if (pos + bpp <= surfSize)
                            Array.Copy(data, pos_, result, pos, bpp);
                    }
                }
            }

            return result;
        }

        public static ulong GetBlockHeight(uint heightInBytes) => BlockHeightMip0(heightInBytes);
        public static ulong GetMipBlockHeight(uint mipHeightInBytes, ulong blockHeightMip0) => MipBlockHeight(mipHeightInBytes, blockHeightMip0);

        public static uint DivRoundUp(uint n, uint d) => (n + d - 1) / d;
        public static uint Pow2RoundUp(uint x)
        {
            x -= 1;
            x |= x >> 1; x |= x >> 2; x |= x >> 4; x |= x >> 8; x |= x >> 16;
            return x + 1;
        }
        private static uint RoundUp(uint x, uint y) => ((x - 1) | (y - 1)) + 1;
    }
}
