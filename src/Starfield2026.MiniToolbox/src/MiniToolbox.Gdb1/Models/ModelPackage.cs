namespace MiniToolbox.Gdb1.Models;

/// <summary>
/// Represents a complete model package with mesh, textures, and animations.
/// </summary>
public class ModelPackage
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Mesh? Mesh { get; set; }
    public List<TextureInfo> Textures { get; set; } = new();
    public List<ConstColorAnimation> Animations { get; set; } = new();
    public string SourceModelGdb { get; set; } = string.Empty;
    public string SourceModelBin { get; set; } = string.Empty;
}
