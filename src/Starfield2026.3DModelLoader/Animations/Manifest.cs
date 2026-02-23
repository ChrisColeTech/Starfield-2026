#nullable enable

namespace Starfield2026.ModelLoader.Animations;

internal sealed class Manifest
{
    public int Version { get; set; }
    public string? Format { get; set; }
    public string? ModelFile { get; set; }
    public string? AnimationMode { get; set; }
    public string[]? Textures { get; set; }
    public ClipEntry[]? Clips { get; set; }
}

internal sealed class ClipEntry
{
    public int Index { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? SourceName { get; set; }
    public string? SemanticName { get; set; }
    public string? File { get; set; }
    public int FrameCount { get; set; }
    public int Fps { get; set; }
}
