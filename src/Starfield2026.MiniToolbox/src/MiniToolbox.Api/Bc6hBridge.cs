using System.Runtime.InteropServices;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using Hexa.NET.DirectXTex;

namespace SwitchToolboxCli.Api;

/// <summary>
/// Bridge for Tegra swizzle/deswizzle via P/Invoke.
/// Wraps tegra_swizzle_x64.dll so we can call it from C# instead of koffi.
/// </summary>
public static class TegraSwizzleBridge
{
    [DllImport("tegra_swizzle_x64.dll", EntryPoint = "deswizzle_block_linear", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DeswizzleBlockLinear(
        ulong width, ulong height, ulong depth,
        byte[] source, ulong sourceLength,
        byte[] destination, ulong destinationLength,
        ulong blockHeight, ulong bytesPerPixel);

    public static byte[] Deswizzle(
        int width, int height,
        int blockW, int blockH,
        int bpp,
        int blockHeightLog2,
        byte[] data)
    {
        int bw = (width + blockW - 1) / blockW;
        int bh = (height + blockH - 1) / blockH;
        int bytesPerPixel = (ulong)bpp > 0 ? bpp : 1;
        ulong ww = (ulong)bw;
        ulong hh = (ulong)bh;
        ulong depth = 1;
        ulong blkH = (ulong)(1 << blockHeightLog2);
        ulong bppU = (ulong)bytesPerPixel;

        int expectedLen = bw * bh * bytesPerPixel;
        byte[] destination = new byte[data.Length];

        DeswizzleBlockLinear(ww, hh, depth,
            data, (ulong)data.Length,
            destination, (ulong)destination.Length,
            blkH, bppU);

        return destination;
    }
}

/// <summary>
/// DirectXTex-based BC6H decoder (Microsoft reference implementation).
/// Uses Hexa.NET.DirectXTex (thin .NET wrapper for DirectXTex native library).
/// DXGI_FORMAT values: BC6H_UF16=95, BC6H_SF16=96, R32G32B32A32_FLOAT=2
/// </summary>
public static class DirectXTexDecoder
{
    private const int DXGI_FORMAT_R32G32B32A32_FLOAT = 2;
    private const int DXGI_FORMAT_BC6H_UF16 = 95;
    private const int DXGI_FORMAT_BC6H_SF16 = 96;

    public static byte[] DecodeBc6hSf16(byte[] compressedData, int width, int height)
    {
        return DecodeDXGI(compressedData, width, height, DXGI_FORMAT_BC6H_SF16);
    }

    public static byte[] DecodeBc6hUf16(byte[] compressedData, int width, int height)
    {
        return DecodeDXGI(compressedData, width, height, DXGI_FORMAT_BC6H_UF16);
    }

    private static unsafe byte[] DecodeDXGI(byte[] compressedData, int width, int height, int format)
    {
        ScratchImage srcImage = DirectXTex.CreateScratchImage();
        ScratchImage decompressed = DirectXTex.CreateScratchImage();
        try
        {
            // Initialize a 2D image with the compressed format
            DirectXTex.Initialize2D(srcImage, format, (nuint)width, (nuint)height, 1, 1, CPFlags.None);

            // Get the first image and copy compressed data into it
            var images = srcImage.GetImages();
            if (images == null)
                throw new Exception("Failed to get images from ScratchImage");

            var img = images[0];
            var dst = new Span<byte>((void*)img.Pixels, (int)img.SlicePitch);
            compressedData.AsSpan(0, Math.Min(compressedData.Length, dst.Length)).CopyTo(dst);

            // Decompress to R32G32B32A32_FLOAT
            var srcMeta = srcImage.GetMetadata();
            var hr = DirectXTex.Decompress2(
                srcImage.GetImages(), srcImage.GetImageCount(), ref srcMeta,
                DXGI_FORMAT_R32G32B32A32_FLOAT, ref decompressed);
            
            if ((int)hr < 0)
                throw new Exception($"DirectXTex Decompress failed: HRESULT=0x{(int)hr:X8}");

            // Read float pixels
            var dstImg = decompressed.GetImages()[0];
            var floatSpan = new ReadOnlySpan<float>((void*)dstImg.Pixels, width * height * 4);

            var rgba = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                float r = Math.Max(0, floatSpan[i * 4 + 0]);
                float g = Math.Max(0, floatSpan[i * 4 + 1]);
                float b = Math.Max(0, floatSpan[i * 4 + 2]);
                // Reinhard tonemap
                rgba[i * 4 + 0] = (byte)Math.Min(255, (int)(r / (r + 1f) * 255f));
                rgba[i * 4 + 1] = (byte)Math.Min(255, (int)(g / (g + 1f) * 255f));
                rgba[i * 4 + 2] = (byte)Math.Min(255, (int)(b / (b + 1f) * 255f));
                rgba[i * 4 + 3] = 255;
            }
            return rgba;
        }
        finally
        {
            srcImage.Release();
            decompressed.Release();
        }
    }
}

/// <summary>
/// Bridge for BCn texture decode via BCnEncoder.Net.
/// Supports BC1–BC5 and BC7. BC6H is handled by DirectXTexDecoder.
/// </summary>
public static class BcnDecodeBridge
{
    /// <summary>
    /// Decode a BCn-encoded texture to RGBA8.
    /// formatCode: the BNTX surface format code (e.g. 0x1A01 for BC1).
    /// </summary>
    public static byte[] Decode(byte[] data, int width, int height, int formatCode)
    {
        var (format, bpp, bw, bh) = GetFormatInfo(formatCode);
        
        // For BC6H, use DirectXTex
        if (formatCode == 0x2006 || formatCode == 0x2106)
        {
            bool isSigned = formatCode == 0x2006;
            return isSigned 
                ? DirectXTexDecoder.DecodeBc6hSf16(data, width, height)
                : DirectXTexDecoder.DecodeBc6hUf16(data, width, height);
        }
        
        var decoder = new BcDecoder();
        int blocksX = (width + bw - 1) / bw;
        int blocksY = (height + bh - 1) / bh;
        var pixels = decoder.DecodeRaw(data, width, height, format);
        
        // BCnEncoder outputs in ColorRgba32 (R,G,B,A bytes)
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length && i < width * height; i++)
        {
            rgba[i * 4 + 0] = pixels[i].r;
            rgba[i * 4 + 1] = pixels[i].g;
            rgba[i * 4 + 2] = pixels[i].b;
            rgba[i * 4 + 3] = pixels[i].a;
        }
        return rgba;
    }

