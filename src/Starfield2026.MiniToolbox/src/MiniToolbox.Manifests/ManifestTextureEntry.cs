namespace MiniToolbox.Manifests;

/// <summary>
/// Rich texture metadata (GDB1 extension).
/// </summary>
public class ManifestTextureEntry
{
    public string? Name { get; set; }
    public string? File { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Format { get; set; }
    public int Size { get; set; }
}
