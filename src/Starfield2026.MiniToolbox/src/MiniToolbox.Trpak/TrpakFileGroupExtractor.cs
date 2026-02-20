using MiniToolbox.Core.Pipeline;
using MiniToolbox.Core.Texture;
using MiniToolbox.Core.Utils;
using MiniToolbox.Trpak.Archive;
using MiniToolbox.Trpak.Decoders;
using MiniToolbox.Trpak.Exporters;
using MiniToolbox.Trpak.Flatbuffers.GF.Animation;
using MiniToolbox.Trpak.Flatbuffers.TR.Model;
using System.Text.Json;

namespace MiniToolbox.Trpak;

/// <summary>
/// TRPAK extractor implementing the pipeline interface for parallel extraction.
/// </summary>
public class TrpakFileGroupExtractor : IFileGroupExtractor
{
    private readonly TrpfsLoader _loader;
    private readonly ExtractionOptions _options;
    private readonly bool _splitMode;

    public TrpakFileGroupExtractor(TrpfsLoader loader, ExtractionOptions? options = null)
    {
        _loader = loader;
        _options = options ?? new ExtractionOptions();
        _splitMode = _options.AnimationMode?.ToLowerInvariant() != "baked";
    }

    public TrpfsLoader Loader => _loader;

    public IEnumerable<ExtractionJob> EnumerateJobs()
    {
        foreach (var (hash, modelPath) in _loader.FindFilesByExtension(".trmdl"))
        {
            string modelName = Path.GetFileNameWithoutExtension(modelPath);

            yield return new ExtractionJob
            {
                Id = hash.ToString("x16"),
                Name = modelName,
                SourceFiles = new[] { modelPath },
                Metadata = new Dictionary<string, object>
                {
                    ["hash"] = hash,
                    ["modelPath"] = modelPath,
                    ["modelDir"] = GetDirectoryOrEmpty(modelPath)
                }
            };
        }
    }

    public async Task<ExtractionResult> ProcessJobAsync(ExtractionJob job, CancellationToken cancellationToken = default)
    {
        var result = ExtractionResult.Succeeded(job.Id, job.Name);

        try
        {
            string modelPath = (string)job.Metadata["modelPath"];
            string modelDir = (string)job.Metadata["modelDir"];
            string normalizedModel = NormalizePath(modelPath);

            // Phase 1: Extract all dependencies to temp
            var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await ExtractModelAndDependenciesAsync(normalizedModel, modelDir, job.TempPath, extracted, cancellationToken);

            // Phase 2: Decode model
            string trmdlOnDisk = Path.Combine(job.TempPath, normalizedModel.Replace('/', Path.DirectorySeparatorChar));
            var decoder = new TrinityModelDecoder(trmdlOnDisk);
            var exportData = decoder.CreateExportData();

            // Phase 3: Export model immediately
            string modelExt = _options.OutputFormat?.ToLowerInvariant() == "obj" ? ".obj" : ".dae";
            string modelOut = Path.Combine(job.OutputPath, "model" + modelExt);

            // For now, always export as DAE (OBJ export not implemented for TRPAK)
            if (modelExt == ".obj")
            {
                modelOut = Path.Combine(job.OutputPath, "model.dae");
            }

            TrinityColladaExporter.Export(modelOut, exportData);
            result.OutputFiles.Add(Path.GetFileName(modelOut));

            // Phase 4: Decode and export textures immediately
            string texOutDir = Path.Combine(job.OutputPath, "textures");
            Directory.CreateDirectory(texOutDir);

            var exportedTextures = new List<string>();
            foreach (string bntxFile in Directory.EnumerateFiles(job.TempPath, "*.bntx", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var bntxBytes = await File.ReadAllBytesAsync(bntxFile, cancellationToken);
                    var textures = BntxDecoder.Decode(bntxBytes);
                    foreach (var tex in textures)
                    {
                        string pngPath = Path.Combine(texOutDir, tex.Name + ".png");
                        tex.SavePng(pngPath);
                        exportedTextures.Add("textures/" + tex.Name + ".png");
                        result.OutputFiles.Add($"textures/{tex.Name}.png");
                    }
                }
                catch
                {
                    // Continue with other textures
                }
            }

            // Phase 5: Bake eye textures
            foreach (var mat in exportData.Materials)
            {
                if (EyeTextureBaker.IsEyeMaterial(mat))
                {
                    EyeTextureBaker.BakeEyeTexture(mat, job.TempPath, texOutDir);
                }
            }

            // Phase 6: Extract and export animations immediately
            string clipOutDir = Path.Combine(job.OutputPath, _splitMode ? "clips" : "animations");
            Directory.CreateDirectory(clipOutDir);

            int animIndex = 0;
            var clipManifestEntries = new List<object>();

            if (exportData.Armature != null)
            {
                foreach (var (hash, animName) in _loader.FindFiles(name =>
                    name.StartsWith(modelDir, StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".tranm", StringComparison.OrdinalIgnoreCase)))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var animBytes = _loader.ExtractFile(hash);
                        if (animBytes == null) continue;

                        var animFb = FlatBufferConverter.DeserializeFrom<Animation>(animBytes);
                        string clipName = Path.GetFileNameWithoutExtension(animName);
                        var animDecoder = new TrinityAnimationDecoder(animFb, clipName);

                        string clipId = $"clip_{animIndex:D3}";
                        string clipDae = Path.Combine(clipOutDir, clipId + ".dae");

                        if (_splitMode)
                        {
                            TrinityColladaExporter.ExportClipOnly(clipDae, exportData.Armature, animDecoder, clipName);
                        }
                        else
                        {
                            TrinityColladaExporter.ExportWithAnimation(clipDae, exportData, animDecoder);
                        }

                        clipManifestEntries.Add(new
                        {
                            index = animIndex,
                            id = clipId,
                            sourceName = clipName,
                            file = (_splitMode ? "clips/" : "animations/") + clipId + ".dae",
                            frameCount = (int)animDecoder.FrameCount,
                            fps = (int)(animDecoder.FrameRate > 0 ? animDecoder.FrameRate : 30)
                        });

                        result.OutputFiles.Add($"{(_splitMode ? "clips" : "animations")}/{clipId}.dae");
                        animIndex++;
                    }
                    catch
                    {
                        // Continue with other animations
                    }
                }
            }

