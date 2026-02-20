namespace MiniToolbox.Gdb1;

/// <summary>
/// Tracks all GDB1 resources in a folder for cross-referencing.
/// </summary>
public class ResourceDatabase
{
    public string Folder { get; }
    public Dictionary<string, string> Models { get; } = new();       // id -> path
    public Dictionary<string, string> Textures { get; } = new();     // id -> path
    public Dictionary<string, string> Animations { get; } = new();   // id -> path

    public ResourceDatabase(string folder)
    {
        Folder = folder;
        Scan();
    }

    private void Scan()
    {
        if (!Directory.Exists(Folder))
            return;

        foreach (var file in Directory.EnumerateFiles(Folder))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var stem = Path.GetFileNameWithoutExtension(file);

            switch (ext)
            {
                case ".modelgdb":
                    Models[stem] = file;
                    break;
                case ".texturegdb":
                    Textures[stem] = file;
                    break;
                case ".constcoloranimgdb":
                    Animations[stem] = file;
                    break;
            }
        }
    }

    public HashSet<string> GetTextureIds() => Models.Keys.ToHashSet();
    public HashSet<string> GetAnimationIds() => Animations.Keys.ToHashSet();

    public string? GetModelPath(string id) => Models.GetValueOrDefault(id);
    public string? GetTexturePath(string id) => Textures.GetValueOrDefault(id);
    public string? GetAnimationPath(string id) => Animations.GetValueOrDefault(id);

    public int ModelCount => Models.Count;
    public int TextureCount => Textures.Count;
    public int AnimationCount => Animations.Count;
}
