using System.Text.Json;
using System.Text.Json.Serialization;
using MiniToolbox.Gdb1.Models;

namespace MiniToolbox.Gdb1.Exporters;

/// <summary>
/// Exports animations and manifests to JSON format.
/// </summary>
public static class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new()
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

        string json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Exports a model package manifest to JSON.
    /// </summary>
    public static void ExportManifest(ModelPackage package, string outputPath)
    {
        var manifest = new ManifestJson
        {
            Id = package.Id,
            Name = package.Name,
            Model = new ModelInfoJson
            {
                File = "model.obj",
                Vertices = package.Mesh?.Vertices.Count ?? 0,
                Triangles = package.Mesh?.TriangleCount ?? 0
            },
            Textures = package.Textures.Select(t => new TextureInfoJson
            {
                Name = t.Name,
                File = t.FileName,
                Width = t.Width,
                Height = t.Height,
                Format = t.FormatName,
                Size = t.DataSize
            }).ToList(),
            Clips = package.Animations.Select(a => new ClipInfoJson
            {
                Name = a.Name,
                Duration = a.Duration,
                Tracks = a.Tracks.Count
            }).ToList(),
            Source = new SourceInfoJson
            {
                ModelGdb = package.SourceModelGdb,
                ModelBin = package.SourceModelBin
            }
        };

        string json = JsonSerializer.Serialize(manifest, Options);
        File.WriteAllText(outputPath, json);
    }

    // JSON DTOs
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

    private class ManifestJson
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public ModelInfoJson Model { get; set; } = new();
        public List<TextureInfoJson> Textures { get; set; } = new();
        public List<ClipInfoJson> Clips { get; set; } = new();
        public SourceInfoJson Source { get; set; } = new();
    }

    private class ModelInfoJson
    {
        public string File { get; set; } = "";
        public int Vertices { get; set; }
        public int Triangles { get; set; }
    }

    private class TextureInfoJson
    {
        public string Name { get; set; } = "";
        public string File { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; } = "";
        public int Size { get; set; }
    }

    private class ClipInfoJson
    {
        public string Name { get; set; } = "";
        public float Duration { get; set; }
        public int Tracks { get; set; }
    }

    private class SourceInfoJson
    {
        public string ModelGdb { get; set; } = "";
        public string ModelBin { get; set; } = "";
    }
}
