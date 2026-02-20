namespace MiniToolbox.Gdb1.Models;

/// <summary>
/// Texture format identifiers for 3DS/Wii U GPU.
/// </summary>
public enum Gdb1TextureFormat
{
    RGBA8 = 0,
    RGB8 = 1,
    RGBA5551 = 2,
    RGB565 = 3,
    RGBA4 = 4,
    LA8 = 5,
    L8 = 6,
    A8 = 7,
    LA4 = 8,
    L4 = 9,
    A4 = 10,
    ETC1 = 11,
    ETC1A4 = 12
}

/// <summary>
/// Information about an extracted texture.
/// </summary>
public class TextureInfo
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public Gdb1TextureFormat Format { get; set; }
    public int DataSize { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public string FormatName => Format.ToString();
}
