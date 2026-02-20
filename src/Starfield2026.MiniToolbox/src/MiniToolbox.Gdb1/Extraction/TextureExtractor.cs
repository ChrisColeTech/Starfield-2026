using System.Buffers.Binary;
using MiniToolbox.Gdb1.Models;

namespace MiniToolbox.Gdb1.Extraction;

/// <summary>
/// Extracts textures from .texturegdb + .texturebin files.
/// </summary>
public class TextureExtractor
{
    private static readonly int[] PowerOf2 = { 16, 32, 64, 128, 256, 512, 1024, 2048 };

    private readonly string _gdbPath;
    private readonly string _binPath;
    private readonly string _metaPath;
    private readonly byte[] _gdbData;
    private readonly byte[] _binData;
    private readonly Gdb1Parser _parser;

    public TextureExtractor(string gdbPath)
    {
        _gdbPath = gdbPath;
        _binPath = Path.ChangeExtension(gdbPath, ".texturebin");
        _metaPath = Path.ChangeExtension(gdbPath, ".resourcemetadata");

        _gdbData = File.ReadAllBytes(_gdbPath);

        if (File.Exists(_binPath))
            _binData = File.ReadAllBytes(_binPath);
        else
            _binData = Array.Empty<byte>();

        _parser = new Gdb1Parser(_gdbData);
    }

    /// <summary>
    /// Gets the texture name from metadata or filename.
    /// </summary>
    public string GetTextureName()
    {
        if (File.Exists(_metaPath))
        {
            var metaData = File.ReadAllBytes(_metaPath);
            var metaParser = new Gdb1Parser(metaData);
            var strings = metaParser.ExtractStrings();

            foreach (var s in strings)
            {
                if (s.Contains(".tga") || s.Contains(".png"))
                    return Path.GetFileNameWithoutExtension(s);
            }
        }

        return Path.GetFileNameWithoutExtension(_gdbPath);
    }

    /// <summary>
    /// Finds texture width, height, and format from GDB data.
    /// </summary>
    private (int width, int height, Gdb1TextureFormat format) FindDimensions()
    {
        int width = 256;
        int height = 256;
        var format = Gdb1TextureFormat.RGBA8;

        for (int i = Gdb1Parser.HeaderSize; i < _gdbData.Length - 12; i += 4)
        {
            uint v1 = BinaryPrimitives.ReadUInt32LittleEndian(_gdbData.AsSpan(i, 4));
            uint v2 = BinaryPrimitives.ReadUInt32LittleEndian(_gdbData.AsSpan(i + 4, 4));

            if (Array.IndexOf(PowerOf2, (int)v1) >= 0 && Array.IndexOf(PowerOf2, (int)v2) >= 0)
            {
                width = (int)v1;
                height = (int)v2;

                uint v3 = BinaryPrimitives.ReadUInt32LittleEndian(_gdbData.AsSpan(i + 8, 4));
                if (v3 < 20)
                    format = (Gdb1TextureFormat)v3;

                break;
            }
        }

        return (width, height, format);
    }

    /// <summary>
    /// Extracts texture information.
    /// </summary>
    public TextureInfo Extract()
    {
        string name = GetTextureName();
        var (width, height, format) = FindDimensions();

        return new TextureInfo
        {
            Name = name,
            Width = width,
            Height = height,
            Format = format,
            DataSize = _binData.Length,
            Data = _binData
        };
    }

    /// <summary>
    /// Exports texture to raw file.
    /// </summary>
    public TextureInfo ExportRaw(string outputPath)
    {
        var info = Extract();
        info.FileName = Path.GetFileName(outputPath);

        if (_binData.Length > 0)
            File.WriteAllBytes(outputPath, _binData);

        return info;
    }

    public string GdbPath => _gdbPath;
    public string BinPath => _binPath;
    public byte[] BinData => _binData;
}
