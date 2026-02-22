namespace MiniToolbox.Manifests;

/// <summary>
/// Animation clip entry in a manifest.
/// Union of all fields used across GARC, TRPAK, GDB1, and the 3DModelLoader consumer.
/// </summary>
public class ManifestClipEntry
{
    public int Index { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? SourceName { get; set; }
    public string? SemanticName { get; set; }
    public string? SemanticSource { get; set; }
    public string? File { get; set; }
    public int FrameCount { get; set; }
    public int Fps { get; set; }
    public int BoneCount { get; set; }

    /// <summary>Duration in seconds (GDB1 color animations).</summary>
    public float? Duration { get; set; }

    /// <summary>Number of animation tracks (GDB1 color animations).</summary>
    public int? TrackCount { get; set; }
}
