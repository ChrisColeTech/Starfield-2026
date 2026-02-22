using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DrpToDae.Formats.DRP;
using DrpToDae.Formats.NUD;
using DrpToDae.Formats.VBN;
using DrpToDae.Formats.Collada;
using DrpToDae.Formats.Animation;
using DrpToDae.IO;
using VbnSkeleton = DrpToDae.Formats.VBN.VBN;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("DRP to DAE Converter");
        Console.WriteLine("====================");

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        // Parse global options (-n N, --baked)
        int maxCount = int.MaxValue;
        bool bakedMode = false;
        var filteredArgs = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "-n" || args[i] == "--number") && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int n) && n > 0)
                    maxCount = n;
                else
                {
                    Console.WriteLine($"Error: invalid number '{args[i + 1]}'");
                    return 1;
                }
                i++; // skip the number value
            }
            else if (args[i] == "--baked")
            {
                bakedMode = true;
            }
            else
            {
                filteredArgs.Add(args[i]);
            }
        }

        if (filteredArgs.Count == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = filteredArgs[0].ToLower();

        try
        {
            switch (command)
            {
                case "--analyze":
                case "-a":
                    if (filteredArgs.Count < 2) { Console.WriteLine("Error: --analyze requires a path"); return 1; }
                    AnalyzePath(filteredArgs[1], filteredArgs.Count > 2 ? filteredArgs[2] : null);
                    break;

                case "--extract-raw":
                case "-x":
                    if (filteredArgs.Count < 2) { Console.WriteLine("Error: --extract-raw requires a path"); return 1; }
                    ExtractRawPath(filteredArgs[1], filteredArgs.Count > 2 ? filteredArgs[2] : null);
                    break;

                case "--batch":
                case "-b":
                    if (filteredArgs.Count < 3) { Console.WriteLine("Error: --batch requires chrdep_path chrmhd_path [output]"); return 1; }
                    BatchExport(filteredArgs[1], filteredArgs[2], filteredArgs.Count > 3 ? filteredArgs[3] : null, maxCount, bakedMode);
                    break;

                case "--test-baked":
                case "-tb":
                    if (filteredArgs.Count < 3) { Console.WriteLine("Error: --test-baked requires chrdep_path chrmhd_path [output]"); return 1; }
                    TestBakedExport(filteredArgs[1], filteredArgs[2], filteredArgs.Count > 3 ? filteredArgs[3] : null, maxCount);
                    break;

                case "--debug-skeleton":
                case "-ds":
                    if (filteredArgs.Count < 2) { Console.WriteLine("Error: --debug-skeleton requires a VBN path"); return 1; }
                    DebugSkeleton(filteredArgs[1]);
                    break;

                case "--batch-variations":
                case "-bv":
                    if (filteredArgs.Count < 3) { Console.WriteLine("Error: --batch-variations requires chrdep_path chrind_path [output]"); return 1; }
                    BatchVariations(filteredArgs[1], filteredArgs[2], filteredArgs.Count > 3 ? filteredArgs[3] : null);
                    break;

                default:
                    if (Directory.Exists(filteredArgs[0]))
                        ProcessDirectory(filteredArgs[0], filteredArgs.Count > 1 ? filteredArgs[1] : null, maxCount);
                    else
                        ProcessFile(filteredArgs[0], filteredArgs.Count > 1 ? filteredArgs[1] : null);
                    break;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: drp-to-dae [mode] <input> [output] [options]");
        Console.WriteLine();
        Console.WriteLine("Modes:");
        Console.WriteLine("  (default)        Convert single DRP file or directory");
        Console.WriteLine("  --batch          Batch convert chrdep + chrmhd folders together");
        Console.WriteLine("  --analyze        Scan DRP files and output contents report");
        Console.WriteLine("  --extract-raw    Extract raw decrypted files for analysis");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -n, --number N   Limit number of files to export");
        Console.WriteLine("  --baked          Export full model+animation per clip (works in Blender directly)");
        Console.WriteLine("                   Default: split mode (model.dae + separate clip DAEs)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  drp-to-dae model.drp ./output/");
        Console.WriteLine("  drp-to-dae ./chrdep/master ./output/");
        Console.WriteLine("  drp-to-dae ./chrdep/master ./output/ -n 5");
        Console.WriteLine("  drp-to-dae --batch ./chrdep/master ./chrmhd/master ./output/");
        Console.WriteLine("  drp-to-dae --batch ./chrdep/master ./chrmhd/master ./output/ --baked");
    }

    static void BatchExport(string chrdepPath, string chrmhdPath, string? outputPath, int maxCount = int.MaxValue, bool bakedMode = false)
    {
        Console.WriteLine("Batch Export Mode");
        Console.WriteLine("=================");
        Console.WriteLine($"Models:    {chrdepPath}");
        Console.WriteLine($"Animations: {chrmhdPath}");
        Console.WriteLine($"Output:    {outputPath ?? chrdepPath}");
        Console.WriteLine($"Mode:      {(bakedMode ? "BAKED (model+anim per clip)" : "SPLIT (model.dae + clip DAEs)")}");

        // Auto-detect chrind path (sibling of chrdep — contains VBN skeletons)
        string? chrindPath = null;
        string? parentDir = Path.GetDirectoryName(chrdepPath);
        if (parentDir != null)
        {
            string grandParent = Path.GetDirectoryName(parentDir) ?? "";
            string subDir = Path.GetFileName(chrdepPath);
            string candidate = Path.Combine(grandParent, "chrind", subDir);
            if (Directory.Exists(candidate))
                chrindPath = candidate;
        }
        Console.WriteLine($"Skeletons: {chrindPath ?? "(not found — will use model DRP)"}");
        Console.WriteLine();

        outputPath ??= chrdepPath;
        Directory.CreateDirectory(outputPath);

        var modelFiles = Directory.GetFiles(chrdepPath, "*.drp", SearchOption.AllDirectories);
        Console.WriteLine($"Found {modelFiles.Length} model DRP files");

        // Group model DRPs by character ID, pick _000 as the primary variant
        var charGroups = new Dictionary<string, string>();
        foreach (var modelFile in modelFiles)
        {
            string charId = ExtractCharacterId(Path.GetFileNameWithoutExtension(modelFile));
            if (string.IsNullOrEmpty(charId)) continue;
            if (!charGroups.ContainsKey(charId) || modelFile.Contains("_000"))
                charGroups[charId] = modelFile;
        }

        Console.WriteLine($"Found {charGroups.Count} unique characters");

        // Apply --max limit
        var charList = charGroups.Take(maxCount).ToList();
        int total = charList.Count;
        int processed = 0;

        int parallelism = Math.Min(Environment.ProcessorCount, 8);
        Console.WriteLine($"Processing {total} characters with parallelism={parallelism}...\n");

        Parallel.ForEach(charList,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            kvp =>
            {
                var (charId, modelFile) = (kvp.Key, kvp.Value);

                // Find skeleton DRP from chrind
                string? skeletonDrp = null;
                if (chrindPath != null)
                {
                    var indFiles = Directory.GetFiles(chrindPath, $"*{charId}*_000.drp", SearchOption.AllDirectories);
                    if (indFiles.Length > 0)
                        skeletonDrp = indFiles[0];
                }

                // Find ALL animation DRPs for this character
                var animFiles = FindAnimationFiles(chrmhdPath, charId);

                string charFolder = Path.Combine(outputPath, charId);

                int n = Interlocked.Increment(ref processed);
                Console.WriteLine($"[{n}/{total}] {charId} — model: {Path.GetFileName(modelFile)}, anims: {animFiles.Length}");

                try
                {
                    ProcessCharacterComplete(modelFile, skeletonDrp, animFiles, charFolder, bakedMode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR [{charId}]: {ex.Message}");
                }
            });

        Console.WriteLine($"\n\nComplete: {processed} characters processed");
    }

    static string ExtractCharacterId(string filename)
    {
        // Extract the character ID portion: e.g. "depp006_000" → "p006", "depa038_000" → "a038"
        var match = Regex.Match(filename, @"dep([a-z]?\d+)_");
        return match.Success ? match.Groups[1].Value : "";
    }

    static string[] FindAnimationFiles(string chrmhdPath, string charId)
    {
        return Directory.GetFiles(chrmhdPath, $"*{charId}*.drp", SearchOption.AllDirectories);
    }

    /// <summary>
    /// Legacy single-file mode: processes one model DRP + optional single animation DRP.
    /// </summary>
    static void ProcessCharacter(string modelPath, string? animPath, string outputDir)
    {
        ProcessCharacterComplete(modelPath, null, animPath != null ? new[] { animPath } : Array.Empty<string>(), outputDir, false);
    }

    /// <summary>
    /// Main pipeline: processes one model DRP + optional skeleton DRP + multiple animation DRPs.
    /// Split mode: model.dae + textures/ + clips/clip_NNN.dae + manifest.json
    /// Baked mode: textures/ + baked/clip_NNN.dae (each with full model+anim) + manifest.json
    /// </summary>
    static void ProcessCharacterComplete(string modelPath, string? skeletonDrpPath, string[] animPaths, string outputDir, bool bakedMode)
    {
        Directory.CreateDirectory(outputDir);

        string texturesDir = Path.Combine(outputDir, "textures");
        string clipsDir = Path.Combine(outputDir, bakedMode ? "baked" : "clips");

        NUD? nud = null;
        VbnSkeleton? vbn = null;
        int textureCount = 0;
        int clipIndex = 0;
        var clipInfos = new List<ClipInfo>();
        var textureInfos = new List<string>();
        var collectedOmoFiles = new List<string>();

        string tempDir = Path.Combine(Path.GetTempPath(), $"drp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // === Step 0: Extract skeleton from chrind DRP if available ===
            if (skeletonDrpPath != null)
            {
                Console.WriteLine("  [0/3] Extracting skeleton...");
                ExtractFromDrp(skeletonDrpPath, tempDir, ref nud, ref vbn, ref textureCount, texturesDir, collectedOmoFiles, textureInfos);
                // We only want the VBN from this, clear any NUD it may have picked up
                nud = null;
                Console.WriteLine($"       Skeleton: {(vbn != null ? $"{vbn.Bones.Count} bones" : "not found")}");
            }

            // === Step 1: Extract model, textures, and skeleton from model DRP ===
            Console.WriteLine("  [1/3] Processing model...");
            ExtractFromDrp(modelPath, tempDir, ref nud, ref vbn, ref textureCount, texturesDir, collectedOmoFiles, textureInfos);

            // Export any OMOs collected during skeleton/model extraction (with baked mode support)
            if (collectedOmoFiles.Count > 0)
            {
                ExportCollectedOmos(collectedOmoFiles, clipsDir, ref clipIndex, clipInfos, vbn, nud, bakedMode);
            }

            // === Step 2: Export model DAE (split mode only) ===
            if (!bakedMode)
            {
                Console.WriteLine("  [2/3] Exporting model DAE...");
                if (nud != null)
                {
                    string daePath = Path.Combine(outputDir, "model.dae");
                    ColladaExporter.Export(daePath, nud, vbn);
                    Console.WriteLine($"       Saved: {daePath}");
                }
                else
                {
                    Console.WriteLine("       No model found in DRP");
                }
            }
            else
            {
                Console.WriteLine("  [2/3] Skipping model.dae (baked mode)...");
                if (nud == null)
                {
                    Console.WriteLine("       WARNING: No model found in DRP - baked clips will have no geometry");
                }

                // Copy textures into baked folder so DAEs can reference textures/ directly
                string bakedTexturesDir = Path.Combine(clipsDir, "textures");
                if (Directory.Exists(texturesDir))
                {
                    Directory.CreateDirectory(bakedTexturesDir);
                    foreach (var texFile in Directory.GetFiles(texturesDir, "*.png"))
                    {
                        string destFile = Path.Combine(bakedTexturesDir, Path.GetFileName(texFile));
                        File.Copy(texFile, destFile, overwrite: true);
                    }
                }
            }

            // === Step 3: Process each animation DRP → one DAE per clip ===
            Console.WriteLine($"  [3/3] Processing {animPaths.Length} animation DRP(s)...");
            foreach (var animPath in animPaths)
            {
                try
                {
                    string clipName = Path.GetFileNameWithoutExtension(animPath);
                    byte[]? bcaData = null;
                    byte[]? bclData = null;
                    VbnSkeleton? animVbn = null;

                    // Extract BCA + BCL from this animation DRP
                    byte[] decryptedData = DecryptIfNeeded(animPath);
                    string decryptedPath = Path.Combine(tempDir, Path.GetFileName(animPath));
                    File.WriteAllBytes(decryptedPath, decryptedData);

                    var drpFile = new DRPFile(decryptedPath);
                    var extractedFiles = drpFile.ExtractFiles();

                    foreach (var kvp in extractedFiles)
                    {
                        byte[] data = DecompressIfNeeded(kvp.Value);
                        if (data.Length < 4) continue;

                        string magic = Encoding.ASCII.GetString(data, 0, 4);
                        string tempFile = Path.Combine(tempDir, $"{clipName}_{kvp.Key}");
                        Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);
                        File.WriteAllBytes(tempFile, data);

                        switch (magic)
                        {
                            case "BCA ":
                                bcaData = data;
                                break;
                            case "BCL ":
                                bclData = data;
                                break;
                            case "VBN ":
                            case " NBV":
                                animVbn ??= new VbnSkeleton(tempFile);
                                break;
                            case "OMO ":
                                Directory.CreateDirectory(clipsDir);
                                string omoClipFile = $"clip_{clipIndex:D3}.dae";
                                string omoClipPath = Path.Combine(clipsDir, omoClipFile);
                                var omoAnim = OMOReader.Read(tempFile);
                                if (vbn != null) ResolveBoneNames(omoAnim, vbn);
                                if (bakedMode && nud != null)
                                {
                                    ColladaExporter.ExportWithAnimation(omoClipPath, nud, vbn, omoAnim);
                                    Console.WriteLine($"       {clipName} (OMO+MODEL): {omoAnim.Bones.Count} bones → {omoClipFile} [BAKED]");
                                }
                                else
                                {
                                    AnimationExporter.ExportToCollada(omoAnim, omoClipPath, vbn);
                                    Console.WriteLine($"       {clipName} (OMO): {omoAnim.Bones.Count} bones → {omoClipFile}");
                                }
                                string omoClipDir = bakedMode ? "baked" : "clips";
                                clipInfos.Add(new ClipInfo(clipIndex, clipName, $"{omoClipDir}/{omoClipFile}", omoAnim.FrameCount, 30));
                                clipIndex++;
                                break;
                        }
                    }

                    // Use the animation DRP's VBN if present, otherwise fall back to model VBN
                    var skeleton = animVbn ?? vbn;

                    if (bcaData != null && bclData != null && skeleton != null)
                    {
                        Directory.CreateDirectory(clipsDir);
                        var bcaAnim = BCAReader.Read(bcaData, bclData, skeleton);
                        string bcaClipFile = $"clip_{clipIndex:D3}.dae";
                        string bcaClipPath = Path.Combine(clipsDir, bcaClipFile);
                        if (bakedMode && nud != null)
                        {
                            ColladaExporter.ExportWithAnimation(bcaClipPath, nud, skeleton, bcaAnim);
                            Console.WriteLine($"       {clipName} (BCA+MODEL): {bcaAnim.Bones.Count} bones → {bcaClipFile} [BAKED]");
                        }
                        else
                        {
                            AnimationExporter.ExportToCollada(bcaAnim, bcaClipPath, skeleton);
                            Console.WriteLine($"       {clipName} (BCA): {bcaAnim.Bones.Count} bones → {bcaClipFile}");
                        }
                        string bcaClipDir = bakedMode ? "baked" : "clips";
                        clipInfos.Add(new ClipInfo(clipIndex, clipName, $"{bcaClipDir}/{bcaClipFile}", bcaAnim.FrameCount, 30));
                        clipIndex++;
                    }
                    else if (bcaData != null || bclData != null)
                    {
                        string missing = bcaData == null ? "BCA" : bclData == null ? "BCL" : "VBN";
                        Console.WriteLine($"       {clipName}: skip (missing {missing})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"       {Path.GetFileName(animPath)}: error — {ex.Message}");
                }
            }

            // === Step 4: Write manifest.json ===
            WriteManifest(outputDir, Path.GetFileName(outputDir), nud, vbn, textureInfos, clipInfos);

            Console.WriteLine($"\n  Summary: model={nud != null}, textures={textureCount}, clips={clipInfos.Count}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    /// <summary>
    /// Extracts model, textures, skeleton from a single DRP file.
    /// OMO files are collected but not exported - they should be exported later with ExportCollectedOmos.
    /// </summary>
    static void ExtractFromDrp(string drpPath, string tempDir,
        ref NUD? nud, ref VbnSkeleton? vbn, ref int textureCount, string texturesDir,
        List<string> collectedOmoFiles, List<string> textureInfos)
    {
        byte[] decryptedData = DecryptIfNeeded(drpPath);
        string decryptedPath = Path.Combine(tempDir, Path.GetFileName(drpPath));
        File.WriteAllBytes(decryptedPath, decryptedData);

        var drpFile = new DRPFile(decryptedPath);
        var extractedFiles = drpFile.ExtractFiles();

        // Two-pass: first resolve VBN/NUD/textures, then export OMOs with the resolved skeleton
        var omoFiles = new List<string>();

        foreach (var kvp in extractedFiles)
        {
            byte[] data = DecompressIfNeeded(kvp.Value);
            if (data.Length < 4) continue;

            string magic = Encoding.ASCII.GetString(data, 0, 4);
            string tempFile = Path.Combine(tempDir, kvp.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);
            File.WriteAllBytes(tempFile, data);

            try
            {
                switch (magic)
                {
                    case "NDP3":
                    case "NDWD":
                        nud ??= new NUD(tempFile);
                        Console.WriteLine($"       Model: {kvp.Key}");
                        break;

                    case "NTP3":
                    case "NTWD":
                    case "NTWU":
                        Directory.CreateDirectory(texturesDir);
                        textureCount += NutExporter.ExportTextures(tempFile, texturesDir, out var exportedTextures);
                        textureInfos.AddRange(exportedTextures);
                        break;

                    case "VBN ":
                    case " NBV":
                        vbn ??= new VbnSkeleton(tempFile);
                        Console.WriteLine($"       Skeleton: {kvp.Key}");
                        break;

                    case "OMO ":
                        omoFiles.Add(tempFile);
                        collectedOmoFiles.Add(tempFile);
                        Console.WriteLine($"       Animation (OMO): {Path.GetFileNameWithoutExtension(kvp.Key)}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"       Skip {magic}: {ex.Message}");
            }
        }

        // Note: OMOs are collected in collectedOmoFiles and exported later via ExportCollectedOmos
    }

    /// <summary>
    /// Export all collected OMO files, with baked mode support.
    /// </summary>
    static void ExportCollectedOmos(List<string> omoFiles, string clipsDir, ref int clipIndex,
        List<ClipInfo> clipInfos, VbnSkeleton? vbn, NUD? nud, bool bakedMode)
    {
        if (omoFiles.Count == 0) return;

        Directory.CreateDirectory(clipsDir);
        string clipDirName = bakedMode ? "baked" : "clips";

        foreach (var omoFile in omoFiles)
        {
            try
            {
                string clipName = Path.GetFileNameWithoutExtension(omoFile);
                string clipFile = $"clip_{clipIndex:D3}.dae";
                string clipPath = Path.Combine(clipsDir, clipFile);
                var omoAnim = OMOReader.Read(omoFile);
                if (vbn != null) ResolveBoneNames(omoAnim, vbn);

                if (bakedMode && nud != null)
                {
                    ColladaExporter.ExportWithAnimation(clipPath, nud, vbn, omoAnim);
                    Console.WriteLine($"       {clipName} (OMO+MODEL): {omoAnim.Bones.Count} bones → {clipFile} [BAKED]");
                }
                else
                {
                    AnimationExporter.ExportToCollada(omoAnim, clipPath, vbn);
                    Console.WriteLine($"       {clipName} (OMO): {omoAnim.Bones.Count} bones → {clipFile}");
                }

                clipInfos.Add(new ClipInfo(clipIndex, clipName, $"{clipDirName}/{clipFile}", omoAnim.FrameCount, 30));
                clipIndex++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"       Skip OMO {Path.GetFileName(omoFile)}: {ex.Message}");
            }
        }
    }

    static byte[] DecryptIfNeeded(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 0x20) return data;

        int fileCount = (data[0x16] << 8) | data[0x17];
        if (fileCount <= 0 || fileCount > 1000)
            return DrpDecrypter.Decrypt(path);
        
        return data;
    }

    static byte[] DecompressIfNeeded(byte[] data)
    {
        try { return Util.DeCompress(data); }
        catch { return data; }
    }

    static void AnalyzePath(string path, string? reportPath)
    {
        if (Directory.Exists(path))
            AnalyzeDirectory(path, reportPath);
        else
            AnalyzeFile(path);
    }

    static void AnalyzeDirectory(string dir, string? reportPath)
    {
        var files = Directory.GetFiles(dir, "*.drp", SearchOption.AllDirectories);
        Console.WriteLine($"Analyzing {files.Length} DRP files...\n");

        var report = new List<(string File, bool Model, bool Tex, bool Skel, bool Anim)>();

        foreach (var file in files)
        {
            var entry = AnalyzeDrpFile(file);
            report.Add((Path.GetFileName(file), entry.HasModel, entry.HasTextures, entry.HasSkeleton, entry.HasAnimations));
        }

        Console.WriteLine($"\nModels: {report.Count(r => r.Model)}");
        Console.WriteLine($"Textures: {report.Count(r => r.Tex)}");
        Console.WriteLine($"Skeletons: {report.Count(r => r.Skel)}");
        Console.WriteLine($"Animations: {report.Count(r => r.Anim)}");

        if (!string.IsNullOrEmpty(reportPath))
        {
            File.WriteAllLines(reportPath, report.Select(r => $"{r.File},{r.Model},{r.Tex},{r.Skel},{r.Anim}"));
            Console.WriteLine($"\nReport: {reportPath}");
        }
    }

    static (bool HasModel, bool HasTextures, bool HasSkeleton, bool HasAnimations) AnalyzeDrpFile(string path)
    {
        bool hasModel = false, hasTex = false, hasSkel = false, hasAnim = false;

        string tempDir = Path.Combine(Path.GetTempPath(), $"analyze-{Guid.NewGuid():N}");
        try
        {
            byte[] data = DecryptIfNeeded(path);
            string tempPath = Path.Combine(tempDir, "temp.drp");
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(tempPath, data);

            var drp = new DRPFile(tempPath);
            foreach (var kvp in drp.ExtractFiles())
            {
                byte[] dec = DecompressIfNeeded(kvp.Value);
                if (dec.Length < 4) continue;
                string m = Encoding.ASCII.GetString(dec, 0, 4);

                if (m == "NDP3" || m == "NDWD") hasModel = true;
                else if (m == "NTP3" || m == "NTWD" || m == "NTWU") hasTex = true;
                else if (m == "VBN " || m == " NBV") hasSkel = true;
                else if (m == "OMO " || m == "BCA " || m == "BCL " || m == "BCH " || m == "BCS ") hasAnim = true;
            }

            Console.WriteLine($"{Path.GetFileName(path)}: M={hasModel} T={hasTex} S={hasSkel} A={hasAnim}");
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }

        return (hasModel, hasTex, hasSkel, hasAnim);
    }

    static void AnalyzeFile(string path)
    {
        Console.WriteLine($"Analyzing: {path}");
        var result = AnalyzeDrpFile(path);
        Console.WriteLine($"\nModel: {result.HasModel}");
        Console.WriteLine($"Textures: {result.HasTextures}");
        Console.WriteLine($"Skeleton: {result.HasSkeleton}");
        Console.WriteLine($"Animations: {result.HasAnimations}");
    }

    static void ExtractRawPath(string path, string? outputPath)
    {
        if (Directory.Exists(path))
        {
            outputPath ??= Path.Combine(path, "extracted_raw");
            foreach (var file in Directory.GetFiles(path, "*.drp", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(path, file);
                string outDir = Path.Combine(outputPath, Path.GetDirectoryName(rel) ?? "", Path.GetFileNameWithoutExtension(file));
                ExtractRawFile(file, outDir);
            }
        }
        else
        {
            ExtractRawFile(path, outputPath);
        }
    }

    static void ExtractRawFile(string path, string? outputPath)
    {
        Console.WriteLine($"Extracting: {path}");
        outputPath ??= Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "_raw");
        Directory.CreateDirectory(outputPath);

        string tempDir = Path.Combine(Path.GetTempPath(), $"extract-{Guid.NewGuid():N}");
        try
        {
            byte[] data = DecryptIfNeeded(path);
            string tempPath = Path.Combine(tempDir, "temp.drp");
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(tempPath, data);

            var drp = new DRPFile(tempPath);
            int count = 0;
            var magics = new Dictionary<string, int>();

            foreach (var kvp in drp.ExtractFiles())
            {
                byte[] dec = DecompressIfNeeded(kvp.Value);
                if (dec.Length < 4) continue;

                string magic = Encoding.ASCII.GetString(dec, 0, 4);
                string safe = new string(magic.Select(c => c >= 32 && c < 127 ? c : '_').ToArray());
                if (!magics.ContainsKey(safe)) magics[safe] = 0;
                int idx = magics[safe]++;

                string outFile = $"{safe}_{idx}_{Sanitize(kvp.Key)}.raw";
                File.WriteAllBytes(Path.Combine(outputPath, outFile), dec);
                count++;
            }

            Console.WriteLine($"  Extracted {count} files");
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    static void ProcessDirectory(string inputDir, string? outputDir, int maxCount = int.MaxValue)
    {
        outputDir ??= inputDir;
        int count = 0;
        foreach (var file in Directory.GetFiles(inputDir, "*.drp", SearchOption.AllDirectories))
        {
            if (count >= maxCount)
            {
                Console.WriteLine($"\nReached limit of {maxCount} files");
                break;
            }

            string rel = Path.GetRelativePath(inputDir, file);
            string outDir = Path.Combine(outputDir, Path.GetDirectoryName(rel) ?? "", Path.GetFileNameWithoutExtension(file));
            ProcessFile(file, outDir);
            count++;
            GC.Collect();
        }
    }

    static void ProcessFile(string drpPath, string? outputDir)
    {
        Console.WriteLine($"Processing: {drpPath}");
        ProcessCharacter(drpPath, null, outputDir ?? Path.GetDirectoryName(drpPath) ?? ".");
    }

    static void ResolveBoneNames(AnimationData animation, VbnSkeleton skeleton)
    {
        var idToName = new Dictionary<uint, string>();
        foreach (var bone in skeleton.Bones)
            idToName[bone.BoneId] = bone.Name;

        foreach (var bone in animation.Bones)
        {
            if (bone.Hash != -1 && idToName.TryGetValue((uint)bone.Hash, out string? name))
                bone.Name = name;
        }
    }

    static void WriteManifest(string outputDir, string characterId, NUD? nud, VbnSkeleton? vbn,
        List<string> textureFiles, List<ClipInfo> clips)
    {
        var manifest = new
        {
            version = 1,
            characterId = characterId,
            model = nud != null ? new
            {
                file = "model.dae",
                meshCount = nud.Meshes.Sum(m => m.Polygons.Count),
                boneCount = vbn?.Bones.Count ?? 0
            } : null,
            textures = textureFiles.Distinct().Select(t => new { file = t }).ToArray(),
            clips = clips.Select(c => new
            {
                index = c.Index,
                name = c.Name,
                file = c.File,
                frameCount = c.FrameCount,
                fps = c.Fps
            }).ToArray()
        };

        string json = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        File.WriteAllText(Path.Combine(outputDir, "manifest.json"), json);
    }

    /// <summary>
    /// Batch export with multiple matrix format variations for testing
    /// </summary>
    static void BatchVariations(string chrdepPath, string chrindPath, string? outputPath)
    {
        Console.WriteLine("Batch Variations Export");
        Console.WriteLine("=======================");
        Console.WriteLine($"Model: {chrdepPath}");
        Console.WriteLine($"Skeleton: {chrindPath}");
        outputPath ??= "./batch-variations";
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine();

        Directory.CreateDirectory(outputPath);

        NUD? nud = null;
        VbnSkeleton? vbn = null;
        string tempDir = Path.Combine(Path.GetTempPath(), $"batch-var-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Extract skeleton from chrind
            var skelFiles = Directory.GetFiles(chrindPath, "*_000.drp", SearchOption.AllDirectories);
            if (skelFiles.Length > 0)
            {
                int dummy = 0;
                var dummyOmo = new List<string>();
                var dummyTex = new List<string>();
                ExtractFromDrp(skelFiles[0], tempDir, ref nud, ref vbn, ref dummy, tempDir, dummyOmo, dummyTex);
                nud = null;
                Console.WriteLine($"Skeleton: {vbn?.Bones.Count ?? 0} bones");
            }

            // Extract model from chrdep
            var modelFiles = Directory.GetFiles(chrdepPath, "*_000.drp", SearchOption.AllDirectories);
            if (modelFiles.Length > 0)
            {
                int texCount = 0;
                var dummyOmo = new List<string>();
                var dummyTex = new List<string>();
                ExtractFromDrp(modelFiles[0], tempDir, ref nud, ref vbn, ref texCount, tempDir, dummyOmo, dummyTex);
                Console.WriteLine($"Model: {nud?.Meshes.Count ?? 0} meshes");
            }

            if (nud == null)
            {
                Console.WriteLine("Error: No model found");
                return;
            }

            if (vbn == null)
            {
                Console.WriteLine("Error: No skeleton found");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Exporting variations...");
            BakedExporter.ExportBatchVariations(outputPath, nud, vbn);

            Console.WriteLine();
            Console.WriteLine("Exported files:");
            foreach (var file in Directory.GetFiles(outputPath, "*.dae"))
            {
                Console.WriteLine($"  {Path.GetFileName(file)}");
            }

            Console.WriteLine();
            Console.WriteLine("Done! Test each file in Blender to find the correct format.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Debug skeleton data - dump raw bone transforms for verification
    /// </summary>
    static void DebugSkeleton(string chrindPath)
    {
        Console.WriteLine("Debug Skeleton");
        Console.WriteLine("==============");
        Console.WriteLine($"Path: {chrindPath}");
        Console.WriteLine();

        // Find first DRP file containing VBN
        var drpFiles = Directory.GetFiles(chrindPath, "*.drp", SearchOption.AllDirectories);
        if (drpFiles.Length == 0)
        {
            Console.WriteLine("Error: No DRP files found");
            return;
        }

        VbnSkeleton? vbn = null;
        NUD? nud = null;
        string tempDir = Path.Combine(Path.GetTempPath(), $"debug-skel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var drpFile in drpFiles.Take(1))
            {
                Console.WriteLine($"Processing: {Path.GetFileName(drpFile)}");
                int dummy = 0;
                var dummyOmo = new List<string>();
                var dummyTex = new List<string>();
                ExtractFromDrp(drpFile, tempDir, ref nud, ref vbn, ref dummy, tempDir, dummyOmo, dummyTex);
                if (vbn != null) break;
            }

            if (vbn == null)
            {
                Console.WriteLine("Error: No VBN skeleton found");
                return;
            }

            Console.WriteLine($"Bone count: {vbn.Bones.Count}");
            Console.WriteLine();

            // Print first 10 bones with details
            int count = Math.Min(10, vbn.Bones.Count);
            for (int i = 0; i < count; i++)
            {
                var bone = vbn.Bones[i];
                Console.WriteLine($"Bone {i}: {bone.Name}");
                Console.WriteLine($"  Parent: {bone.ParentIndex}");
                Console.WriteLine($"  Position: ({bone.Position[0]:F6}, {bone.Position[1]:F6}, {bone.Position[2]:F6})");
                Console.WriteLine($"  Rotation (Euler): ({bone.Rotation[0]:F6}, {bone.Rotation[1]:F6}, {bone.Rotation[2]:F6})");
                Console.WriteLine($"  Quaternion: ({bone.Rot.X:F6}, {bone.Rot.Y:F6}, {bone.Rot.Z:F6}, {bone.Rot.W:F6})");
                Console.WriteLine($"  Scale: ({bone.Scale[0]:F4}, {bone.Scale[1]:F4}, {bone.Scale[2]:F4})");

                // Compute and print the local TRS matrix
                var scale = System.Numerics.Matrix4x4.CreateScale(bone.Sca);
                var rotation = System.Numerics.Matrix4x4.CreateFromQuaternion(bone.Rot);
                var translation = System.Numerics.Matrix4x4.CreateTranslation(bone.Pos);
                var local = scale * rotation * translation;

                Console.WriteLine($"  Local Matrix (S*R*T):");
                Console.WriteLine($"    [{local.M11:F6}, {local.M12:F6}, {local.M13:F6}, {local.M14:F6}]");
                Console.WriteLine($"    [{local.M21:F6}, {local.M22:F6}, {local.M23:F6}, {local.M24:F6}]");
                Console.WriteLine($"    [{local.M31:F6}, {local.M32:F6}, {local.M33:F6}, {local.M34:F6}]");
                Console.WriteLine($"    [{local.M41:F6}, {local.M42:F6}, {local.M43:F6}, {local.M44:F6}]");
                Console.WriteLine();
            }

            Console.WriteLine("Done.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Test the new BakedExporter in phases.
    /// Phase 1: model + skeleton only (no animation)
    /// Phase 2: model + skeleton + bind pose animation
    /// Phase 3: model + skeleton + full animation
    /// </summary>
    static void TestBakedExport(string chrdepPath, string chrmhdPath, string? outputPath, int maxCount = 1)
    {
        Console.WriteLine("Test Baked Export");
        Console.WriteLine("=================");
        Console.WriteLine($"Models: {chrdepPath}");
        Console.WriteLine($"Animations: {chrmhdPath}");
        outputPath ??= "./test-baked-output";
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine();

        // Find chrind path (sibling of chrdep containing VBN skeletons)
        string? chrindPath = null;
        string normalizedPath = chrdepPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? parentDir = Path.GetDirectoryName(normalizedPath);
        if (parentDir != null)
        {
            string grandParent = Path.GetDirectoryName(parentDir) ?? "";
            string subDir = Path.GetFileName(normalizedPath);  // e.g. "master"
            string candidate = Path.Combine(grandParent, "chrind", subDir);
            if (Directory.Exists(candidate))
                chrindPath = candidate;
        }
        Console.WriteLine($"Chrind: {chrindPath ?? "(not found)"}");
        Console.WriteLine();

        var modelFiles = Directory.GetFiles(chrdepPath, "*.drp", SearchOption.AllDirectories);
        var charGroups = new Dictionary<string, string>();
        foreach (var modelFile in modelFiles)
        {
            string charId = ExtractCharacterId(Path.GetFileNameWithoutExtension(modelFile));
            if (string.IsNullOrEmpty(charId)) continue;
            if (!charGroups.ContainsKey(charId) || modelFile.Contains("_000"))
                charGroups[charId] = modelFile;
        }

        var charList = charGroups.Take(maxCount).ToList();
        Console.WriteLine($"Testing {charList.Count} character(s)...\n");

        foreach (var (charId, modelFile) in charList)
        {
            Console.WriteLine($"=== Character: {charId} ===");

            string charOutput = Path.Combine(outputPath, charId);
            Directory.CreateDirectory(charOutput);

            // Extract model and skeleton
            NUD? nud = null;
            VbnSkeleton? vbn = null;
            AnimationData? firstAnim = null;

            string tempDir = Path.Combine(Path.GetTempPath(), $"drp-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Get skeleton and OMO animations from chrind if available
                var collectedOmoFromChrind = new List<string>();
                if (chrindPath != null)
                {
                    string skelPattern = $"*{charId}*_000.drp";
                    var skelFiles = Directory.GetFiles(chrindPath, skelPattern, SearchOption.AllDirectories);
                    if (skelFiles.Length > 0)
                    {
                        int dummy = 0;
                        var dummyTex = new List<string>();
                        ExtractFromDrp(skelFiles[0], tempDir, ref nud, ref vbn, ref dummy, tempDir, collectedOmoFromChrind, dummyTex);
                        nud = null; // Only want skeleton
                        Console.WriteLine($"  Skeleton: {vbn?.Bones.Count ?? 0} bones from chrind");
                        Console.WriteLine($"  OMO animations in chrind: {collectedOmoFromChrind.Count}");
                    }
                }

                // Extract model
                {
                    int texCount = 0;
                    var dummyOmo = new List<string>();
                    var dummyTex = new List<string>();
                    string texDir = Path.Combine(charOutput, "textures");
                    Directory.CreateDirectory(texDir);
                    ExtractFromDrp(modelFile, tempDir, ref nud, ref vbn, ref texCount, texDir, dummyOmo, dummyTex);
                    Console.WriteLine($"  Model: {nud?.Meshes.Sum(m => m.Polygons.Count) ?? 0} meshes, {texCount} textures");
                    Console.WriteLine($"  Skeleton: {vbn?.Bones.Count ?? 0} bones");
                }

                // Use OMO animations from chrind (already collected during skeleton extraction)
                if (collectedOmoFromChrind.Count > 0)
                {
                    firstAnim = OMOReader.Read(collectedOmoFromChrind[0]);
                    if (vbn != null) ResolveBoneNames(firstAnim, vbn);
                    Console.WriteLine($"  Animation: {firstAnim.FrameCount} frames, {firstAnim.Bones.Count} bones");
                }

                if (nud == null)
                {
                    Console.WriteLine("  ERROR: No model found, skipping");
                    continue;
                }

                // Phase 0: Model only (no skeleton, no skinning)
                Console.WriteLine("\n  Phase 0: Model only (no skeleton)...");
                string phase0Path = Path.Combine(charOutput, "phase0_static.dae");
                BakedExporter.ExportPhase0(phase0Path, nud);
                Console.WriteLine($"    Exported: {phase0Path}");

                // Phase 1: Model + Skeleton only (no animation)
                Console.WriteLine("\n  Phase 1: Model + Skeleton (no animation)...");
                string phase1Path = Path.Combine(charOutput, "phase1_model_only.dae");
                BakedExporter.ExportPhase1(phase1Path, nud, vbn);
                Console.WriteLine($"    Exported: {phase1Path}");

                // Phase 2: Model + Skeleton + Bind Pose Animation (1 frame)
                Console.WriteLine("\n  Phase 2: Model + Skeleton + Bind Pose Animation (1 frame)...");
                string phase2Path = Path.Combine(charOutput, "phase2_bind_pose.dae");
                BakedExporter.ExportPhase2(phase2Path, nud, vbn);
                Console.WriteLine($"    Exported: {phase2Path}");

                // Phase 2 reference: Export with old exporter for comparison
                if (firstAnim != null && vbn != null)
                {
                    Console.WriteLine("\n  Phase 2 Reference (old exporter)...");
                    string refPath = Path.Combine(charOutput, "phase2_reference.dae");
                    ColladaExporter.ExportWithAnimation(refPath, nud, vbn, firstAnim);
                    Console.WriteLine($"    Exported: {refPath}");
                }

                // Phase 3: Model + Skeleton + Full Animation
                if (firstAnim != null)
                {
                    Console.WriteLine($"\n  Phase 3: Model + Skeleton + Full Animation ({firstAnim.FrameCount} frames)...");
                    string phase3Path = Path.Combine(charOutput, "phase3_animated.dae");
                    BakedExporter.ExportPhase3(phase3Path, nud, vbn, firstAnim);
                    Console.WriteLine($"    Exported: {phase3Path}");
                }
                else
                {
                    Console.WriteLine("\n  Phase 3: Skipped (no animation found)");
                }

                Console.WriteLine($"\n  Done: {charOutput}\n");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        Console.WriteLine("Test complete!");
    }
}

record ClipInfo(int Index, string Name, string File, int FrameCount, int Fps);
