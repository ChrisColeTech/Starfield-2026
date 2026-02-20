namespace MiniToolbox.App.Commands;

/// <summary>
/// Supported output model formats.
/// </summary>
public enum ModelFormat
{
    Obj,
    Dae,
    // Fbx - planned for future
}

/// <summary>
/// Animation export mode.
/// </summary>
public enum AnimationMode
{
    Split,  // Clip-only files (no embedded geometry)
    Baked   // Full geometry per clip
}

public static class ModelFormatExtensions
{
    public static string GetExtension(this ModelFormat format) => format switch
    {
        ModelFormat.Obj => ".obj",
        ModelFormat.Dae => ".dae",
        _ => ".obj"
    };

    public static ModelFormat Parse(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return ModelFormat.Obj;

        return value.ToLowerInvariant() switch
        {
            "obj" => ModelFormat.Obj,
            "dae" => ModelFormat.Dae,
            "collada" => ModelFormat.Dae,
            _ => throw new ArgumentException($"Unknown model format: {value}. Supported: obj, dae")
        };
    }
}
