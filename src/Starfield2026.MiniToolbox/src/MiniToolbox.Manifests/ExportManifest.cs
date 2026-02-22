namespace MiniToolbox.Manifests;

/// <summary>
/// Canonical manifest format for exported model groups.
/// Serializes to camelCase JSON compatible with 3DModelLoader's SplitModelAnimationSetLoader.
/// </summary>
public class ExportManifest
{
    public int Version { get; set; } = 1;

    /// <summary>Display name, inferred from model filename or folder name.</summary>
    public string? Name { get; set; }

    /// <summary>Absolute path to the manifest's directory (forward slashes).</summary>
    public string? Dir { get; set; }

    /// <summary>Relative path from assets root to the manifest directory (forward slashes).</summary>
    public string? AssetsPath { get; set; }

    public string? Format { get; set; }

    /// <summary>File extension without dot (e.g. "dae", "obj", "fbx"). Mirrors Format for BgEditor compat.</summary>
    public string? ModelFormat { get; set; }

    public string? Id { get; set; }
    public string? AnimationMode { get; set; }

    /// <summary>Single-model path (TRPAK, GDB1). Also set by GARC for consumer compat.</summary>
    public string? ModelFile { get; set; }

    /// <summary>Optional MTL file for OBJ format models.</summary>
    public string? MtlFile { get; set; }

    /// <summary>Multi-model path (GARC). Null for single-model exporters.</summary>
    public List<ManifestModelEntry>? Models { get; set; }

    /// <summary>Texture file paths relative to the manifest directory.</summary>
    public List<string>? Textures { get; set; }

    /// <summary>Animation clip entries.</summary>
    public List<ManifestClipEntry>? Clips { get; set; }

    /// <summary>Rich texture metadata (GDB1 extension). Null for other exporters.</summary>
    public List<ManifestTextureEntry>? TextureDetails { get; set; }

    /// <summary>Source file tracking (GDB1 extension). Null for other exporters.</summary>
    public ManifestSourceInfo? Source { get; set; }
}
