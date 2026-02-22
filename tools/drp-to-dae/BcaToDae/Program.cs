using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("BCA to DAE Animation Converter (R&D)");
        Console.WriteLine("====================================");

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLower();

        try
        {
            switch (command)
            {
                case "dump":
                case "-d":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Error: dump requires a file path");
                        return 1;
                    }
                    DumpFile(args[1]);
                    break;

                case "batch-dump":
                case "-bd":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Error: batch-dump requires a directory path");
                        return 1;
                    }
                    BatchDump(args[1]);
                    break;

                case "convert":
                case "-c":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Error: convert requires bca_path vbn_path [output_path]");
                        return 1;
                    }
                    ConvertToDae(args[1], args[2], args.Length > 3 ? args[3] : null);
                    break;

                case "analyze":
                case "-a":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Error: analyze requires a file path");
                        return 1;
                    }
                    AnalyzeFile(args[1]);
                    break;

                case "compare-hashes":
                case "-ch":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Error: compare-hashes requires <bca_path> <vbn_path>");
                        return 1;
                    }
                    Dumpers.HashComparer.Compare(args[1], args[2]);
                    break;

                case "analyze-bcl":
                case "-ab":
                    if (args.Length < 4)
                    {
                        Console.WriteLine("Error: analyze-bcl requires <bcl_path> <bca_path> <vbn_path>");
                        return 1;
                    }
                    Dumpers.BclAnalyzer.Analyze(args[1], args[2], args[3]);
                    break;

                case "find-structure":
                case "-fs":
                    if (args.Length < 4)
                    {
                        Console.WriteLine("Error: find-structure requires <bcl_path> <bca_path> <vbn_path>");
                        return 1;
                    }
                    Dumpers.BclStructFinder.FindStructure(args[1], args[2], args[3]);
                    break;

                case "brute-force":
                case "-bf":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Error: brute-force requires <bca_path> <vbn_path>");
                        return 1;
                    }
                    Dumpers.HashBruteForce.Run(args[1], args[2]);
                    break;

                case "map-entries":
                case "-me":
                    if (args.Length < 4)
                    {
                        Console.WriteLine("Error: map-entries requires <bcl_path> <bca_path> <vbn_path>");
                        return 1;
                    }
                    Dumpers.BclEntryMapper.Map(args[1], args[2], args[3]);
                    break;

                case "probe":
                case "-p":
                    if (args.Length < 4)
                    {
                        Console.WriteLine("Error: probe requires <bcl_path> <bca_path> <vbn_path>");
                        return 1;
                    }
                    Dumpers.BclKeyframeProbe.Probe(args[1], args[2], args[3]);
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    return 1;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: bca-to-dae <command> [arguments]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  dump <file>           Dump structure of BCA/BCL/BCH file");
        Console.WriteLine("  batch-dump <dir>      Dump all animation files in directory");
        Console.WriteLine("  analyze <file>        Deep analysis with hex dumps");
        Console.WriteLine("  convert <bca> <vbn>   Convert animation to DAE (future)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  bca-to-dae dump BCA_0_p006_000_bca.raw");
        Console.WriteLine("  bca-to-dae analyze BCL_0_p006_000_bcl.raw");
        Console.WriteLine("  bca-to-dae batch-dump ./extracted_p006/");
    }

    static void DumpFile(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"File not found: {path}");
            return;
        }

        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 4)
        {
            Console.WriteLine("File too small");
            return;
        }

        string magic = Encoding.ASCII.GetString(data, 0, 4);
        Console.WriteLine($"\n=== {Path.GetFileName(path)} ===");
        Console.WriteLine($"Magic: {magic}");
        Console.WriteLine($"Size: {data.Length} bytes");

        switch (magic)
        {
            case "BCA ":
                Dumpers.BcaDumper.Dump(data);
                break;
            case "BCL ":
                Dumpers.BclDumper.Dump(data);
                break;
            case "BCH ":
                Dumpers.BchDumper.Dump(data);
                break;
            case "BCS ":
                Dumpers.BcsDumper.Dump(data);
                break;
            case "BHA ":
                Dumpers.BhaDumper.Dump(data);
                break;
            case "VBN ":
            case " NBV":
                Dumpers.VbnDumper.Dump(data);
                break;
            default:
                Console.WriteLine($"Unknown format: {magic}");
                HexDump(data, 0, Math.Min(256, data.Length));
                break;
        }
    }

    static void BatchDump(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"Directory not found: {directory}");
            return;
        }

        var files = Directory.GetFiles(directory, "*.raw", SearchOption.AllDirectories);
        Console.WriteLine($"Found {files.Length} .raw files\n");

        var stats = new Dictionary<string, int>();

        foreach (var file in files)
        {
            byte[] data = File.ReadAllBytes(file);
            if (data.Length < 4) continue;

            string magic = Encoding.ASCII.GetString(data, 0, 4);
            
            string cleanMagic = new string(magic.Select(c => c >= 32 && c < 127 ? c : '?').ToArray());
            if (!stats.ContainsKey(cleanMagic)) stats[cleanMagic] = 0;
            stats[cleanMagic]++;

            if (magic == "BCA " || magic == "BCL " || magic == "BCH ")
            {
                Console.WriteLine($"\n{new string('=', 60)}");
                DumpFile(file);
            }
        }

        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine("SUMMARY:");
        foreach (var kvp in stats.OrderBy(x => x.Key))
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value} files");
        }
    }

    static void AnalyzeFile(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"File not found: {path}");
            return;
        }

        byte[] data = File.ReadAllBytes(path);
        string magic = data.Length >= 4 ? Encoding.ASCII.GetString(data, 0, 4) : "????";

        Console.WriteLine($"\n=== DEEP ANALYSIS: {Path.GetFileName(path)} ===");
        Console.WriteLine($"Magic: {magic}");
        Console.WriteLine($"Size: {data.Length} bytes ({data.Length / 1024.0:F2} KB)");
        Console.WriteLine();

        Console.WriteLine("--- Header (256 bytes) ---");
        HexDump(data, 0, Math.Min(256, data.Length));

        Console.WriteLine("\n--- String Search ---");
        FindStrings(data);

        Console.WriteLine("\n--- Pattern Analysis ---");
        AnalyzePatterns(data);

        switch (magic)
        {
            case "BCL ":
                Dumpers.BclDumper.DeepAnalyze(data);
                break;
            case "BCA ":
                Dumpers.BcaDumper.DeepAnalyze(data);
                break;
            case "BCH ":
                Dumpers.BchDumper.DeepAnalyze(data);
                break;
        }
    }

    static void ConvertToDae(string bcaPath, string vbnPath, string? outputPath)
    {
        Console.WriteLine("Animation conversion not yet implemented.");
        Console.WriteLine("Complete Phase 0-3 first to parse the animation data.");
    }

    static void HexDump(byte[] data, int offset, int length)
    {
        int bytesPerLine = 16;
        for (int i = 0; i < length; i += bytesPerLine)
        {
            int addr = offset + i;
            int lineLen = Math.Min(bytesPerLine, length - i);

            Console.Write($"{addr:X6}: ");

            for (int j = 0; j < bytesPerLine; j++)
            {
                if (j < lineLen)
                    Console.Write($"{data[addr + j]:X2} ");
                else
                    Console.Write("   ");
                if (j == 7) Console.Write(" ");
            }

            Console.Write(" |");
            for (int j = 0; j < lineLen; j++)
            {
                byte b = data[addr + j];
                Console.Write(b >= 32 && b < 127 ? (char)b : '.');
            }
            Console.WriteLine("|");
        }
    }

    static void FindStrings(byte[] data)
    {
        var strings = new List<(int offset, string text)>();
        int minLen = 4;

        int start = 0;
        while (start < data.Length)
        {
            while (start < data.Length && (data[start] < 32 || data[start] > 126))
                start++;

            if (start >= data.Length) break;

            int end = start;
            while (end < data.Length && data[end] >= 32 && data[end] <= 126)
                end++;

            if (end - start >= minLen)
            {
                string text = Encoding.ASCII.GetString(data, start, end - start);
                strings.Add((start, text));
            }

            start = end + 1;
        }

        if (strings.Count == 0)
        {
            Console.WriteLine("No ASCII strings found.");
        }
        else
        {
            Console.WriteLine($"Found {strings.Count} strings:");
            foreach (var (offset, text) in strings.Take(30))
            {
                string display = text.Length > 50 ? text.Substring(0, 50) + "..." : text;
                Console.WriteLine($"  0x{offset:X4}: \"{display}\"");
            }
            if (strings.Count > 30)
                Console.WriteLine($"  ... and {strings.Count - 30} more");
        }
    }

    static void AnalyzePatterns(byte[] data)
    {
        Console.WriteLine($"First 4 bytes (hex): {BitConverter.ToString(data, 0, 4).Replace("-", " ")}");
        
        Console.WriteLine("\nPotential integers at offsets:");
        int[] offsets = { 0x04, 0x08, 0x0C, 0x10, 0x14, 0x18, 0x1C, 0x20, 0x24, 0x28, 0x2C, 0x30 };
        foreach (int off in offsets)
        {
            if (off + 4 <= data.Length)
            {
                uint valLE = BitConverter.ToUInt32(data, off);
                uint valBE = (uint)((data[off] << 24) | (data[off + 1] << 16) | (data[off + 2] << 8) | data[off + 3]);
                float fval = BitConverter.ToSingle(data, off);
                Console.WriteLine($"  0x{off:X2}: LE={valLE} (0x{valLE:X8}), BE={valBE}, float={fval:F4}");
            }
        }

        Console.WriteLine("\nSearching for common float values:");
        for (int i = 0; i < Math.Min(data.Length - 4, 512); i += 4)
        {
            float f = BitConverter.ToSingle(data, i);
            if (float.IsNormal(f) || f == 0)
            {
                if (Math.Abs(f - 60.0f) < 0.01f)
                    Console.WriteLine($"  0x{i:X4}: {f} (likely FPS=60)");
                else if (Math.Abs(f - 30.0f) < 0.01f)
                    Console.WriteLine($"  0x{i:X4}: {f} (likely FPS=30)");
                else if (Math.Abs(f - 1.0f) < 0.01f && i > 16)
                    Console.WriteLine($"  0x{i:X4}: {f} (likely scale=1.0)");
            }
        }
    }
}
