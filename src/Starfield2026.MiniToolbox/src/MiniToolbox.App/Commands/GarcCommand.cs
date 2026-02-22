using MiniToolbox.Garc;
using MiniToolbox.Garc.Compressions;
using MiniToolbox.Garc.Containers;
using MiniToolbox.Garc.Models.GenericFormats;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

namespace MiniToolbox.App.Commands;

/// <summary>
/// Command handler for GARC (3DS Pokemon) format extraction.
/// Supports Pokemon Sun/Moon, X/Y, and other 3DS titles that use GARC containers.
/// </summary>
public static class GarcCommand
{
    public static int Run(string[] args)
    {
        string? inputFile = null;
        string? outputDir = null;
        string? filter = null;
        string format = "dae";
        bool infoMode = false;
        bool listMode = false;
        bool extractMode = false;
        int limit = 0;
        int skip = 0;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--input" or "-i":
                    inputFile = args[++i];
                    break;
                case "--output" or "-o":
                    outputDir = args[++i];
                    break;
                case "--filter":
                    filter = args[++i];
                    break;
                case "--format" or "-f":
                    format = args[++i].ToLowerInvariant();
                    break;
                case "--info":
                    infoMode = true;
                    break;
                case "--list":
                    listMode = true;
                    break;
                case "--extract":
                    extractMode = true;
                    break;
                case "--limit" or "-n":
                    limit = int.Parse(args[++i]);
                    break;
                case "--skip":
                    skip = int.Parse(args[++i]);
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
            }
        }

        if (string.IsNullOrWhiteSpace(inputFile))
        {
            PrintUsage();
            return 1;
        }

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"ERROR: Input file not found: {inputFile}");
            return 1;
        }

        if (infoMode) return RunInfo(inputFile);
        if (listMode) return RunList(inputFile, skip, limit);
        if (extractMode)
        {
            outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "garc_export");
            return RunExtract(inputFile, outputDir, format, filter, skip, limit);
        }

        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("GARC Extractor - 3DS Pokemon model extraction (Sun/Moon, X/Y)");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  minitoolbox garc --input <garcFile> --info");
        Console.WriteLine("  minitoolbox garc --input <garcFile> --list [--skip N] [-n N]");
        Console.WriteLine("  minitoolbox garc --input <garcFile> --extract -o <outputDir> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -i, --input    Input GARC file (required)");
        Console.WriteLine("  -o, --output   Output directory (default: ./garc_export)");
        Console.WriteLine("  -f, --format   Output format: dae (default), obj");
        Console.WriteLine("  -n, --limit    Max entries to process (0 = all)");
        Console.WriteLine("  --skip         Skip first N entries");
        Console.WriteLine("  --filter       Only extract entries whose model/texture names contain this string");
        Console.WriteLine("                 (e.g. --filter tr for trainer models)");
        Console.WriteLine("  --info         Show GARC file summary");
        Console.WriteLine("  --list         List all entries with detected types");
        Console.WriteLine("  --extract      Extract models, textures, and animations");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  minitoolbox garc -i sun-moon-dump/RomFS/a/1/7/4 --info");
        Console.WriteLine("  minitoolbox garc -i sun-moon-dump/RomFS/a/1/7/4 --list");
        Console.WriteLine("  minitoolbox garc -i sun-moon-dump/RomFS/a/1/7/4 --extract -o ./trainers --filter tr");
        Console.WriteLine("  minitoolbox garc -i sun-moon-dump/RomFS/a/0/9/4 --extract -o ./pokemon -n 20");
    }

    private static int RunInfo(string inputFile)
    {
        Console.WriteLine($"Loading GARC: {inputFile}");
        var container = GARC.load(inputFile);

        Console.WriteLine($"  Entries: {container.content.Count}");

        // Sample first few entries for type detection
        int sampleCount = Math.Min(container.content.Count, 20);
        var typeCounts = new Dictionary<string, int>();

        for (int i = 0; i < sampleCount; i++)
        {
            byte[] data = MaterializeEntry(container, i);
            if (data == null || data.Length < 16)
            {
                Increment(typeCounts, "empty");
                continue;
            }

            try
            {
                var loaded = FileIO.load(new MemoryStream(data));
                string typeName = loaded.type.ToString();
                Increment(typeCounts, typeName);
            }
            catch
            {
                Increment(typeCounts, "error");
            }
        }

        Console.WriteLine($"  Sample ({sampleCount} entries):");
        foreach (var kvp in typeCounts.OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
        }

        container.data?.Close();
        return 0;
    }

    private static int RunList(string inputFile, int skip, int limit)
    {
        Console.WriteLine($"Loading GARC: {inputFile}");
        var container = GARC.load(inputFile);

        int start = Math.Min(skip, container.content.Count);
        int end = limit > 0
            ? Math.Min(start + limit, container.content.Count)
            : container.content.Count;

        Console.WriteLine($"Entries {start}-{end - 1} of {container.content.Count}:");
        Console.WriteLine();

        for (int i = start; i < end; i++)
        {
            var entry = container.content[i];
            string typeSuffix = "";

            byte[] data = MaterializeEntry(container, i);
            if (data != null && data.Length >= 16)
            {
                try
                {
                    var loaded = FileIO.load(new MemoryStream(data));
                    typeSuffix = $" [{loaded.type}]";

                    if (loaded.type.HasFlag(FileIO.formatType.model) && loaded.data is RenderBase.OModelGroup mdl)
                    {
                        string names = GetModelNames(mdl);
                        if (!string.IsNullOrEmpty(names))
                            typeSuffix += $" ({names})";
                    }
                }
                catch (Exception ex)
                {
                    typeSuffix = $" [error: {ex.Message}]";
                }
            }
            else
            {
                typeSuffix = data == null ? " [null]" : $" [{data.Length} bytes]";
            }

            Console.WriteLine($"  [{i:D5}] {entry.name}{typeSuffix}");
        }

        container.data?.Close();
        return 0;
    }

    private static int RunExtract(string inputFile, string outputDir, string format, string? filter, int skip, int limit)
    {
        Console.WriteLine($"Loading GARC: {inputFile}");
        Console.WriteLine($"Output: {outputDir}");
        Console.WriteLine($"Format: {format}");
        if (!string.IsNullOrEmpty(filter))
            Console.WriteLine($"Filter: {filter}");
        Console.WriteLine();

        var container = GARC.load(inputFile);
        Directory.CreateDirectory(outputDir);

        int start = Math.Min(skip, container.content.Count);
        int end = limit > 0
            ? Math.Min(start + limit, container.content.Count)
            : container.content.Count;

        int extracted = 0;
        int skipped = 0;
        int failed = 0;

        for (int i = start; i < end; i++)
        {
            byte[] data = MaterializeEntry(container, i);
            if (data == null || data.Length < 16)
            {
                skipped++;
                continue;
            }

            FileIO.LoadedFile loaded;
            try
            {
                loaded = FileIO.load(new MemoryStream(data));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  [{i:D5}] Load error: {ex.Message}");
                failed++;
                continue;
            }

            if (loaded.type == FileIO.formatType.unsupported)
            {
                skipped++;
                continue;
            }

            // Handle nested containers (e.g., Pokemon container entries)
            if (loaded.type.HasFlag(FileIO.formatType.container) && loaded.data is OContainer nested)
            {
                int subExtracted = ExtractContainer(nested, i, outputDir, format, filter);
                extracted += subExtracted;
                continue;
            }

            // Handle models
            if (loaded.type.HasFlag(FileIO.formatType.model) && loaded.data is RenderBase.OModelGroup modelGroup)
            {
                string modelName = GetPrimaryModelName(modelGroup) ?? $"entry_{i:D5}";

                if (!string.IsNullOrEmpty(filter) && !modelName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                string entryDir = Path.Combine(outputDir, SanitizeName(modelName));
                Directory.CreateDirectory(entryDir);

                try
                {
                    ExportModelGroup(modelGroup, entryDir, modelName, format);
                    extracted++;
                    Console.WriteLine($"  [{i:D5}] {modelName}: {modelGroup.model.Count} model(s), {modelGroup.texture.Count} texture(s), {modelGroup.skeletalAnimation.list.Count} clip(s)");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  [{i:D5}] Export error for {modelName}: {ex.Message}");
                    failed++;
                }
                continue;
            }

            // Handle standalone textures
            if (loaded.type.HasFlag(FileIO.formatType.texture) && loaded.data is RenderBase.OModelGroup texGroup)
            {
                foreach (var tex in texGroup.texture)
                {
                    if (!string.IsNullOrEmpty(filter) && !tex.name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string texDir = Path.Combine(outputDir, $"entry_{i:D5}_textures");
                    Directory.CreateDirectory(texDir);
                    SaveTexture(tex, texDir);
                    extracted++;
                }
                continue;
            }

            skipped++;
        }

        Console.WriteLine();
        Console.WriteLine($"=== Extract complete ===");
        Console.WriteLine($"  Extracted: {extracted}");
        Console.WriteLine($"  Skipped:   {skipped}");
        Console.WriteLine($"  Failed:    {failed}");

        container.data?.Close();
        return failed > 0 && extracted == 0 ? 1 : 0;
    }

    private static int ExtractContainer(OContainer nested, int parentIndex, string outputDir, string format, string? filter)
    {
        int extracted = 0;

        for (int j = 0; j < nested.content.Count; j++)
        {
            var sub = nested.content[j];
            byte[] subData = sub.data;
            if (subData == null || subData.Length < 16) continue;

            try
            {
                var subLoaded = FileIO.load(new MemoryStream(subData));
                if (subLoaded.type.HasFlag(FileIO.formatType.model) && subLoaded.data is RenderBase.OModelGroup mdl)
                {
                    string name = GetPrimaryModelName(mdl) ?? $"entry_{parentIndex:D5}_sub_{j:D3}";
                    if (!string.IsNullOrEmpty(filter) && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string dir = Path.Combine(outputDir, SanitizeName(name));
                    Directory.CreateDirectory(dir);
                    ExportModelGroup(mdl, dir, name, format);
                    extracted++;
                    Console.WriteLine($"  [{parentIndex:D5}/{j:D3}] {name}: {mdl.model.Count} model(s), {mdl.texture.Count} texture(s)");
                }
            }
            catch { }
        }

        return extracted;
    }

    private static void ExportModelGroup(RenderBase.OModelGroup modelGroup, string outputDir, string baseName, string format)
    {
        var manifestModels = new List<object>();
        var manifestTextures = new List<object>();
        var manifestClips = new List<object>();

        // Export models
        for (int m = 0; m < modelGroup.model.Count; m++)
        {
            string suffix = modelGroup.model.Count > 1 ? $"_{m}" : "";
            string modelFileName = $"model{suffix}.{format}";
            string modelFile = Path.Combine(outputDir, modelFileName);

            if (format == "obj")
            {
                OBJ.export(modelGroup, modelFile, m);
            }
            else
            {
                DAE.export(modelGroup, modelFile, m);
            }

            var mdl = modelGroup.model[m];
            manifestModels.Add(new
            {
                file = modelFileName,
                name = mdl.name ?? $"model{suffix}",
                meshCount = mdl.mesh.Count,
                boneCount = mdl.skeleton.Count
            });

            // Export animation clips as separate DAEs (split mode)
            if (format == "dae" && modelGroup.skeletalAnimation != null && modelGroup.skeletalAnimation.list.Count > 0)
            {
                string clipsDir = Path.Combine(outputDir, "clips");
                Directory.CreateDirectory(clipsDir);

                for (int a = 0; a < modelGroup.skeletalAnimation.list.Count; a++)
                {
                    string clipFileName = $"clip_{a:D3}.dae";
                    string clipFile = Path.Combine(clipsDir, clipFileName);
                    DAE.exportSkeletalClip(modelGroup, clipFile, m, a);

                    // Only add clip entries once (for first model)
                    if (m == 0)
                    {
                        var anim = modelGroup.skeletalAnimation.list[a] as RenderBase.OSkeletalAnimation;
                        manifestClips.Add(new
                        {
                            index = a,
                            name = anim?.name ?? $"clip_{a}",
                            file = $"clips/{clipFileName}",
                            frameCount = (int)(anim?.frameSize ?? 0),
                            fps = 30,
                            boneCount = anim?.bone.Count ?? 0
                        });
                    }
                }
            }
        }

        // Export textures
        foreach (var tex in modelGroup.texture)
        {
            SaveTexture(tex, outputDir);
            string texFileName = SanitizeName(tex.name) + ".png";
            manifestTextures.Add(new
            {
                name = tex.name,
                file = texFileName,
                width = tex.texture?.Width ?? 0,
                height = tex.texture?.Height ?? 0
            });
        }

        // Write manifest (matches TRPAK format where applicable)
        var manifest = new
        {
            version = 1,
            format,
            id = baseName,
            animationMode = "split",
            models = manifestModels,
            textures = manifestTextures,
            clips = manifestClips
        };

        string manifestPath = Path.Combine(outputDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SaveTexture(RenderBase.OTexture tex, string outputDir)
    {
        if (tex.texture == null) return;
        string texPath = Path.Combine(outputDir, SanitizeName(tex.name) + ".png");
        tex.texture.Save(texPath, ImageFormat.Png);
    }

    /// <summary>
    /// Materializes an entry's data from the GARC stream, handling decompression.
    /// </summary>
    private static byte[]? MaterializeEntry(OContainer container, int index)
    {
        var entry = container.content[index];

        if (!entry.loadFromDisk)
            return entry.data;

        if (container.data == null || entry.fileLength == 0)
            return null;

        try
        {
            container.data.Seek(entry.fileOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[entry.fileLength];
            int bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                int read = container.data.Read(buffer, bytesRead, buffer.Length - bytesRead);
                if (read == 0) break;
                bytesRead += read;
            }

            if (entry.doDecompression && buffer.Length > 0 && buffer[0] == 0x11)
            {
                uint decompressedSize = (uint)(buffer[1] | (buffer[2] << 8) | (buffer[3] << 16));
                return LZSS_Ninty.decompress(new MemoryStream(buffer), decompressedSize);
            }

            return buffer;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a display name from model textures (e.g., "tr0001_00" from "tr0001_00_BodyA1").
    /// </summary>
    private static string? GetPrimaryModelName(RenderBase.OModelGroup modelGroup)
    {
        // Try to get name from texture names (most reliable for Pokemon games)
        foreach (var tex in modelGroup.texture)
        {
            if (string.IsNullOrEmpty(tex.name)) continue;

            // Pattern: prefix_NNNN_NN_* (e.g., tr0001_00_BodyA1, pm0001_00_BodyA1)
            string name = tex.name;
            int underscoreCount = 0;
            int cutoff = -1;
            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] == '_')
                {
                    underscoreCount++;
                    if (underscoreCount == 2)
                    {
                        cutoff = i;
                        break;
                    }
                }
            }

            if (cutoff > 0)
                return name[..cutoff];
        }

        // Fallback to model name
        if (modelGroup.model.Count > 0 && !string.IsNullOrEmpty(modelGroup.model[0].name))
            return modelGroup.model[0].name;

        return null;
    }

    private static string GetModelNames(RenderBase.OModelGroup modelGroup)
    {
        var names = new HashSet<string>();
        string? primary = GetPrimaryModelName(modelGroup);
        if (primary != null) names.Add(primary);

        foreach (var mdl in modelGroup.model)
        {
            if (!string.IsNullOrEmpty(mdl.name))
                names.Add(mdl.name);
        }

        return string.Join(", ", names.Take(3));
    }

    private static string SanitizeName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
            name = name.Replace(c, '_');
        return name;
    }

    private static void Increment(Dictionary<string, int> dict, string key)
    {
        if (dict.ContainsKey(key))
            dict[key]++;
        else
            dict[key] = 1;
    }
}
