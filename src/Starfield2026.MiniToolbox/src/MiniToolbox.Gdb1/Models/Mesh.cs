namespace MiniToolbox.Gdb1.Models;

/// <summary>
/// Represents a 3D mesh extracted from a GDB1 model.
/// </summary>
public class Mesh
{
    public string Name { get; set; } = string.Empty;
    public List<Vertex> Vertices { get; set; } = new();
    public List<int> Indices { get; set; } = new();
    public List<string> TextureIds { get; set; } = new();

    public int TriangleCount => Indices.Count / 3;
}
