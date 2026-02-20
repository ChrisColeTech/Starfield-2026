using MiniToolbox.Core.Pipeline;
using MiniToolbox.Gdb1;
using System.Text.Json;

namespace MiniToolbox.App.Commands;

/// <summary>
/// Command handler for GDB1 (Star Fox Zero/Guard) format extraction.
/// </summary>
public static class Gdb1Command
{
    public static int Run(string[] args)
    {
        string? inputDir = null;
        string? modelId = null;
        string? outputDir = null;
        bool listMode = false;
        bool allMode = false;
        bool scanMode = false;
        int parallelism = Environment.ProcessorCount;
        var format = ModelFormat.Obj;
        var animMode = AnimationMode.Split;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--input" or "-i":
                    inputDir = args[++i];
                    break;
                case "--model" or "-m":
                    modelId = args[++i];
                    break;
                case "--output" or "-o":
                    outputDir = args[++i];
                    break;
                case "--format" or "-f":
                    format = ModelFormatExtensions.Parse(args[++i]);
                    break;
                case "--parallel" or "-p":
                    parallelism = int.Parse(args[++i]);
                    break;
                case "--list":
                    listMode = true;
                    break;
                case "--all":
                    allMode = true;
                    break;
                case "--scan":
                    scanMode = true;
                    break;
                case "--split":
                    animMode = AnimationMode.Split;
                    break;
                case "--baked":
                    animMode = AnimationMode.Baked;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(inputDir))
        {
            PrintUsage();
            return 1;
        }

        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"ERROR: Input directory not found: {inputDir}");
            return 1;
        }

        // Scan mode
        if (scanMode)
        {
            return RunScan(inputDir);
        }

        // List mode
        if (listMode)
        {
            return RunList(inputDir);
        }

        // All mode - uses pipeline for parallel extraction
        if (allMode)
        {
            outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "gdb1_export");
            return RunAllAsync(inputDir, outputDir, format, animMode, parallelism).GetAwaiter().GetResult();
        }

        // Single model mode - uses pipeline for consistency
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), modelId);
            return RunSingleAsync(inputDir, modelId, outputDir, format, animMode).GetAwaiter().GetResult();
        }

        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("GDB1 Extractor - Star Fox Zero/Guard model extraction");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  minitoolbox gdb1 --input <resourceDir> --scan");
        Console.WriteLine("  minitoolbox gdb1 --input <resourceDir> --list");
        Console.WriteLine("  minitoolbox gdb1 --input <resourceDir> --model <id> [-o <outputDir>] [-f obj|dae]");
        Console.WriteLine("  minitoolbox gdb1 --input <resourceDir> --all [-o <outputDir>] [-f obj|dae] [-p N]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -i, --input    Input directory containing .modelgdb/.texturegdb files");
        Console.WriteLine("  -m, --model    Model ID (filename without extension, e.g. 00b1486b)");
        Console.WriteLine("  -o, --output   Output directory (default: current dir)");
        Console.WriteLine("  -f, --format   Output format: obj (default), dae");
        Console.WriteLine("  -p, --parallel Max parallel jobs (default: CPU count)");
        Console.WriteLine("  --scan         Scan and report resource counts");
        Console.WriteLine("  --list         List all available models");
        Console.WriteLine("  --all          Extract all models in parallel");
        Console.WriteLine("  --split        Export animations as separate clip files (default)");
        Console.WriteLine("  --baked        Export animations embedded in model files");
    }

    private static int RunScan(string inputDir)
    {
        Console.WriteLine($"Scanning resources in: {inputDir}");
        var db = new ResourceDatabase(inputDir);

        Console.WriteLine($"  Models:     {db.ModelCount}");
        Console.WriteLine($"  Textures:   {db.TextureCount}");
        Console.WriteLine($"  Animations: {db.AnimationCount}");

        return 0;
    }

    private static int RunList(string inputDir)
    {
        Console.WriteLine($"Models in: {inputDir}");
        var db = new ResourceDatabase(inputDir);

        int count = 0;
        foreach (var (id, path) in db.Models.OrderBy(x => x.Key))
        {
            // Try to get the model name from metadata
            string name = id;
            string metaPath = Path.ChangeExtension(path, ".resourcemetadata");
            if (File.Exists(metaPath))
            {
                try
                {
                    var parser = new Gdb1Parser(File.ReadAllBytes(metaPath));
                    var strings = parser.ExtractStrings();
                    foreach (var s in strings)
                    {
                        if (s.Contains(".cmdl"))
                        {
                            name = $"{id} ({Path.GetFileNameWithoutExtension(s)})";
                            break;
                        }
                    }
                }
                catch { }
            }

            Console.WriteLine($"  {name}");
            count++;

            if (count >= 50 && db.ModelCount > 50)
            {
                Console.WriteLine($"  ... and {db.ModelCount - 50} more");
                break;
            }
        }

        Console.WriteLine($"\n{db.ModelCount} model(s) found.");
        return 0;
    }

    private static async Task<int> RunSingleAsync(string inputDir, string modelId, string outputDir, ModelFormat format, AnimationMode animMode)
    {
        Console.WriteLine($"Extracting model: {modelId}");
        Console.WriteLine($"Input: {inputDir}");
        Console.WriteLine($"Output: {outputDir}");
        Console.WriteLine($"Format: {format}");
        Console.WriteLine($"Animation mode: {animMode}");
        Console.WriteLine();

        var options = new ExtractionOptions
        {
            MaxParallelism = 1,
            KeepRawFiles = true,
            OutputFormat = format.ToString().ToLowerInvariant(),
            AnimationMode = animMode.ToString().ToLowerInvariant()
        };

        var extractor = new Gdb1FileGroupExtractor(inputDir, options);

        if (!extractor.ResourceDb.Models.ContainsKey(modelId))
        {
            Console.Error.WriteLine($"ERROR: Model not found: {modelId}");
            return 1;
        }

        var pipeline = new ExtractionPipeline(extractor, outputDir, options);
        var result = await pipeline.RunSingleAsync(modelId);

        if (!result.Success)
        {
            Console.Error.WriteLine($"ERROR: {result.ErrorMessage}");
            return 1;
        }

        if (format == ModelFormat.Dae)
        {
            Console.WriteLine("NOTE: DAE export not yet implemented. Exported as OBJ.");
        }

        Console.WriteLine($"\nExtracted: {result.JobName}");
        Console.WriteLine($"  Triangles: {result.Stats.GetValueOrDefault("triangles", 0)}");
        Console.WriteLine($"  Textures:  {result.Stats.GetValueOrDefault("textures", 0)}");
        Console.WriteLine($"  Clips:     {result.Stats.GetValueOrDefault("animations", 0)}");
        Console.WriteLine($"  Duration:  {result.Duration.TotalSeconds:F2}s");
        Console.WriteLine($"  Raw files: {pipeline.Workspace.TempRoot}");

        return 0;
    }

    private static async Task<int> RunAllAsync(string inputDir, string outputDir, ModelFormat format, AnimationMode animMode, int parallelism)
    {
        Console.WriteLine($"Batch extracting all models (parallel: {parallelism})");
        Console.WriteLine($"Input: {inputDir}");
        Console.WriteLine($"Output: {outputDir}");
        Console.WriteLine($"Format: {format}");
        Console.WriteLine();

        var options = new ExtractionOptions
        {
            MaxParallelism = parallelism,
            KeepRawFiles = true,
            ContinueOnError = true,
            OutputFormat = format.ToString().ToLowerInvariant(),
            AnimationMode = animMode.ToString().ToLowerInvariant()
        };

        var extractor = new Gdb1FileGroupExtractor(inputDir, options);

        Console.WriteLine($"Found {extractor.ResourceDb.ModelCount} models, {extractor.ResourceDb.TextureCount} textures, {extractor.ResourceDb.AnimationCount} animations");
        Console.WriteLine();

        var pipeline = new ExtractionPipeline(extractor, outputDir, options);

        // Progress callback
        pipeline.OnProgress += progress =>
        {
            if (progress.Success)
            {
                int tris = (int)progress.Stats.GetValueOrDefault("triangles", 0);
                int tex = (int)progress.Stats.GetValueOrDefault("textures", 0);
                int clips = (int)progress.Stats.GetValueOrDefault("animations", 0);
                Console.WriteLine($"[{progress.Current}/{progress.Total}] {progress.JobName}: {tris} tris, {tex} tex, {clips} clips");
            }
            else
            {
                Console.WriteLine($"[{progress.Current}/{progress.Total}] {progress.JobId}: FAILED - {progress.ErrorMessage}");
            }
        };

        var summary = await pipeline.RunAsync();

        if (format == ModelFormat.Dae)
        {
            Console.WriteLine("\nNOTE: DAE export not yet implemented. All models exported as OBJ.");
        }

        Console.WriteLine($"\n=== Batch complete ===");
        Console.WriteLine($"  Succeeded: {summary.SuccessCount}");
        Console.WriteLine($"  Failed:    {summary.FailedCount}");
        Console.WriteLine($"  Duration:  {summary.TotalDuration.TotalSeconds:F2}s");
        Console.WriteLine($"  Raw files: {pipeline.Workspace.TempRoot}");

        // Write summary
        var summaryData = new
        {
            format = format.ToString().ToLowerInvariant(),
            parallelism = parallelism,
            durationSeconds = summary.TotalDuration.TotalSeconds,
            succeeded = summary.SuccessCount,
            failed = summary.FailedCount,
            total = summary.TotalJobs,
            models = summary.Succeeded.Select(r => new
            {
                id = r.JobId,
                name = r.JobName,
                triangles = r.Stats.GetValueOrDefault("triangles", 0),
                textures = r.Stats.GetValueOrDefault("textures", 0),
                animations = r.Stats.GetValueOrDefault("animations", 0),
                durationMs = r.Duration.TotalMilliseconds
            }).ToArray(),
            failures = summary.Failed.Select(r => new
            {
                id = r.JobId,
                error = r.ErrorMessage
            }).ToArray()
        };

        string summaryPath = Path.Combine(outputDir, "extraction_summary.json");
        await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(summaryData, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"  Summary:   {summaryPath}");

        return 0;
    }
}