            // Copy textures for baked mode
            if (!_splitMode && animIndex > 0 && Directory.Exists(texOutDir))
            {
                string animTexDir = Path.Combine(clipOutDir, "textures");
                Directory.CreateDirectory(animTexDir);
                foreach (var texFile in Directory.GetFiles(texOutDir, "*.png"))
                {
                    File.Copy(texFile, Path.Combine(animTexDir, Path.GetFileName(texFile)), overwrite: true);
                }
            }

            // Phase 7: Write manifest immediately
            var manifest = new
            {
                version = 1,
                format = Path.GetExtension(modelOut).TrimStart('.'),
                modelFile = Path.GetFileName(modelOut),
                animationMode = _splitMode ? "split" : "baked",
                rawFiles = ".raw",
                textures = exportedTextures.ToArray(),
                clips = clipManifestEntries.ToArray()
            };

            string manifestPath = Path.Combine(job.OutputPath, "manifest.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            result.OutputFiles.Add("manifest.json");

            // Record stats
            result.Stats["submeshes"] = exportData.Submeshes.Count;
            result.Stats["materials"] = exportData.Materials.Count;
            result.Stats["bones"] = exportData.Armature?.Bones.Count ?? 0;
            result.Stats["textures"] = exportedTextures.Count;
            result.Stats["animations"] = animIndex;

            return result;
        }
        catch (Exception ex)
        {
            return ExtractionResult.Failed(job.Id, ex.Message);
        }
    }

    private async Task ExtractModelAndDependenciesAsync(
        string modelPath,
        string modelDir,
        string tempRoot,
        HashSet<string> extracted,
        CancellationToken ct)
    {
        var pending = new Queue<string>();
        pending.Enqueue(modelPath);

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            string relPath = NormalizePath(pending.Dequeue());
            if (!extracted.Add(relPath)) continue;

            var bytes = _loader.ExtractFile(relPath);
            if (bytes == null) continue;

            // Write to temp immediately
            await WriteExtractedFileAsync(tempRoot, relPath, bytes, ct);

            string ext = Path.GetExtension(relPath).ToLowerInvariant();
            string dir = GetDirectoryOrEmpty(relPath);

            // Parse and enqueue dependencies
            if (ext == ".trmdl")
            {
                try
                {
                    var mdl = FlatBufferConverter.DeserializeFrom<TRMDL>(bytes);

                    if (mdl.Meshes != null)
                    {
                        foreach (var mesh in mdl.Meshes)
                        {
                            if (!string.IsNullOrWhiteSpace(mesh?.PathName))
                                EnqueuePath(dir, mesh.PathName, pending);
                        }
                    }

                    if (mdl.Materials != null)
                    {
                        foreach (var mat in mdl.Materials)
                        {
                            if (!string.IsNullOrWhiteSpace(mat))
                                EnqueuePath(dir, mat, pending);
                        }
                    }

                    if (mdl.Skeleton != null && !string.IsNullOrWhiteSpace(mdl.Skeleton.PathName))
                    {
                        EnqueuePath(dir, mdl.Skeleton.PathName, pending);
                    }
                }
                catch { }
            }
            else if (ext == ".trmsh")
            {
                try
                {
                    var msh = FlatBufferConverter.DeserializeFrom<TRMSH>(bytes);
                    if (!string.IsNullOrWhiteSpace(msh?.bufferFilePath))
                        EnqueuePath(dir, msh.bufferFilePath, pending);
                }
                catch { }
            }
            else if (ext == ".trmtr")
            {
                try
                {
                    var mtr = FlatBufferConverter.DeserializeFrom<TRMTR>(bytes);
                    if (mtr?.Materials != null)
                    {
                        foreach (var mat in mtr.Materials)
                        {
                            if (mat?.Textures == null) continue;
                            foreach (var tex in mat.Textures)
                            {
                                if (!string.IsNullOrWhiteSpace(tex?.File))
                                    EnqueuePath(dir, tex.File, pending);
                            }
                        }
                    }
                }
                catch { }
            }
        }
    }

    public bool ValidateJobOutput(ExtractionJob job, ExtractionResult result)
    {
        // Check that essential files exist
        string modelPath = Path.Combine(job.OutputPath, "model.dae");
        if (!File.Exists(modelPath))
        {
            // Try .obj
            modelPath = Path.Combine(job.OutputPath, "model.obj");
            if (!File.Exists(modelPath))
                return false;
        }

        string manifestPath = Path.Combine(job.OutputPath, "manifest.json");
        if (!File.Exists(manifestPath))
            return false;

        // Verify model file has content
        var modelSize = new FileInfo(modelPath).Length;
        if (modelSize < 100)
            return false;

        // Verify manifest is valid JSON
        try
        {
            string manifestJson = File.ReadAllText(manifestPath);
            JsonDocument.Parse(manifestJson);
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static string NormalizePath(string path)
    {
        path = (path ?? string.Empty).Replace('\\', '/').Trim();
        if (path.StartsWith("romfs://", StringComparison.OrdinalIgnoreCase))
            path = path["romfs://".Length..];
        if (path.StartsWith("trpfs://", StringComparison.OrdinalIgnoreCase))
            path = path["trpfs://".Length..];
        return path.TrimStart('/');
    }

    private static string GetDirectoryOrEmpty(string path)
    {
        path = path.Replace('\\', '/');
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path[..(slash + 1)] : string.Empty;
    }

    private static void EnqueuePath(string baseDir, string relativePath, Queue<string> queue)
    {
        relativePath = relativePath.Replace('\\', '/').TrimStart('/');
        if (relativePath.StartsWith("romfs://", StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath["romfs://".Length..];
        if (relativePath.StartsWith("trpfs://", StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath["trpfs://".Length..];
        relativePath = relativePath.TrimStart('/');

        if (!relativePath.Contains('/') || !relativePath.StartsWith("pokemon", StringComparison.OrdinalIgnoreCase))
            relativePath = baseDir + relativePath;

        queue.Enqueue(relativePath);
    }

    private static async Task WriteExtractedFileAsync(string tempRoot, string relPath, byte[] data, CancellationToken ct)
    {
        string dest = Path.Combine(tempRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        await File.WriteAllBytesAsync(dest, data, ct);
    }
}
