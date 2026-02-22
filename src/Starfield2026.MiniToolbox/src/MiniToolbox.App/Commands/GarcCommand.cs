using MiniToolbox.Spica.Formats.CtrH3D;
using MiniToolbox.Spica.Formats.CtrH3D.Model;
using MiniToolbox.Spica.Formats.Generic.COLLADA;
using MiniToolbox.Manifests;
using SpicaCli.Formats;
using System.Drawing.Imaging;

namespace MiniToolbox.App.Commands;

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
        bool dumpMode = false;
        bool bakeTextures = false;
        bool flatMode = false;
        int limit = 0;
        int skip = 0;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--input" or "-i":  inputFile = args[++i]; break;
                case "--output" or "-o": outputDir = args[++i]; break;
                case "--filter":         filter = args[++i]; break;
                case "--format" or "-f": format = args[++i].ToLowerInvariant(); break;
                case "--info":           infoMode = true; break;
                case "--list":           listMode = true; break;
                case "--extract":        extractMode = true; break;
                case "--bake":           bakeTextures = true; break;
                case "--limit" or "-n":  limit = int.Parse(args[++i]); break;
                case "--skip":           skip = int.Parse(args[++i]); break;
                case "--flat":           flatMode = true; break;
                case "--dump":           dumpMode = true; break;
                case "--help" or "-h":   PrintUsage(); return 0;
            }
        }

        if (string.IsNullOrWhiteSpace(inputFile)) { PrintUsage(); return 1; }
        if (!File.Exists(inputFile)) { Console.Error.WriteLine($"ERROR: Not found: {inputFile}"); return 1; }

        if (infoMode) return RunInfo(inputFile);
        if (listMode) return RunList(inputFile, skip, limit);
        if (dumpMode)
        {
            outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "garc_dump");
            return RunDump(inputFile, outputDir, skip, limit);
        }
        if (extractMode)
        {
            outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "garc_export");
            if (flatMode)
                return RunFlatExtract(inputFile, outputDir, format, skip, limit, bakeTextures);
            return RunExtract(inputFile, outputDir, format, filter, skip, limit, bakeTextures);
        }

        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("GARC Extractor - 3DS Pokemon model extraction (Sun/Moon, X/Y)");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  minitoolbox garc -i <garcFile> --info");
        Console.WriteLine("  minitoolbox garc -i <garcFile> --list [--skip N] [-n N]");
        Console.WriteLine("  minitoolbox garc -i <garcFile> --extract -o <dir> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -i, --input    Input GARC file");
        Console.WriteLine("  -o, --output   Output directory (default: ./garc_export)");
        Console.WriteLine("  -n, --limit    Max groups to process (0 = all)");
        Console.WriteLine("  --skip         Skip first N groups");
        Console.WriteLine("  --filter       Only extract matching Pokemon IDs (e.g. pm0025)");
        Console.WriteLine("  --bake         Bake multi-texture materials into single diffuse");
        Console.WriteLine("  --flat         Extract each entry individually (no Pokemon grouping, for maps/terrain)");
        Console.WriteLine("  --dump         Dump raw (decompressed) entry bytes to files");
        Console.WriteLine("  --info         Show GARC file summary");
        Console.WriteLine("  --list         List entries with detected types");
        Console.WriteLine("  --extract      Extract models, textures, and animations");
    }

    // ── Info ──────────────────────────────────────────────────────────────

    private static int RunInfo(string inputFile)
    {
        Console.WriteLine($"Loading GARC: {inputFile}");
        using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read);

        if (!GARC.IsGARC(fs)) { Console.Error.WriteLine("ERROR: Not a valid GARC file."); return 1; }

        var entries = GARC.GetEntries(fs);
        Console.WriteLine($"  Entries: {entries.Length}");

        int sampleCount = Math.Min(entries.Length, 20);
        var typeCounts = new Dictionary<string, int>();

        for (int i = 0; i < sampleCount; i++)
        {
            string type = IdentifyType(Decompress(GARC.ReadEntry(fs, entries[i])));
            typeCounts.TryGetValue(type, out int c);
            typeCounts[type] = c + 1;
        }

        Console.WriteLine($"  Sample ({sampleCount} entries):");
        foreach (var kvp in typeCounts.OrderByDescending(x => x.Value))
            Console.WriteLine($"    {kvp.Key}: {kvp.Value}");

        return 0;
    }

    // ── List ──────────────────────────────────────────────────────────────

    private static int RunList(string inputFile, int skip, int limit)
    {
        Console.WriteLine($"Loading GARC: {inputFile}");
        using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        var entries = GARC.GetEntries(fs);

        int start = Math.Min(skip, entries.Length);
        int end = limit > 0 ? Math.Min(start + limit, entries.Length) : entries.Length;

        Console.WriteLine($"Entries {start}-{end - 1} of {entries.Length}:");
        Console.WriteLine();

        H3DDict<H3DBone> skeleton = null;

        for (int i = start; i < end; i++)
        {
            byte[] data = Decompress(GARC.ReadEntry(fs, entries[i]));
            string suffix;

            try
            {
                using var ms = new MemoryStream(data);
                var h3d = FormatIdentifier.IdentifyAndOpen(ms, skeleton);
                if (h3d != null)
                {
                    var parts = new List<string>();
                    if (h3d.Models.Count > 0)
                    {
                        foreach (var mdl in h3d.Models)
                            parts.Add($"{mdl.Name}({mdl.Meshes.Count}m/{mdl.Skeleton.Count}b)");
                        skeleton ??= h3d.Models[0].Skeleton;
                    }
                    if (h3d.Textures.Count > 0)
                        parts.Add($"{h3d.Textures.Count}tex: {string.Join(", ", h3d.Textures.Select(t => t.Name).Take(3))}");
                    if (h3d.SkeletalAnimations.Count > 0)
                        parts.Add($"{h3d.SkeletalAnimations.Count}anim");
                    suffix = parts.Count > 0 ? string.Join(" | ", parts) : "empty";
                }
                else
                {
                    suffix = IdentifyType(data);
                }
            }
            catch (Exception ex)
            {
                suffix = $"error: {ex.Message}";
            }

            Console.WriteLine($"  [{i:D5}] {data.Length,8}b  {suffix}");
        }

        return 0;
    }

    // ── Extract ───────────────────────────────────────────────────────────

    private static int RunExtract(string inputFile, string outputDir, string format, string? filter,
        int skip, int limit, bool bakeTextures)
    {
        Console.WriteLine($"Loading GARC: {inputFile}");
        Console.WriteLine($"Output: {outputDir}");
        if (!string.IsNullOrEmpty(filter)) Console.WriteLine($"Filter: {filter}");
        Console.WriteLine();

        using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        var entries = GARC.GetEntries(fs);
        Directory.CreateDirectory(outputDir);

        // Phase 1: Quick-classify every entry to build Pokemon groups.
        // Model entries (PC package with GFModel inside) start a new group.
        // Subsequent texture/animation entries belong to that group until the next model.
        Console.WriteLine($"Classifying {entries.Length} entries...");

        var groups = new List<PokemonGroup>();
        PokemonGroup current = null;
        H3DDict<H3DBone> skeleton = null;

        for (int i = 0; i < entries.Length; i++)
        {
            byte[] data = Decompress(GARC.ReadEntry(fs, entries[i]));
            if (data.Length < 4) continue;

            var entryType = ClassifyEntry(data);

            if (entryType == EntryKind.Model)
            {
                // Try to extract Pokemon ID and skeleton from the model
                string pokemonId = null;
                try
                {
                    using var ms = new MemoryStream(data);
                    var h3d = FormatIdentifier.IdentifyAndOpen(ms);
                    if (h3d?.Models.Count > 0)
                    {
                        skeleton ??= h3d.Models[0].Skeleton;
                        pokemonId = ExtractPokemonId(h3d);
                    }
                }
                catch { }

                pokemonId ??= $"entry_{i:D5}";
                current = new PokemonGroup { Id = pokemonId, ModelIndex = i };
                groups.Add(current);
            }
            else if (current != null)
            {
                if (entryType == EntryKind.Texture)
                    current.TextureIndices.Add(i);
                else if (entryType == EntryKind.Animation)
                    current.AnimationIndices.Add(i);
            }
        }

        Console.WriteLine($"Found {groups.Count} Pokemon group(s)");

        // Apply skip/limit
        int gStart = Math.Min(skip, groups.Count);
        int gEnd = limit > 0 ? Math.Min(gStart + limit, groups.Count) : groups.Count;

        int extracted = 0, skipped = 0, failed = 0;

        // Phase 2: Extract each group
        for (int gi = gStart; gi < gEnd; gi++)
        {
            var group = groups[gi];

            if (!string.IsNullOrEmpty(filter) && !group.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            try
            {
                ExtractGroup(fs, entries, group, skeleton, outputDir, format, bakeTextures);
                extracted++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ERROR [{group.Id}]: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"=== Extract complete ===");
        Console.WriteLine($"  Extracted: {extracted}  Skipped: {skipped}  Failed: {failed}");

        return failed > 0 && extracted == 0 ? 1 : 0;
    }

    private static void ExtractGroup(
        FileStream fs, GARC.GARCEntry[] entries, PokemonGroup group,
        H3DDict<H3DBone> skeleton, string outputDir, string format, bool bakeTextures)
    {
        string groupDir = Path.Combine(outputDir, SanitizeName(group.Id));
        Directory.CreateDirectory(groupDir);

        // Load model → H3D scene
        H3D scene = new H3D();

        byte[] modelData = Decompress(GARC.ReadEntry(fs, entries[group.ModelIndex]));
        using (var ms = new MemoryStream(modelData))
        {
            var h3d = FormatIdentifier.IdentifyAndOpen(ms);
            if (h3d != null) scene.Merge(h3d);
        }

        // Merge texture entries
        foreach (int idx in group.TextureIndices)
        {
            byte[] data = Decompress(GARC.ReadEntry(fs, entries[idx]));
            using var ms = new MemoryStream(data);
            var h3d = FormatIdentifier.IdentifyAndOpen(ms);
            if (h3d != null) scene.Merge(h3d);
        }

        // Merge animation entries (need skeleton for GFMotion decoding)
        var mdlSkeleton = scene.Models.Count > 0 ? scene.Models[0].Skeleton : skeleton;
        foreach (int idx in group.AnimationIndices)
        {
            byte[] data = Decompress(GARC.ReadEntry(fs, entries[idx]));
            using var ms = new MemoryStream(data);
            var h3d = FormatIdentifier.IdentifyAndOpen(ms, mdlSkeleton);
            if (h3d != null) scene.Merge(h3d);
        }

        if (scene.Models.Count == 0)
        {
            Console.Error.WriteLine($"  [{group.Id}] No models found, skipping");
            return;
        }

        // Save textures
        string texturesDir = Path.Combine(groupDir, "textures");
        var manifestTextures = new List<string>();

        if (scene.Textures.Count > 0)
        {
            Directory.CreateDirectory(texturesDir);
            foreach (var tex in scene.Textures)
            {
                string safeName = SanitizeName(tex.Name);
                string texPath = Path.Combine(texturesDir, $"{safeName}.png");
                try
                {
                    using var bmp = tex.ToBitmap();
                    bmp.Save(texPath, ImageFormat.Png);
                    manifestTextures.Add($"textures/{safeName}.png");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  [{group.Id}] Texture error {tex.Name}: {ex.Message}");
                }
            }
        }

        // Bake multi-texture materials if requested
        if (bakeTextures)
            PicaTextureBaker.BakeScene(scene, texturesDir);

        // Export model DAEs
        var manifestModels = new List<ManifestModelEntry>();

        for (int m = 0; m < scene.Models.Count; m++)
        {
            string suffix = scene.Models.Count > 1 ? $"_{m}" : "";
            string modelFileName = $"model{suffix}.{format}";
            string modelFile = Path.Combine(groupDir, modelFileName);

            var dae = new DAE(scene, m);

            // Patch texture refs to point into textures/ subfolder
            if (dae.library_images != null)
            {
                foreach (var img in dae.library_images)
                {
                    if (img.init_from != null && !img.init_from.Contains("textures/"))
                        img.init_from = $"./textures/{Path.GetFileName(img.init_from)}";
                }
            }

            dae.Save(modelFile);

            var mdl = scene.Models[m];
            manifestModels.Add(new ManifestModelEntry
            {
                File = modelFileName,
                Name = mdl.Name ?? $"model{suffix}",
                MeshCount = mdl.Meshes.Count,
                BoneCount = mdl.Skeleton.Count
            });
        }

        // Export animation clips
        var manifestClips = new List<ManifestClipEntry>();

        if (scene.SkeletalAnimations.Count > 0)
        {
            string clipsDir = Path.Combine(groupDir, "clips");
            Directory.CreateDirectory(clipsDir);

            for (int a = 0; a < scene.SkeletalAnimations.Count; a++)
            {
                string clipFileName = $"clip_{a:D3}.dae";
                string clipFile = Path.Combine(clipsDir, clipFileName);

                new DAE(scene, 0, a, clipOnly: true).Save(clipFile);

                var anim = scene.SkeletalAnimations[a];
                manifestClips.Add(new ManifestClipEntry
                {
                    Index = a,
                    Id = $"clip_{a:D3}",
                    Name = anim.Name ?? $"clip_{a}",
                    SourceName = anim.Name,
                    File = $"clips/{clipFileName}",
                    FrameCount = (int)anim.FramesCount,
                    Fps = 30,
                    BoneCount = anim.Elements.Count
                });
            }
        }

        // Write manifest
        string manifestModelFileName = manifestModels.Count > 0 ? manifestModels[0].File : null;
        ManifestSerializer.Write(Path.Combine(groupDir, "manifest.json"), new ExportManifest
        {
            Name = group.Id,
            Dir = groupDir.Replace('\\', '/'),
            AssetsPath = Path.GetRelativePath(outputDir, groupDir).Replace('\\', '/'),
            Format = format,
            ModelFormat = format,
            Id = group.Id,
            AnimationMode = "split",
            ModelFile = manifestModelFileName,
            Models = manifestModels,
            Textures = manifestTextures,
            Clips = manifestClips
        });

        Console.WriteLine($"  {group.Id}: {scene.Models.Count} model(s), {scene.Textures.Count} tex, {scene.SkeletalAnimations.Count} clip(s)");
    }

    // ── Flat Extract (per-entry, for maps/terrain) ─────────────────────────

    private static int RunFlatExtract(string inputFile, string outputDir, string format,
        int skip, int limit, bool bakeTextures)
    {
        Console.WriteLine($"Loading GARC: {inputFile}");
        Console.WriteLine($"Output: {outputDir} (flat mode — one folder per model entry)");
        Console.WriteLine();

        using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        var entries = GARC.GetEntries(fs);
        Directory.CreateDirectory(outputDir);

        int start = Math.Min(skip, entries.Length);
        int end = limit > 0 ? Math.Min(start + limit, entries.Length) : entries.Length;

        int extracted = 0, skipped = 0, failed = 0;

        for (int i = start; i < end; i++)
        {
            byte[] data;
            try { data = Decompress(GARC.ReadEntry(fs, entries[i])); }
            catch { skipped++; continue; }

            if (data.Length < 4) { skipped++; continue; }

            // Try to parse this entry as an H3D scene
            H3D scene;
            try
            {
                using var ms = new MemoryStream(data);
                scene = FormatIdentifier.IdentifyAndOpen(ms);
            }
            catch { skipped++; continue; }

            if (scene == null || scene.Models.Count == 0)
            {
                skipped++;
                continue;
            }

            // Derive folder name from model name or entry index
            string folderName = scene.Models[0].Name;
            if (string.IsNullOrEmpty(folderName))
                folderName = $"entry_{i:D5}";
            folderName = SanitizeName(folderName);

            // Avoid collisions
            string groupDir = Path.Combine(outputDir, folderName);
            if (Directory.Exists(groupDir))
            {
                int suffix = 2;
                while (Directory.Exists($"{groupDir}_{suffix}")) suffix++;
                groupDir = $"{groupDir}_{suffix}";
                folderName = Path.GetFileName(groupDir);
            }

            try
            {
                Directory.CreateDirectory(groupDir);

                // Save textures
                string texturesDir = Path.Combine(groupDir, "textures");
                var manifestTextures = new List<string>();

                if (scene.Textures.Count > 0)
                {
                    Directory.CreateDirectory(texturesDir);
                    foreach (var tex in scene.Textures)
                    {
                        string safeName = SanitizeName(tex.Name);
                        string texPath = Path.Combine(texturesDir, $"{safeName}.png");
                        try
                        {
                            using var bmp = tex.ToBitmap();
                            bmp.Save(texPath, ImageFormat.Png);
                            manifestTextures.Add($"textures/{safeName}.png");
                        }
                        catch { }
                    }
                }

                if (bakeTextures)
                    PicaTextureBaker.BakeScene(scene, texturesDir);

                // Export model DAEs
                var manifestModels = new List<ManifestModelEntry>();

                for (int m = 0; m < scene.Models.Count; m++)
                {
                    string modelSuffix = scene.Models.Count > 1 ? $"_{m}" : "";
                    string modelFileName = $"model{modelSuffix}.{format}";
                    string modelFile = Path.Combine(groupDir, modelFileName);

                    var dae = new DAE(scene, m);

                    if (dae.library_images != null)
                    {
                        foreach (var img in dae.library_images)
                        {
                            if (img.init_from != null && !img.init_from.Contains("textures/"))
                                img.init_from = $"./textures/{Path.GetFileName(img.init_from)}";
                        }
                    }

                    dae.Save(modelFile);

                    var mdl = scene.Models[m];
                    manifestModels.Add(new ManifestModelEntry
                    {
                        File = modelFileName,
                        Name = mdl.Name ?? $"model{modelSuffix}",
                        MeshCount = mdl.Meshes.Count,
                        BoneCount = mdl.Skeleton.Count
                    });
                }

                // Export animation clips (maps rarely have these, but handle them)
                var manifestClips = new List<ManifestClipEntry>();

                if (scene.SkeletalAnimations.Count > 0)
                {
                    string clipsDir = Path.Combine(groupDir, "clips");
                    Directory.CreateDirectory(clipsDir);

                    for (int a = 0; a < scene.SkeletalAnimations.Count; a++)
                    {
                        string clipFileName = $"clip_{a:D3}.dae";
                        string clipFile = Path.Combine(clipsDir, clipFileName);

                        new DAE(scene, 0, a, clipOnly: true).Save(clipFile);

                        var anim = scene.SkeletalAnimations[a];
                        manifestClips.Add(new ManifestClipEntry
                        {
                            Index = a,
                            Id = $"clip_{a:D3}",
                            Name = anim.Name ?? $"clip_{a}",
                            SourceName = anim.Name,
                            File = $"clips/{clipFileName}",
                            FrameCount = (int)anim.FramesCount,
                            Fps = 30,
                            BoneCount = anim.Elements.Count
                        });
                    }
                }

                // Write manifest
                string flatModelFileName = manifestModels.Count > 0 ? manifestModels[0].File : null;
                ManifestSerializer.Write(Path.Combine(groupDir, "manifest.json"), new ExportManifest
                {
                    Name = folderName,
                    Dir = groupDir.Replace('\\', '/'),
                    AssetsPath = Path.GetRelativePath(outputDir, groupDir).Replace('\\', '/'),
                    Format = format,
                    ModelFormat = format,
                    Id = folderName,
                    AnimationMode = manifestClips.Count > 0 ? "split" : "static",
                    ModelFile = flatModelFileName,
                    Models = manifestModels,
                    Textures = manifestTextures,
                    Clips = manifestClips
                });

                Console.WriteLine($"  [{i:D5}] {folderName}: {scene.Models.Count} model(s), {scene.Textures.Count} tex, {scene.SkeletalAnimations.Count} clip(s)");
                extracted++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ERROR [{i:D5}]: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"=== Flat extract complete ===");
        Console.WriteLine($"  Extracted: {extracted}  Skipped: {skipped}  Failed: {failed}");

        return failed > 0 && extracted == 0 ? 1 : 0;
    }

    // ── Dump (raw bytes) ───────────────────────────────────────────────────

    private static int RunDump(string inputFile, string outputDir, int skip, int limit)
    {
        Console.WriteLine($"Loading GARC: {inputFile}");
        Console.WriteLine($"Output: {outputDir} (raw dump)");
        Console.WriteLine();

        using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        var entries = GARC.GetEntries(fs);
        Directory.CreateDirectory(outputDir);

        int start = Math.Min(skip, entries.Length);
        int end = limit > 0 ? Math.Min(start + limit, entries.Length) : entries.Length;

        for (int i = start; i < end; i++)
        {
            byte[] data = Decompress(GARC.ReadEntry(fs, entries[i]));
            string outPath = Path.Combine(outputDir, $"entry_{i:D5}.bin");
            File.WriteAllBytes(outPath, data);
            Console.WriteLine($"  [{i:D5}] {data.Length} bytes");
        }

        Console.WriteLine();
        Console.WriteLine($"Dumped {end - start} entries to {outputDir}");
        return 0;
    }

    // ── Entry classification ──────────────────────────────────────────────

    enum EntryKind { Unknown, Model, Texture, Animation, Metadata }

    private static EntryKind ClassifyEntry(byte[] data)
    {
        if (data.Length < 4) return EntryKind.Unknown;

        uint magic = BitConverter.ToUInt32(data, 0);

        // Check GFPackage (two uppercase ASCII bytes) first
        if (data[0] >= 'A' && data[0] <= 'Z' && data[1] >= 'A' && data[1] <= 'Z'
            && data.Length >= 0x80 && magic != 0x00484342)
        {
            string pkg = $"{(char)data[0]}{(char)data[1]}";
            return pkg switch
            {
                "PC" => ClassifyPCPackage(data),
                "PK" or "PB" or "BS" => EntryKind.Animation,
                "AD" or "PT" => EntryKind.Texture,
                "CM" or "MM" or "GR" or "BG" => EntryKind.Model,
                _ => EntryKind.Metadata
            };
        }

        return magic switch
        {
            0x15122117 => EntryKind.Model,      // GFModel
            0x15041213 => EntryKind.Texture,     // GFTexture
            0x00060000 => EntryKind.Animation,   // GFMotion
            0x00010000 => EntryKind.Model,       // GFModelPack
            0x00484342 => EntryKind.Model,       // BCH
            _ => EntryKind.Unknown
        };
    }

    private static EntryKind ClassifyPCPackage(byte[] data)
    {
        // PC packages contain sub-entries. Peek at the first sub-entry's magic.
        if (data.Length < 12) return EntryKind.Metadata;

        ushort entryCount = BitConverter.ToUInt16(data, 2);
        if (entryCount == 0) return EntryKind.Metadata;

        uint firstOffset = BitConverter.ToUInt32(data, 4);
        long absOffset = 4 + firstOffset - 4; // relative to byte 0
        if (absOffset < 0 || absOffset + 4 > data.Length) return EntryKind.Metadata;

        uint subMagic = BitConverter.ToUInt32(data, (int)absOffset);
        return subMagic switch
        {
            0x15122117 => EntryKind.Model,    // GFModel inside PC
            0x15041213 => EntryKind.Texture,  // GFTexture inside PC
            0x00060000 => EntryKind.Animation,
            _ => EntryKind.Metadata
        };
    }

    private static string ExtractPokemonId(H3D scene)
    {
        // Try texture names: "pm0001_00_BodyA1" → "pm0001_00"
        foreach (var tex in scene.Textures)
        {
            string id = ExtractIdFromName(tex.Name);
            if (id != null) return id;
        }

        // Try material texture references
        foreach (var mdl in scene.Models)
        {
            foreach (var mat in mdl.Materials)
            {
                string id = ExtractIdFromName(mat.Texture0Name);
                if (id != null) return id;
            }
        }

        // Fall back to model name
        if (scene.Models.Count > 0 && !string.IsNullOrEmpty(scene.Models[0].Name))
            return scene.Models[0].Name;

        return null;
    }

    private static string ExtractIdFromName(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith("pm")) return null;

        // "pm0001_00_BodyA1" → find second underscore
        int first = name.IndexOf('_', 2);
        if (first < 0) return null;
        int second = name.IndexOf('_', first + 1);
        return second > 0 ? name[..second] : null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static byte[] Decompress(byte[] data)
    {
        if (LZSS.IsCompressed(data))
        {
            try { return LZSS.Decompress(data); }
            catch { return data; }
        }
        return data;
    }

    private static string IdentifyType(byte[] data)
    {
        if (data.Length < 4) return "empty";
        uint magic = BitConverter.ToUInt32(data, 0);
        return magic switch
        {
            0x15122117 => "GFModel",
            0x15041213 => "GFTexture",
            0x00060000 => "GFMotion",
            0x00010000 => "GFModelPack",
            0x00484342 => "BCH",
            _ when data[0] >= 'A' && data[0] <= 'Z' && data[1] >= 'A' && data[1] <= 'Z'
                => $"GFPackage({(char)data[0]}{(char)data[1]})",
            _ => $"unknown(0x{magic:X8})"
        };
    }

    private static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    class PokemonGroup
    {
        public string Id;
        public int ModelIndex;
        public List<int> TextureIndices = new();
        public List<int> AnimationIndices = new();
    }
}
