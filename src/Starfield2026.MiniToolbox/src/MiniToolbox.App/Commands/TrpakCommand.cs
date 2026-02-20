using MiniToolbox.Core.Pipeline;
using MiniToolbox.Trpak;
using MiniToolbox.Trpak.Archive;
using System.Text.Json;

namespace MiniToolbox.App.Commands;

/// <summary>
/// Command handler for TRPAK (Pokemon Scarlet/Violet) format extraction.
/// </summary>
public static class TrpakCommand
{
    public static int Run(string[] args)
    {
        string? arcDir = null;
        string? modelPath = null;
        string? outputDir = null;
        bool listMode = false;
        bool allMode = false;
        int parallelism = Environment.ProcessorCount;
        var format = ModelFormat.Dae;
        var animMode = AnimationMode.Split;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--arc" or "-a":
                    arcDir = args[++i];
                    break;
                case "--model" or "-m":
                    modelPath = args[++i];
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
                case "--split":
                    animMode = AnimationMode.Split;
                    break;
                case "--baked":
                    animMode = AnimationMode.Baked;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(arcDir))
        {
            PrintUsage();
            return 1;
        }

        // Load hash cache
        Console.WriteLine("Loading hash cache...");
        var hashCache = new TrpakHashCache();
        string hashFile = Path.Combine(AppContext.BaseDirectory, "hashes_inside_fd.txt");
        if (File.Exists(hashFile))
        {
            hashCache.LoadHashList(File.ReadAllLines(hashFile));
            Console.WriteLine($"  {hashCache.Count} entries loaded.");
        }
        else
        {
            Console.Error.WriteLine($"  WARNING: {hashFile} not found - will use raw hashes.");
        }

        // Open archive
        Console.WriteLine($"Opening archive: {arcDir}");
        var loader = new TrpfsLoader(arcDir, hashCache);
        Console.WriteLine($"  {loader.FileCount} files in descriptor, {loader.PackNames.Count} packs.");

        // List mode
        if (listMode)
        {
            return RunList(loader);
        }

