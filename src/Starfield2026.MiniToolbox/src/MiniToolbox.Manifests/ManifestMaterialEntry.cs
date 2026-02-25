namespace MiniToolbox.Manifests;

/// <summary>
/// Material metadata for TRPAK manifest — shader, textures, and color parameters.
/// </summary>
public class ManifestMaterialEntry
{
    public string? Name { get; set; }
    public string? ShaderName { get; set; }
    public List<ManifestMaterialTexture>? Textures { get; set; }
    public List<ManifestMaterialVec4>? Vec4Params { get; set; }
    public List<ManifestMaterialFloat>? FloatParams { get; set; }
}

public class ManifestMaterialTexture
{
    public string? Name { get; set; }
    public string? File { get; set; }
}

public class ManifestMaterialVec4
{
    public string? Name { get; set; }
    public float W { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public class ManifestMaterialFloat
{
    public string? Name { get; set; }
    public float Value { get; set; }
}
