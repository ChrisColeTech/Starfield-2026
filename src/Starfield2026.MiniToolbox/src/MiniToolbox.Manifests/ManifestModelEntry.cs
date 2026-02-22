namespace MiniToolbox.Manifests;

/// <summary>
/// Model entry for multi-model groups (GARC).
/// </summary>
public class ManifestModelEntry
{
    public string? File { get; set; }
    public string? Name { get; set; }
    public int MeshCount { get; set; }
    public int BoneCount { get; set; }
}