    private static (CompressionFormat format, int bpp, int blockW, int blockH) GetFormatInfo(int formatCode)
    {
        return formatCode switch
        {
            0x1A01 => (CompressionFormat.Bc1, 8, 4, 4),     // BC1
            0x1A02 => (CompressionFormat.Bc1, 8, 4, 4),     // BC1 (sRGB)
            0x1B01 => (CompressionFormat.Bc2, 16, 4, 4),    // BC2
            0x1B02 => (CompressionFormat.Bc2, 16, 4, 4),    // BC2 (sRGB)
            0x1C01 => (CompressionFormat.Bc3, 16, 4, 4),    // BC3
            0x1C02 => (CompressionFormat.Bc3, 16, 4, 4),    // BC3 (sRGB)
            0x1D01 => (CompressionFormat.Bc4, 8, 4, 4),     // BC4
            0x1D02 => (CompressionFormat.Bc4, 8, 4, 4),     // BC4 (signed)
            0x1E01 => (CompressionFormat.Bc5, 16, 4, 4),    // BC5
            0x1E02 => (CompressionFormat.Bc5, 16, 4, 4),    // BC5 (signed)
            0x2001 => (CompressionFormat.Bc7, 16, 4, 4),    // BC7
            0x2002 => (CompressionFormat.Bc7, 16, 4, 4),    // BC7 (sRGB)
            _ => throw new ArgumentException($"Unsupported format: 0x{formatCode:X4}")
        };
    }
}