        // All mode - uses pipeline for parallel extraction
        if (allMode)
        {
            outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "export_all");
            return RunAllAsync(loader, outputDir, format, animMode, parallelism).GetAwaiter().GetResult();
        }

        // Single model mode
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            Console.Error.WriteLine("ERROR: --model <romfsPath> is required for export. Use --list to see available models.");
            return 1;
        }

        outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileNameWithoutExtension(modelPath));
        return RunSingleAsync(loader, modelPath, outputDir, format, animMode).GetAwaiter().GetResult();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("TRPAK Extractor - Pokemon Scarlet/Violet model extraction");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  minitoolbox trpak --arc <arcDir> --list");
        Console.WriteLine("  minitoolbox trpak --arc <arcDir> --model <path> [-o <outputDir>] [-f obj|dae]");
        Console.WriteLine("  minitoolbox trpak --arc <arcDir> --all [-o <outputDir>] [-f obj|dae] [-p N]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -a, --arc      Archive directory containing .trpfs/.trpak files");
        Console.WriteLine("  -m, --model    Model path within archive (e.g. pokemon/pm0001/pm0001_00.trmdl)");
        Console.WriteLine("  -o, --output   Output directory (default: current dir)");
        Console.WriteLine("  -f, --format   Output format: dae (default), obj");
        Console.WriteLine("  -p, --parallel Max parallel jobs (default: CPU count)");
        Console.WriteLine("  --list         List all available models");
        Console.WriteLine("  --all          Extract all models in parallel");
        Console.WriteLine("  --split        Export animations as clip-only DAEs (default)");
        Console.WriteLine("  --baked        Export animations with full baked geometry per clip");
    }

    private static int RunList(TrpfsLoader loader)
    {
        Console.WriteLine("\nAvailable .trmdl files:");
        int count = 0;
        foreach (var (hash, name) in loader.FindFilesByExtension(".trmdl"))
        {
            Console.WriteLine($"  {name}");
            count++;
        }
        Console.WriteLine($"\n{count} model(s) found.");
        return 0;
    }

    private static async Task<int> RunSingleAsync(TrpfsLoader loader, string modelPath, string outputDir, ModelFormat format, AnimationMode animMode)
    {
        Console.WriteLine($"Extracting model: {modelPath}");
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

        var extractor = new TrpakFileGroupExtractor(loader, options);

        // Find the job for this model
        var job = extractor.EnumerateJobs().FirstOrDefault(j =>
            ((string)j.Metadata["modelPath"]).Equals(modelPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));

        if (job == null)
        {
            Console.Error.WriteLine($"ERROR: Model not found: {modelPath}");
            return 1;
        }

        var pipeline = new ExtractionPipeline(extractor, outputDir, options);
        var result = await pipeline.RunSingleAsync(job.Id);

        if (!result.Success)
        {
            Console.Error.WriteLine($"ERROR: {result.ErrorMessage}");
            return 1;
        }

        if (format == ModelFormat.Obj)
        {
            Console.WriteLine("NOTE: OBJ export for TRPAK not yet implemented. Exported as DAE.");
        }

        Console.WriteLine($"\nExtracted: {result.JobName}");
        Console.WriteLine($"  Submeshes:  {result.Stats.GetValueOrDefault("submeshes", 0)}");
        Console.WriteLine($"  Materials:  {result.Stats.GetValueOrDefault("materials", 0)}");
        Console.WriteLine($"  Bones:      {result.Stats.GetValueOrDefault("bones", 0)}");
        Console.WriteLine($"  Textures:   {result.Stats.GetValueOrDefault("textures", 0)}");
        Console.WriteLine($"  Animations: {result.Stats.GetValueOrDefault("animations", 0)}");
        Console.WriteLine($"  Duration:   {result.Duration.TotalSeconds:F2}s");
        Console.WriteLine($"  Raw files:  {pipeline.Workspace.TempRoot}");

        return 0;
    }

    private static async Task<int> RunAllAsync(TrpfsLoader loader, string outputDir, ModelFormat format, AnimationMode animMode, int parallelism)
    {
        Console.WriteLine($"Batch extracting all models (parallel: {parallelism})");
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

        var extractor = new TrpakFileGroupExtractor(loader, options);
        var jobCount = extractor.EnumerateJobs().Count();

        Console.WriteLine($"Found {jobCount} models");
        Console.WriteLine();

        var pipeline = new ExtractionPipeline(extractor, outputDir, options);

        // Progress callback
        pipeline.OnProgress += progress =>
        {
            if (progress.Success)
            {
                int submeshes = (int)progress.Stats.GetValueOrDefault("submeshes", 0);
                int tex = (int)progress.Stats.GetValueOrDefault("textures", 0);
                int anims = (int)progress.Stats.GetValueOrDefault("animations", 0);
                Console.WriteLine($"[{progress.Current}/{progress.Total}] {progress.JobName}: {submeshes} submeshes, {tex} tex, {anims} anims");
            }
            else
            {
                Console.WriteLine($"[{progress.Current}/{progress.Total}] {progress.JobName}: FAILED - {progress.ErrorMessage}");
            }
        };

        var summary = await pipeline.RunAsync();

        if (format == ModelFormat.Obj)
        {
            Console.WriteLine("\nNOTE: OBJ export for TRPAK not yet implemented. All models exported as DAE.");
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
                submeshes = r.Stats.GetValueOrDefault("submeshes", 0),
                materials = r.Stats.GetValueOrDefault("materials", 0),
                bones = r.Stats.GetValueOrDefault("bones", 0),
                textures = r.Stats.GetValueOrDefault("textures", 0),
                animations = r.Stats.GetValueOrDefault("animations", 0),
                durationMs = r.Duration.TotalMilliseconds
            }).ToArray(),
            failures = summary.Failed.Select(r => new
            {
                id = r.JobId,
                name = r.JobName,
                error = r.ErrorMessage
            }).ToArray()
        };

        string summaryPath = Path.Combine(outputDir, "extraction_summary.json");
        await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(summaryData, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"  Summary:   {summaryPath}");

        return 0;
    }
}
