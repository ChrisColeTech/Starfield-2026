using System.Text;

namespace MiniToolbox.App.Commands;

/// <summary>
/// Decodes Sun/Moon game text files (XOR encrypted, from GARC a/0/3/2).
/// Based on pk3DS TextFile.cs decryption algorithm.
/// </summary>
public static class TextDecodeCommand
{
    private const ushort KEY_BASE = 0x7C89;
    private const ushort KEY_ADVANCE = 0x2983;

    public static int Run(string[] args)
    {
        string? inputDir = null;
        string? outputDir = null;
        int entry = -1;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--input" or "-i": inputDir = args[++i]; break;
                case "--output" or "-o": outputDir = args[++i]; break;
                case "--entry" or "-e": entry = int.Parse(args[++i]); break;
                case "--help" or "-h": PrintUsage(); return 0;
            }
        }

        if (string.IsNullOrWhiteSpace(inputDir)) { PrintUsage(); return 1; }
        outputDir ??= Path.Combine(Path.GetDirectoryName(inputDir) ?? ".", "text_decoded");

        if (entry >= 0)
        {
            string path = Path.Combine(inputDir, $"entry_{entry:D5}.bin");
            if (!File.Exists(path)) { Console.Error.WriteLine($"Not found: {path}"); return 1; }
            var lines = DecodeTextFile(File.ReadAllBytes(path));
            for (int l = 0; l < lines.Length; l++)
                Console.WriteLine($"  [{l:D3}] {lines[l]}");
            return 0;
        }

        // Decode all entries
        Directory.CreateDirectory(outputDir);
        var files = Directory.GetFiles(inputDir, "entry_*.bin");
        Array.Sort(files);

        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var lines = DecodeTextFile(File.ReadAllBytes(file));
                string outPath = Path.Combine(outputDir, $"{name}.txt");
                File.WriteAllLines(outPath, lines);
                Console.WriteLine($"  {name}: {lines.Length} lines");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  {name}: ERROR {ex.Message}");
            }
        }

        Console.WriteLine($"Decoded to {outputDir}");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Text Decoder - Sun/Moon game text decryption");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  minitoolbox text -i <dumpDir> -o <outputDir>     Decode all entries");
        Console.WriteLine("  minitoolbox text -i <dumpDir> -e <entryIndex>    Decode one entry to console");
    }

    public static string[] DecodeTextFile(byte[] data)
    {
        if (data.Length < 0x10)
            return Array.Empty<string>();

        ushort lineCount = BitConverter.ToUInt16(data, 2);
        int sdo = (int)BitConverter.ToUInt32(data, 0xC); // section data offset (0x10)
        // sdo+0: 4-byte section length, then line metadata at sdo+4

        var lines = new string[lineCount];
        ushort key = KEY_BASE;

        for (int i = 0; i < lineCount; i++)
        {
            // Line metadata: offset (4 bytes) + length in chars (2 bytes) + 2 unused
            int metaBase = sdo + 4 + i * 8;
            if (metaBase + 8 > data.Length) break;

            int lineOffset = BitConverter.ToInt32(data, metaBase);     // relative to sdo
            short lineLength = BitConverter.ToInt16(data, metaBase + 4); // in characters (ushorts)

            int absOffset = sdo + lineOffset;
            int byteLength = lineLength * 2;
            if (absOffset + byteLength > data.Length)
            {
                lines[i] = "";
                key += KEY_ADVANCE;
                continue;
            }

            byte[] encrypted = new byte[byteLength];
            Array.Copy(data, absOffset, encrypted, 0, byteLength);

            byte[] decrypted = CryptLineData(encrypted, key);
            lines[i] = DecodeString(decrypted);

            key += KEY_ADVANCE;
        }

        return lines;
    }

    private static byte[] CryptLineData(byte[] data, ushort key)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i + 1 < result.Length; i += 2)
        {
            ushort val = BitConverter.ToUInt16(data, i);
            BitConverter.GetBytes((ushort)(val ^ key)).CopyTo(result, i);
            key = (ushort)(key << 3 | key >> 13);
        }
        return result;
    }

    private static string DecodeString(byte[] data)
    {
        var sb = new StringBuilder();
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            ushort c = BitConverter.ToUInt16(data, i);
            if (c == 0x0000) break; // null terminator
            if (c == 0x0010) // variable marker
            {
                // Skip: next word is count, then variable IDs
                if (i + 3 < data.Length)
                {
                    ushort varCount = BitConverter.ToUInt16(data, i + 2);
                    i += 2 + varCount * 2; // skip variable data
                    sb.Append("[VAR]");
                }
                continue;
            }
            if (c == 0xBE00) { sb.Append('\n'); continue; } // newline
            if (c == 0xBE01) { sb.Append("[CLEAR]"); continue; }
            if (c == 0xBE02) { sb.Append("[WAIT]"); continue; }

            sb.Append((char)c);
        }
        return sb.ToString();
    }
}
