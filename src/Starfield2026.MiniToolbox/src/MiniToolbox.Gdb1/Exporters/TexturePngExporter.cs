using MiniToolbox.Gdb1.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MiniToolbox.Gdb1.Exporters;

/// <summary>
/// Exports GDB1 textures to PNG format.
/// </summary>
public static class TexturePngExporter
{
    /// <summary>
    /// Exports a texture to PNG format.
    /// </summary>
    public static void Export(TextureInfo texture, string outputPath)
    {
        if (texture.Data == null || texture.Data.Length == 0)
            return;

        using var image = DecodeTexture(texture);
        image.SaveAsPng(outputPath);
    }

    /// <summary>
    /// Decodes raw texture data to an image.
    /// </summary>
    private static Image<Rgba32> DecodeTexture(TextureInfo texture)
    {
        var image = new Image<Rgba32>(texture.Width, texture.Height);
        var data = texture.Data;

        switch (texture.Format)
        {
            case Gdb1TextureFormat.RGBA8:
                DecodeRgba8(image, data);
                break;

            case Gdb1TextureFormat.RGB8:
                DecodeRgb8(image, data);
                break;

            case Gdb1TextureFormat.RGBA5551:
                DecodeRgba5551(image, data);
                break;

            case Gdb1TextureFormat.RGB565:
                DecodeRgb565(image, data);
                break;

            case Gdb1TextureFormat.RGBA4:
                DecodeRgba4(image, data);
                break;

            case Gdb1TextureFormat.LA8:
                DecodeLa8(image, data);
                break;

            case Gdb1TextureFormat.L8:
                DecodeL8(image, data);
                break;

            case Gdb1TextureFormat.A8:
                DecodeA8(image, data);
                break;

            default:
                // Unknown format - try RGBA8
                DecodeRgba8(image, data);
                break;
        }

        return image;
    }

    private static void DecodeRgba8(Image<Rgba32> image, byte[] data)
    {
        int width = image.Width;
        int height = image.Height;
        int offset = 0;

        for (int y = 0; y < height && offset + 3 < data.Length; y++)
        {
            for (int x = 0; x < width && offset + 3 < data.Length; x++)
            {
                byte r = data[offset++];
                byte g = data[offset++];
                byte b = data[offset++];
                byte a = data[offset++];
                image[x, y] = new Rgba32(r, g, b, a);
            }
        }
    }

    private static void DecodeRgb8(Image<Rgba32> image, byte[] data)
    {
        int width = image.Width;
        int height = image.Height;
        int offset = 0;

        for (int y = 0; y < height && offset + 2 < data.Length; y++)
        {
            for (int x = 0; x < width && offset + 2 < data.Length; x++)
            {
                byte r = data[offset++];
                byte g = data[offset++];
                byte b = data[offset++];
                image[x, y] = new Rgba32(r, g, b, 255);
            }
        }
    }

    private static void DecodeRgba5551(Image<Rgba32> image, byte[] data)
    {
        int width = image.Width;
        int height = image.Height;
        int offset = 0;

        for (int y = 0; y < height && offset + 1 < data.Length; y++)
        {
            for (int x = 0; x < width && offset + 1 < data.Length; x++)
            {
                ushort pixel = (ushort)(data[offset++] | (data[offset++] << 8));
                byte r = (byte)((pixel >> 11) * 255 / 31);
                byte g = (byte)(((pixel >> 6) & 0x1F) * 255 / 31);
                byte b = (byte)(((pixel >> 1) & 0x1F) * 255 / 31);
                byte a = (byte)((pixel & 1) * 255);
                image[x, y] = new Rgba32(r, g, b, a);
            }
        }
    }

    private static void DecodeRgb565(Image<Rgba32> image, byte[] data)
    {
        int width = image.Width;
        int height = image.Height;
        int offset = 0;

        for (int y = 0; y < height && offset + 1 < data.Length; y++)
        {
            for (int x = 0; x < width && offset + 1 < data.Length; x++)
            {
                ushort pixel = (ushort)(data[offset++] | (data[offset++] << 8));
                byte r = (byte)((pixel >> 11) * 255 / 31);
                byte g = (byte)(((pixel >> 5) & 0x3F) * 255 / 63);
                byte b = (byte)((pixel & 0x1F) * 255 / 31);
                image[x, y] = new Rgba32(r, g, b, 255);
            }
        }
    }

    private static void DecodeRgba4(Image<Rgba32> image, byte[] data)
    {
        int width = image.Width;
        int height = image.Height;
        int offset = 0;

        for (int y = 0; y < height && offset + 1 < data.Length; y++)
        {
            for (int x = 0; x < width && offset + 1 < data.Length; x++)
            {
                ushort pixel = (ushort)(data[offset++] | (data[offset++] << 8));
                byte r = (byte)(((pixel >> 12) & 0xF) * 17);
                byte g = (byte)(((pixel >> 8) & 0xF) * 17);
                byte b = (byte)(((pixel >> 4) & 0xF) * 17);
                byte a = (byte)((pixel & 0xF) * 17);
                image[x, y] = new Rgba32(r, g, b, a);
            }
        }
    }

    private static void DecodeLa8(Image<Rgba32> image, byte[] data)
    {
        int width = image.Width;
        int height = image.Height;
        int offset = 0;

        for (int y = 0; y < height && offset + 1 < data.Length; y++)
        {
            for (int x = 0; x < width && offset + 1 < data.Length; x++)
            {
                byte l = data[offset++];
                byte a = data[offset++];
                image[x, y] = new Rgba32(l, l, l, a);
            }
        }
    }

    private static void DecodeL8(Image<Rgba32> image, byte[] data)
    {
        int width = image.Width;
        int height = image.Height;
        int offset = 0;

        for (int y = 0; y < height && offset < data.Length; y++)
        {
            for (int x = 0; x < width && offset < data.Length; x++)
            {
                byte l = data[offset++];
                image[x, y] = new Rgba32(l, l, l, 255);
            }
        }
    }

    private static void DecodeA8(Image<Rgba32> image, byte[] data)
    {
        int width = image.Width;
        int height = image.Height;
        int offset = 0;

        for (int y = 0; y < height && offset < data.Length; y++)
        {
            for (int x = 0; x < width && offset < data.Length; x++)
            {
                byte a = data[offset++];
                image[x, y] = new Rgba32(255, 255, 255, a);
            }
        }
    }
}
