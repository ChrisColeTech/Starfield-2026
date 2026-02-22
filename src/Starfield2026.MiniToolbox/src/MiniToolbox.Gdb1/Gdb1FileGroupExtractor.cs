using MiniToolbox.Core.Pipeline;
using MiniToolbox.Gdb1.Exporters;
using MiniToolbox.Gdb1.Extraction;
using MiniToolbox.Gdb1.Models;
using System.Text.Json;

namespace MiniToolbox.Gdb1;

/// <summary>
/// GDB1 extractor implementing the pipeline interface for parallel extraction.
/// </summary>
public class Gdb1FileGroupExtractor : IFileGroupExtractor
{
    private readonly ResourceDatabase _resourceDb;
    private readonly ExtractionOptions _options;

    public Gdb1FileGroupExtractor(string resourceFolder, ExtractionOptions? options = null)
    {
        _resourceDb = new ResourceDatabase(resourceFolder);
        _options = options ?? new ExtractionOptions();
    }

    public ResourceDatabase ResourceDb => _resourceDb;

    public IEnumerable<ExtractionJob> EnumerateJobs()
    {
        foreach (var (modelId, modelPath) in _resourceDb.Models)
        {
            // Gather all source files for this model
            var sourceFiles = new List<string> { modelPath };

            string binPath = Path.ChangeExtension(modelPath, ".modelbin");
            if (File.Exists(binPath))
                sourceFiles.Add(binPath);

            string metaPath = Path.ChangeExtension(modelPath, ".resourcemetadata");
            if (File.Exists(metaPath))
                sourceFiles.Add(metaPath);

            // Get model name from metadata
            string modelName = modelId;
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
                            modelName = Path.GetFileNameWithoutExtension(s);
                            break;
                        }
                    }
                }
                catch { }
            }

            yield return new ExtractionJob
            {
                Id = modelId,
                Name = modelName,
                SourceFiles = sourceFiles.ToArray(),
                Metadata = new Dictionary<string, object>
                {
                    ["gdbPath"] = modelPath,
                    ["binPath"] = binPath
                }
            };
        }
    }

    public async Task<ExtractionResult> ProcessJobAsync(ExtractionJob job, CancellationToken cancellationToken = default)
    {
        var result = ExtractionResult.Succeeded(job.Id, job.Name);

        try
        {
            string gdbPath = (string)job.Metadata["gdbPath"];
            string binPath = (string)job.Metadata["binPath"];

            // Copy source files to temp (streaming)
            string tempGdb = Path.Combine(job.TempPath, Path.GetFileName(gdbPath));
            string tempBin = Path.Combine(job.TempPath, Path.GetFileName(binPath));

            await StreamingFileReader.CopyFileAsync(gdbPath, tempGdb, cancellationToken);
            if (File.Exists(binPath))
                await StreamingFileReader.CopyFileAsync(binPath, tempBin, cancellationToken);

            // Extract model (reads from temp files)
            var extractor = new ModelExtractor(tempGdb, _resourceDb);
            var mesh = extractor.Extract();

            // Create output subdirectories
            string texturesDir = Path.Combine(job.OutputPath, "textures");
            string clipsDir = Path.Combine(job.OutputPath, "clips");
            Directory.CreateDirectory(texturesDir);
            Directory.CreateDirectory(clipsDir);

            // Export textures first (as PNG) so we can pass them to ObjExporter for MTL
            var textureInfos = new List<TextureInfo>();
            foreach (var texId in mesh.TextureIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var texPath = _resourceDb.GetTexturePath(texId);
                if (texPath == null) continue;

                try
                {
                    // Copy to temp first
                    string tempTexGdb = Path.Combine(job.TempPath, Path.GetFileName(texPath));
                    await StreamingFileReader.CopyFileAsync(texPath, tempTexGdb, cancellationToken);

                    string texBinPath = Path.ChangeExtension(texPath, ".texturebin");
                    if (File.Exists(texBinPath))
                    {
                        string tempTexBin = Path.Combine(job.TempPath, Path.GetFileName(texBinPath));
                        await StreamingFileReader.CopyFileAsync(texBinPath, tempTexBin, cancellationToken);
                    }

                    var texExtractor = new TextureExtractor(tempTexGdb);
                    var texInfo = texExtractor.Extract();

                    // Export as PNG
                    string pngPath = Path.Combine(texturesDir, $"{texInfo.Name}.png");
                    TexturePngExporter.Export(texInfo, pngPath);
                    texInfo.FileName = $"textures/{texInfo.Name}.png";

                    textureInfos.Add(texInfo);
                    result.OutputFiles.Add($"textures/{texInfo.Name}.png");
                }
                catch
                {
                    // Continue with other textures
                }
            }

            // Export model with material references (OBJ + MTL)
            string modelFile = Path.Combine(job.OutputPath, "model.obj");
            ObjExporter.Export(mesh, modelFile, textureInfos);
            result.OutputFiles.Add("model.obj");
            if (textureInfos.Count > 0)
            {
                result.OutputFiles.Add("model.mtl");
            }

            // Export animations immediately
            var animations = new List<ConstColorAnimation>();
            foreach (var animId in extractor.GetAnimationRefs())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var animPath = _resourceDb.GetAnimationPath(animId);
                if (animPath == null) continue;

                try
                {
                    // Copy to temp first
                    string tempAnimGdb = Path.Combine(job.TempPath, Path.GetFileName(animPath));
                    await StreamingFileReader.CopyFileAsync(animPath, tempAnimGdb, cancellationToken);

                    var animExtractor = new AnimationExtractor(tempAnimGdb);
                    var anim = animExtractor.Extract();

                    string safeName = SanitizeName(anim.Name);
                    string outputPath = Path.Combine(clipsDir, $"{safeName}.json");
                    JsonExporter.ExportAnimation(anim, outputPath);

                    animations.Add(anim);
                    result.OutputFiles.Add($"clips/{safeName}.json");
                }
                catch
                {
                    // Continue with other animations
                }
            }

            // Write manifest immediately
            var package = new ModelPackage
            {
                Id = job.Id,
                Name = job.Name,
                Mesh = mesh,
                Textures = textureInfos,
                Animations = animations,
                SourceModelGdb = Path.GetFileName(gdbPath),
                SourceModelBin = Path.GetFileName(binPath)
            };

            string manifestPath = Path.Combine(job.OutputPath, "manifest.json");
            JsonExporter.ExportManifest(package, manifestPath);
            result.OutputFiles.Add("manifest.json");

            // Record stats
            result.Stats["triangles"] = mesh.TriangleCount;
            result.Stats["vertices"] = mesh.Vertices.Count;
            result.Stats["textures"] = textureInfos.Count;
            result.Stats["animations"] = animations.Count;

            return result;
        }
        catch (Exception ex)
        {
            return ExtractionResult.Failed(job.Id, ex.Message);
        }
    }

    public bool ValidateJobOutput(ExtractionJob job, ExtractionResult result)
    {
        // Check that essential files exist
        string modelPath = Path.Combine(job.OutputPath, "model.obj");
        string manifestPath = Path.Combine(job.OutputPath, "manifest.json");

        if (!File.Exists(modelPath))
            return false;

        if (!File.Exists(manifestPath))
            return false;

        // Verify model file has content
        var modelSize = new FileInfo(modelPath).Length;
        if (modelSize < 100) // Minimum valid OBJ size
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

    private static string SanitizeName(string name)
    {
        return new string(name.Select(c =>
            char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
    }
}
