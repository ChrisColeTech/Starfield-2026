using System.Text.Json;
using System.Text.Json.Serialization;
using MiniToolbox.Gdb1.Models;
using MiniToolbox.Manifests;

namespace MiniToolbox.Gdb1.Exporters;

/// <summary>
/// Exports animations and manifests to JSON format.
/// </summary>
public static class JsonExporter
{
    private static readonly JsonSerializerOptions AnimationOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Exports a color animation to JSON.
    /// </summary>
    public static void ExportAnimation(ConstColorAnimation animation, string outputPath)
    {
        var data = new AnimationJson
        {
            Name = animation.Name,
            Duration = animation.Duration,
            Type = "ConstColorAnimation",
            Source = animation.SourceFile,
            Tracks = animation.Tracks.Select(t => new TrackJson
            {
                Name = t.Name,
                Keyframes = t.Keyframes.Select(k => new KeyframeJson
                {
                    Time = k.Time,
                    Color = k.ToArray()
                }).ToList()
            }).ToList()
        };

        string json = JsonSerializer.Serialize(data, AnimationOptions);
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Exports a model package manifest to JSON.
    /// </summary>
    public static void ExportManifest(ModelPackage package, string outputPath)
    {
        string outputDir = Path.GetDirectoryName(outputPath)!;
        string folderName = Path.GetFileName(outputDir);
        var manifest = new ExportManifest
        {
            Name = !string.IsNullOrEmpty(package.Name) ? package.Name : folderName,
            Dir = outputDir.Replace('\\', '/'),
            AssetsPath = folderName,
            Format = "obj",
            ModelFormat = "obj",
            Id = package.Id,
            ModelFile = "model.obj",
            MtlFile = package.Textures.Count > 0 ? "model.mtl" : null,
            Textures = package.Textures.Select(t => t.FileName).ToList(),
            TextureDetails = package.Textures.Select(t => new ManifestTextureEntry
            {
                Name = t.Name,
                File = t.FileName,
                Width = t.Width,
                Height = t.Height,
                Format = t.FormatName,
                Size = t.DataSize
            }).ToList(),
            Clips = package.Animations.Select((a, i) => new ManifestClipEntry
            {
                Index = i,
                Name = a.Name,
                Duration = a.Duration,
                TrackCount = a.Tracks.Count
            }).ToList(),
            Source = new ManifestSourceInfo
            {
                ModelGdb = package.SourceModelGdb,
                ModelBin = package.SourceModelBin
            }
        };

        ManifestSerializer.Write(outputPath, manifest);
    }

    // Animation-specific JSON DTOs (not manifest-related)
    private class AnimationJson
    {
        public string Name { get; set; } = "";
        public float Duration { get; set; }
        public string Type { get; set; } = "";
        public string Source { get; set; } = "";
        public List<TrackJson> Tracks { get; set; } = new();
    }

    private class TrackJson
    {
        public string Name { get; set; } = "";
        public List<KeyframeJson> Keyframes { get; set; } = new();
    }

    private class KeyframeJson
    {
        public float Time { get; set; }
        public int[] Color { get; set; } = Array.Empty<int>();
    }
}
