using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniToolbox.Manifests;

/// <summary>
/// Shared serialization for manifest files.
/// Uses camelCase naming and omits null fields.
/// </summary>
public static class ManifestSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(ExportManifest manifest)
        => JsonSerializer.Serialize(manifest, Options);

    public static void Write(string path, ExportManifest manifest)
        => System.IO.File.WriteAllText(path, Serialize(manifest));

    public static async Task WriteAsync(string path, ExportManifest manifest, CancellationToken ct = default)
        => await System.IO.File.WriteAllTextAsync(path, Serialize(manifest), ct);
}
