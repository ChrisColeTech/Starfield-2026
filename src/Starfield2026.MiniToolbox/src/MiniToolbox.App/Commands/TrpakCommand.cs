using MiniToolbox.Core.Pipeline;
using MiniToolbox.Hashes;
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
        bool scanMode = false;
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
                case "--scan":
                    scanMode = true;
                    break;
                case "--scan-extract":
                    scanMode = true;
                    allMode = true; // reuse flags: scan=true + all=true = scan-extract
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

        // List packs mode - dump all pack names (readable even without hash list)
        if (args.Any(a => a.Equals("--list-packs", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"\nAll pack names ({loader.PackNames.Count}):");
            foreach (var name in loader.PackNames)
                Console.WriteLine($"  {name}");
            return 0;
        }

        // Generate hash list mode - build hash list from archive structure
        if (args.Any(a => a.Equals("--generate-hashes", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("\nGenerating hash list for archive...");
            var generator = new HashListGenerator(loader);
            generator.OnProgress += p =>
            {
                Console.WriteLine($"  {p.CurrentPack}");
            };

            // Use existing hash cache paths as templates for matching
            var templatePaths = hashCache.AllPaths();
            Console.WriteLine($"  Using {templatePaths.Count} template paths from hash cache.");

            var hashList = generator.Generate(templatePaths);
            Console.WriteLine($"\n  Generated {hashList.Count} hash entries.");

            // Write output
            string hashOutPath = outputDir != null
                ? Path.Combine(outputDir, "hashes_inside_fd.txt")
                : Path.Combine(arcDir, "hashes_inside_fd.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(hashOutPath)!);
            HashListGenerator.WriteHashList(hashOutPath, hashList);
            Console.WriteLine($"  Written to: {hashOutPath}");
            return 0;
        }

        // Scan mode
        if (scanMode && !allMode)
        {
            return RunScan(loader, outputDir);
        }

        // Scan-extract mode: find models by content then extract all pack files
        if (scanMode && allMode)
        {
            outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "scan_extract");
            return RunScanExtract(loader, outputDir);
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

    private static int RunScan(TrpfsLoader loader, string? outputDir)
    {
        Console.WriteLine("\nScanning all file hashes for TRMDL models (no hash list needed)...");
        var hashes = loader.FileHashes;
        int total = hashes.Length;
        int scanned = 0;
        int found = 0;
        int errors = 0;
        var models = new List<(ulong hash, int meshCount, int matCount, bool hasSkeleton)>();

        foreach (var hash in hashes)
        {
            scanned++;
            if (scanned % 10000 == 0)
                Console.Write($"\r  Scanned {scanned}/{total}... ({found} models found)");

            try
            {
                var bytes = loader.ExtractFile(hash);
                if (bytes == null || bytes.Length < 20) continue;

                // Try to deserialize as TRMDL FlatBuffer
                var mdl = MiniToolbox.Core.Utils.FlatBufferConverter.DeserializeFrom<MiniToolbox.Trpak.Flatbuffers.TR.Model.TRMDL>(bytes);
                if (mdl == null) continue;

                // Check if it looks like a valid model (has meshes or skeleton)
                int meshCount = mdl.Meshes?.Length ?? 0;
                int matCount = mdl.Materials?.Length ?? 0;
                bool hasSkeleton = mdl.Skeleton != null && !string.IsNullOrWhiteSpace(mdl.Skeleton.PathName);

                if (meshCount > 0 || hasSkeleton)
                {
                    // Try to get name from hash cache, fall back to raw hash
                    string? name = null;
                    foreach (var (h, n) in loader.FindFiles(n => true))
                    {
                        // This is expensive, just report by hash
                        break;
                    }

                    found++;
                    models.Add((hash, meshCount, matCount, hasSkeleton));
                    Console.Write($"\r  Scanned {scanned}/{total}... ({found} models found)");
                }
            }
            catch
            {
                errors++;
            }
        }

        Console.WriteLine($"\r  Scanned {total}/{total}. Done.                          ");
        Console.WriteLine($"\n  Found {found} probable TRMDL models ({errors} extraction errors).\n");

        // Write results
        if (outputDir != null)
        {
            Directory.CreateDirectory(outputDir);
            string reportPath = Path.Combine(outputDir, "scan_results.txt");
            using var writer = new StreamWriter(reportPath);
            writer.WriteLine($"# TRMDL Scan Results — {total} hashes scanned, {found} models found");
            writer.WriteLine($"# Errors: {errors}");
            writer.WriteLine();
            foreach (var (hash, meshCount, matCount, hasSkeleton) in models)
            {
                writer.WriteLine($"0x{hash:X16}  meshes={meshCount} materials={matCount} skeleton={hasSkeleton}");
            }
            Console.WriteLine($"  Report saved to: {reportPath}");
        }
        else
        {
            foreach (var (hash, meshCount, matCount, hasSkeleton) in models)
            {
                Console.WriteLine($"  0x{hash:X16}  meshes={meshCount} materials={matCount} skeleton={hasSkeleton}");
            }
        }

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

    /// <summary>
    /// Scan-extract: iterate packs one at a time, check for TRMDL content,
    /// write all files from model packs to disk. Memory-bounded: only one pack in memory at a time.
    /// </summary>
    private static int RunScanExtract(TrpfsLoader loader, string outputDir)
    {
        Console.WriteLine("\nScan-extract: iterating packs, looking for models...");
        Console.WriteLine($"  Output: {outputDir}");
        Directory.CreateDirectory(outputDir);

        var packNames = loader.PackNames;
        int totalPacks = packNames.Count;
        int modelsFound = 0;
        int packsWithModels = 0;
        int filesWritten = 0;

        // Build pack→file hashes index (lightweight — just ulong arrays)
        Console.WriteLine("  Building pack index...");
        var hashes = loader.FileHashes;
        var packFileHashes = new Dictionary<int, List<ulong>>();
        for (int i = 0; i < hashes.Length; i++)
        {
            var info = loader.GetFileInfo(i);
            if (info == null) continue;
            int packIdx = (int)info.PackIndex;
            if (!packFileHashes.TryGetValue(packIdx, out var list))
            {
                list = new List<ulong>();
                packFileHashes[packIdx] = list;
            }
            list.Add(hashes[i]);
        }
        Console.WriteLine($"  {packFileHashes.Count} packs indexed from {hashes.Length} files.");

        // Iterate packs one at a time — stream to disk
        string logPath = Path.Combine(outputDir, "_scan_log.txt");
        using var log = new StreamWriter(logPath, false) { AutoFlush = true };
        log.WriteLine($"Scan started at {DateTime.Now}");

        for (int packIdx = 0; packIdx < totalPacks; packIdx++)
        {
            if (packIdx % 500 == 0)
            {
                string msg = $"  Pack {packIdx}/{totalPacks} | {modelsFound} models | {filesWritten} files | {packsWithModels} packs";
                Console.WriteLine(msg);
                Console.Out.Flush();
                log.WriteLine(msg);
            }

            if (!packFileHashes.TryGetValue(packIdx, out var fileHashList))
                continue;

            string packName = packNames[packIdx];

            try
            {
                // Extract all files from this pack, check for TRMDL
                bool hasModel = false;
                var packFileData = new List<(ulong hash, byte[] data)>();
                MiniToolbox.Trpak.Flatbuffers.TR.Model.TRMDL? trmdl = null;
                int trmdlFileIndex = -1;

                foreach (var fileHash in fileHashList)
                {
                    try
                    {
                        var bytes = loader.ExtractFile(fileHash);
                        if (bytes == null) continue;
                        packFileData.Add((fileHash, bytes));

                        // Quick TRMDL check
                        if (!hasModel && bytes.Length >= 20)
                        {
                            try
                            {
                                var mdl = MiniToolbox.Core.Utils.FlatBufferConverter.DeserializeFrom<
                                    MiniToolbox.Trpak.Flatbuffers.TR.Model.TRMDL>(bytes);
                                if (mdl?.Meshes?.Length > 0 || mdl?.Skeleton != null)
                                {
                                    hasModel = true;
                                    modelsFound++;
                                    trmdl = mdl;
                                    trmdlFileIndex = packFileData.Count - 1;
                                }
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.WriteLine($"  ERR extracting hash 0x{fileHash:X16} from pack {packIdx}: {ex.Message}");
                    }
                }

                // Only write to disk if this pack contains a model
                if (hasModel && trmdl != null)
                {
                    string safeName = packName
                        .Replace("arc/", "")
                        .Replace(".trpak", "")
                        .Replace('/', '_')
                        .Replace('\\', '_');

                    string packDir = Path.Combine(outputDir, safeName);
                    Directory.CreateDirectory(packDir);

                    // Build expected filenames from TRMDL references
                    var expectedNames = new List<string>();
                    string modelBaseName = safeName; // default to pack name

                    // Helper: check if a path string is valid for Windows filesystem
                    bool IsValidPathRef(string? s) =>
                        !string.IsNullOrEmpty(s) && s.All(c => c >= 0x20 && c < 0x7F);

                    // Mesh references
                    if (trmdl.Meshes != null)
                    {
                        foreach (var mesh in trmdl.Meshes)
                        {
                            if (IsValidPathRef(mesh?.PathName))
                            {
                                expectedNames.Add(Path.GetFileName(mesh!.PathName));
                                if (modelBaseName == safeName)
                                    modelBaseName = Path.GetFileNameWithoutExtension(mesh.PathName);
                                string bufName = Path.GetFileNameWithoutExtension(mesh.PathName) + ".trmbf";
                                expectedNames.Add(bufName);
                            }
                        }
                    }

                    // Material references
                    if (trmdl.Materials != null)
                        foreach (var mat in trmdl.Materials)
                            if (IsValidPathRef(mat))
                                expectedNames.Add(Path.GetFileName(mat!));

                    // Skeleton reference
                    string? skelName = null;
                    if (IsValidPathRef(trmdl.Skeleton?.PathName))
                    {
                        skelName = Path.GetFileName(trmdl.Skeleton!.PathName);
                        expectedNames.Add(skelName);
                    }

                    // Write TRMDL file with proper name
                    if (trmdlFileIndex >= 0 && trmdlFileIndex < packFileData.Count)
                    {
                        string trmdlName = modelBaseName + ".trmdl";
                        File.WriteAllBytes(Path.Combine(packDir, trmdlName), packFileData[trmdlFileIndex].data);
                        filesWritten++;
                    }

                    // Write remaining files with type-detected names
                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int meshIdx = 0, matIdx = 0, sklIdx = 0, binIdx = 0, bntxIdx = 0;

                    for (int fi = 0; fi < packFileData.Count; fi++)
                    {
                        if (fi == trmdlFileIndex) continue; // already written

                        var (fileHash, data) = packFileData[fi];
                        string ext = DetectFileExtension(data);
                        string fileName;

                        switch (ext)
                        {
                            case ".trmsh":
                                // Match to TRMDL mesh reference by index
                                fileName = (meshIdx < trmdl.Meshes?.Length &&
                                    IsValidPathRef(trmdl.Meshes[meshIdx]?.PathName))
                                    ? Path.GetFileName(trmdl.Meshes[meshIdx]!.PathName)
                                    : $"mesh_{meshIdx:D2}.trmsh";
                                meshIdx++;
                                break;
                            case ".trskl":
                                fileName = skelName ?? $"skeleton_{sklIdx:D2}.trskl";
                                skelName = null; // first skeleton gets the referenced name
                                sklIdx++;
                                break;
                            case ".trmtr":
                                fileName = (matIdx < trmdl.Materials?.Length &&
                                    IsValidPathRef(trmdl.Materials[matIdx]))
                                    ? Path.GetFileName(trmdl.Materials[matIdx]!)
                                    : $"material_{matIdx:D2}.trmtr";
                                matIdx++;
                                break;
                            case ".bntx":
                                fileName = $"texture_{bntxIdx:D2}.bntx";
                                bntxIdx++;
                                break;
                            default:
                                // Could be .trmbf (mesh buffer) — detect by size proximity
                                if (data.Length > 1000 && meshIdx > 0)
                                    fileName = modelBaseName + ".trmbf";
                                else
                                    fileName = $"file_{binIdx:D3}{ext}";
                                binIdx++;
                                break;
                        }

                        // Deduplicate names
                        if (!usedNames.Add(fileName))
                        {
                            string basePart = Path.GetFileNameWithoutExtension(fileName);
                            string extPart = Path.GetExtension(fileName);
                            int dup = 1;
                            while (!usedNames.Add($"{basePart}_{dup}{extPart}"))
                                dup++;
                            fileName = $"{basePart}_{dup}{extPart}";
                        }

                        File.WriteAllBytes(Path.Combine(packDir, fileName), data);
                        filesWritten++;
                    }
                    packsWithModels++;
                }

                // Free pack data immediately
                packFileData.Clear();
                loader.ClearPackCache();
            }
            catch (Exception ex)
            {
                log.WriteLine($"  ERR pack {packIdx} '{packName}': {ex.Message}");
            }
        }

        log.WriteLine($"Scan completed at {DateTime.Now}");

        Console.WriteLine($"\r  Done.                                                                      ");
        Console.WriteLine($"\n  Results:");
        Console.WriteLine($"    Packs scanned:   {totalPacks}");
        Console.WriteLine($"    Models found:    {modelsFound}");
        Console.WriteLine($"    Packs with models: {packsWithModels}");
        Console.WriteLine($"    Files written:   {filesWritten}");
        Console.WriteLine($"    Output:          {outputDir}");

        return 0;
    }

    /// <summary>
    /// Detect file type by inspecting content bytes.
    /// </summary>
    private static string DetectFileExtension(byte[] data)
    {
        if (data.Length < 4) return ".bin";

        // BNTX magic
        if (data.Length >= 4 && data[0] == 'B' && data[1] == 'N' && data[2] == 'T' && data[3] == 'X')
            return ".bntx";

        // Try FlatBuffer types
        try
        {
            var mdl = MiniToolbox.Core.Utils.FlatBufferConverter.DeserializeFrom<
                MiniToolbox.Trpak.Flatbuffers.TR.Model.TRMDL>(data);
            if (mdl?.Meshes?.Length > 0 || mdl?.Skeleton != null)
                return ".trmdl";
        }
        catch { }

        try
        {
            var msh = MiniToolbox.Core.Utils.FlatBufferConverter.DeserializeFrom<
                MiniToolbox.Trpak.Flatbuffers.TR.Model.TRMSH>(data);
            if (msh?.bufferFilePath != null)
                return ".trmsh";
        }
        catch { }

        try
        {
            var skl = MiniToolbox.Core.Utils.FlatBufferConverter.DeserializeFrom<
                MiniToolbox.Trpak.Flatbuffers.TR.Model.TRSKL>(data);
            if (skl?.TransformNodes?.Length > 0)
                return ".trskl";
        }
        catch { }

        try
        {
            var mtr = MiniToolbox.Core.Utils.FlatBufferConverter.DeserializeFrom<
                MiniToolbox.Trpak.Flatbuffers.TR.Model.TRMTR>(data);
            if (mtr?.Materials?.Length > 0)
                return ".trmtr";
        }
        catch { }

        return ".bin";
    }
}
