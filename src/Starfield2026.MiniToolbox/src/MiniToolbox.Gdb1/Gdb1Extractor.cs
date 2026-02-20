using MiniToolbox.Gdb1.Exporters;
using MiniToolbox.Gdb1.Extraction;
using MiniToolbox.Gdb1.Models;

namespace MiniToolbox.Gdb1;

/// <summary>
/// High-level API for extracting GDB1 models, textures, and animations.
/// </summary>
public class Gdb1Extractor
{
    private readonly ResourceDatabase _resourceDb;

    public Gdb1Extractor(string resourceFolder)
    {
        _resourceDb = new ResourceDatabase(resourceFolder);
    }

    public ResourceDatabase ResourceDb => _resourceDb;

    /// <summary>
    /// Extracts a single model by ID.
    /// </summary>
    public Mesh? ExtractModel(string modelId)
    {
        var modelPath = _resourceDb.GetModelPath(modelId);
        if (modelPath == null)
            return null;

        var extractor = new ModelExtractor(modelPath, _resourceDb);
        return extractor.Extract();
    }

    /// <summary>
    /// Extracts a single texture by ID.
    /// </summary>
    public TextureInfo? ExtractTexture(string textureId)
    {
        var texturePath = _resourceDb.GetTexturePath(textureId);
        if (texturePath == null)
            return null;

        var extractor = new TextureExtractor(texturePath);
        return extractor.Extract();
    }

    /// <summary>
    /// Extracts a single animation by ID.
    /// </summary>
    public ConstColorAnimation? ExtractAnimation(string animationId)
    {
        var animPath = _resourceDb.GetAnimationPath(animationId);
        if (animPath == null)
            return null;

        var extractor = new AnimationExtractor(animPath);
        return extractor.Extract();
    }

    /// <summary>
    /// Extracts a complete model package with textures and animations.
    /// </summary>
    public ModelPackage? ExtractModelPackage(string modelId, string outputFolder)
    {
        var modelPath = _resourceDb.GetModelPath(modelId);
        if (modelPath == null)
            return null;

        try
        {
            var modelExtractor = new ModelExtractor(modelPath, _resourceDb);
            var mesh = modelExtractor.Extract();

            // Create package folder
            string packageName = SanitizeName(mesh.Name);
            if (string.IsNullOrEmpty(packageName))
                packageName = modelId;

            string packageFolder = Path.Combine(outputFolder, packageName);

            // Handle duplicate names
            if (Directory.Exists(packageFolder))
                packageFolder = Path.Combine(outputFolder, $"{packageName}_{modelId}");

            Directory.CreateDirectory(packageFolder);

            string texturesFolder = Path.Combine(packageFolder, "textures");
            Directory.CreateDirectory(texturesFolder);

            string clipsFolder = Path.Combine(packageFolder, "clips");
            Directory.CreateDirectory(clipsFolder);

            // Export model
            string objPath = Path.Combine(packageFolder, "model.obj");
            ObjExporter.Export(mesh, objPath);

            // Create package
            var package = new ModelPackage
            {
                Id = modelId,
                Name = mesh.Name,
                Mesh = mesh,
                SourceModelGdb = Path.GetFileName(modelPath),
                SourceModelBin = Path.GetFileName(Path.ChangeExtension(modelPath, ".modelbin"))
            };

            // Export referenced textures
            foreach (var texId in mesh.TextureIds)
            {
                var texPath = _resourceDb.GetTexturePath(texId);
                if (texPath == null) continue;

                try
                {
                    var texExtractor = new TextureExtractor(texPath);
                    string texName = texExtractor.GetTextureName();
                    string outputPath = Path.Combine(texturesFolder, $"{texName}.raw");

                    var texInfo = texExtractor.ExportRaw(outputPath);
                    package.Textures.Add(texInfo);
                }
                catch
                {
                    // Skip failed textures
                }
            }

            // Export referenced animations
            foreach (var animId in modelExtractor.GetAnimationRefs())
            {
                var animPath = _resourceDb.GetAnimationPath(animId);
                if (animPath == null) continue;

                try
                {
                    var animExtractor = new AnimationExtractor(animPath);
                    var anim = animExtractor.Extract();

                    string outputPath = Path.Combine(clipsFolder, $"{SanitizeName(anim.Name)}.json");
                    JsonExporter.ExportAnimation(anim, outputPath);
                    package.Animations.Add(anim);
                }
                catch
                {
                    // Skip failed animations
                }
            }

            // Export manifest
            string manifestPath = Path.Combine(packageFolder, "manifest.json");
            JsonExporter.ExportManifest(package, manifestPath);

            return package;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts all models to the specified output folder.
    /// </summary>
    public ExtractAllResult ExtractAll(string outputFolder, Action<ExtractProgress>? progressCallback = null)
    {
        Directory.CreateDirectory(outputFolder);

        var result = new ExtractAllResult();
        var modelIds = _resourceDb.Models.Keys.ToList();
        int total = modelIds.Count;

        for (int i = 0; i < modelIds.Count; i++)
        {
            var modelId = modelIds[i];
            var package = ExtractModelPackage(modelId, outputFolder);

            if (package != null)
            {
                result.Succeeded.Add(package);

                progressCallback?.Invoke(new ExtractProgress
                {
                    Current = i + 1,
                    Total = total,
                    ModelId = modelId,
                    ModelName = package.Name,
                    Success = true,
                    TriangleCount = package.Mesh?.TriangleCount ?? 0,
                    TextureCount = package.Textures.Count,
                    ClipCount = package.Animations.Count
                });
            }
            else
            {
                result.Failed.Add(modelId);

                progressCallback?.Invoke(new ExtractProgress
                {
                    Current = i + 1,
                    Total = total,
                    ModelId = modelId,
                    Success = false
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts all animations to the specified output folder.
    /// </summary>
    public int ExtractAllAnimations(string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);
        int count = 0;

        foreach (var (animId, animPath) in _resourceDb.Animations)
        {
            try
            {
                var extractor = new AnimationExtractor(animPath);
                var anim = extractor.Extract();

                string outputPath = Path.Combine(outputFolder, $"{SanitizeName(anim.Name)}.json");
                JsonExporter.ExportAnimation(anim, outputPath);
                count++;
            }
            catch
            {
                // Skip failed animations
            }
        }

        return count;
    }

    private static string SanitizeName(string name)
    {
        return new string(name.Select(c =>
            char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
    }
}

/// <summary>
/// Result of extracting all models.
/// </summary>
public class ExtractAllResult
{
    public List<ModelPackage> Succeeded { get; } = new();
    public List<string> Failed { get; } = new();

    public int SuccessCount => Succeeded.Count;
    public int FailedCount => Failed.Count;
    public int TotalCount => SuccessCount + FailedCount;
}

/// <summary>
/// Progress information during batch extraction.
/// </summary>
public class ExtractProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string ModelId { get; set; } = "";
    public string ModelName { get; set; } = "";
    public bool Success { get; set; }
    public int TriangleCount { get; set; }
    public int TextureCount { get; set; }
    public int ClipCount { get; set; }
}
