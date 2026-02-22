using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using MiniToolbox.Trpak.Decoders;
using MiniToolbox.Core.Texture;
using System.Numerics;

namespace MiniToolbox.Trpak.Exporters;

/// <summary>
/// Bakes layered material albedo textures from LayerMaskMap + BaseColorLayerN parameters.
/// Generalizes the EyeClearCoat compositing formula to all Trinity materials that use
/// the layer mask pattern (fire, hair, SSS, transparent, etc.).
///
/// The compositing formula (same as EyeClearCoat, from gftool shaders):
///   layerMask = texture(LayerMaskMap, uv);  // RGBA = 4 layer weights
///   remainder = clamp(1.0 - dot(vec4(1.0), layerMask), 0, 1);
///   color = BaseColorLayer1*mask.r + BaseColorLayer2*mask.g
///         + BaseColorLayer3*mask.b + BaseColorLayer4*mask.a
///         + white*remainder;
///   emission += EmissionColorLayerN * EmissionIntensityN * maskChannel[N]
///   final = sRGB(clamp(color + emission, 0, 1))
/// </summary>
public static class TrinityTextureBaker
{
    /// <summary>
    /// Check if a material uses layered compositing and needs baking.
    /// Returns true if it has a LayerMaskMap texture + BaseColorLayer params
    /// and is NOT EyeClearCoat (handled by EyeTextureBaker).
    /// </summary>
    public static bool NeedsLayerBaking(TrinityMaterial material)
    {
        // EyeClearCoat is handled separately
        if (EyeTextureBaker.IsEyeMaterial(material))
            return false;

        bool hasLayerMask = material.Textures.Any(t =>
            string.Equals(t.Name, "LayerMaskMap", StringComparison.OrdinalIgnoreCase));
        if (!hasLayerMask)
            return false;

        bool hasBaseColorLayer = material.Vec4Params.Any(p =>
            p.Name != null && p.Name.StartsWith("BaseColorLayer", StringComparison.OrdinalIgnoreCase));

        return hasBaseColorLayer;
    }

    /// <summary>
    /// Bake the layered material's albedo from LayerMaskMap + color parameters.
    /// Overwrites the existing BaseColorMap PNG with the composited result.
    /// Returns the path to the baked texture, or null if baking fails.
    /// </summary>
    public static string? BakeLayeredTexture(TrinityMaterial material, string tempRoot, string texOutDir)
    {
        if (!NeedsLayerBaking(material)) return null;

        // Find the LayerMaskMap texture
        var lymRef = material.Textures.FirstOrDefault(t =>
            string.Equals(t.Name, "LayerMaskMap", StringComparison.OrdinalIgnoreCase));
        if (lymRef == null) return null;

        // Find and decode the BNTX file for the layer mask
        string? lymBntxPath = FindBntxFile(lymRef.FilePath, tempRoot);
        if (lymBntxPath == null || !File.Exists(lymBntxPath)) return null;

        Image<Rgba32>? maskImage = DecodeBntxToImage(lymBntxPath);
        if (maskImage == null) return null;

        // Extract material color parameters
        var baseColors = ExtractBaseColors(material);
        var emissionColors = ExtractEmissionColors(material);
        var emissionIntensities = ExtractEmissionIntensities(material);

        // Bake the composited texture
        int width = maskImage.Width;
        int height = maskImage.Height;
        using var result = new Image<Rgba32>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var maskPixel = maskImage[x, y];
                float maskR = maskPixel.R / 255f; // Layer 1
                float maskG = maskPixel.G / 255f; // Layer 2
                float maskB = maskPixel.B / 255f; // Layer 3
                float maskA = maskPixel.A / 255f; // Layer 4

                // Shader compositing formula
                float maskSum = maskR + maskG + maskB + maskA;
                float remainder = Math.Clamp(1f - maskSum, 0f, 1f);

                // Blend base colors using mask channels (linear space)
                Vector3 color = baseColors[0] * maskR
                              + baseColors[1] * maskG
                              + baseColors[2] * maskB
                              + baseColors[3] * maskA
                              + Vector3.One * remainder;

                // Add emission contribution
                Vector3 emission = emissionColors[0] * emissionIntensities[0] * maskR
                                 + emissionColors[1] * emissionIntensities[1] * maskG
                                 + emissionColors[2] * emissionIntensities[2] * maskB
                                 + emissionColors[3] * emissionIntensities[3] * maskA
                                 + emissionColors[4] * emissionIntensities[4] * remainder;

                color += emission;

                // Apply sRGB gamma correction (linear → sRGB)
                color = new Vector3(
                    LinearToSrgb(color.X),
                    LinearToSrgb(color.Y),
                    LinearToSrgb(color.Z));

                color = Vector3.Clamp(color, Vector3.Zero, Vector3.One);

                result[x, y] = new Rgba32(
                    (byte)(color.X * 255),
                    (byte)(color.Y * 255),
                    (byte)(color.Z * 255),
                    255);
            }
        }

        maskImage.Dispose();

        // Save the baked texture — but only overwrite if existing albedo is a blank placeholder
        string albFileName = GetAlbedoFileName(material);
        string outPath = Path.Combine(texOutDir, albFileName);

        if (File.Exists(outPath) && !IsBlankAlbedo(outPath))
        {
            // Existing albedo has real content (skin, clothing, etc.) — don't overwrite
            Console.WriteLine($"  Skipped layer bake: {Path.GetFileName(outPath)} (albedo has content) [{material.ShaderName}]");
            return null;
        }

        result.SaveAsPng(outPath);

        Console.WriteLine($"  Baked layer texture: {Path.GetFileName(outPath)} ({width}x{height}) [{material.ShaderName}]");
        return outPath;
    }

    private static Vector3[] ExtractBaseColors(TrinityMaterial material)
    {
        var colors = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            string paramName = $"BaseColorLayer{i + 1}";
            var param = material.Vec4Params.FirstOrDefault(p =>
                string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
            if (param?.Value != null)
                colors[i] = new Vector3(param.Value.W, param.Value.X, param.Value.Y);
            else
                colors[i] = Vector3.Zero;
        }
        return colors;
    }

    private static Vector3[] ExtractEmissionColors(TrinityMaterial material)
    {
        var colors = new Vector3[5];
        string[] names = { "EmissionColorLayer1", "EmissionColorLayer2", "EmissionColorLayer3",
                           "EmissionColorLayer4", "EmissionColorLayer5" };
        for (int i = 0; i < 5; i++)
        {
            var param = material.Vec4Params.FirstOrDefault(p =>
                string.Equals(p.Name, names[i], StringComparison.OrdinalIgnoreCase));
            if (param?.Value != null)
                colors[i] = new Vector3(param.Value.W, param.Value.X, param.Value.Y);
            else
                colors[i] = Vector3.Zero;
        }
        return colors;
    }

    private static float[] ExtractEmissionIntensities(TrinityMaterial material)
    {
        var intensities = new float[5];
        string[] names = { "EmissionIntensityLayer1", "EmissionIntensityLayer2", "EmissionIntensityLayer3",
                           "EmissionIntensityLayer4", "EmissionIntensityLayer5" };
        for (int i = 0; i < 5; i++)
        {
            var param = material.FloatParams.FirstOrDefault(p =>
                string.Equals(p.Name, names[i], StringComparison.OrdinalIgnoreCase));
            if (param != null)
                intensities[i] = param.Value;
        }
        return intensities;
    }

    private static string GetAlbedoFileName(TrinityMaterial material)
    {
        // Try to find the BaseColorMap texture reference
        var albRef = material.Textures.FirstOrDefault(t =>
            string.Equals(t.Name, "BaseColorMap", StringComparison.OrdinalIgnoreCase));
        if (albRef != null)
        {
            string fileName = Path.GetFileNameWithoutExtension(albRef.FilePath);
            return fileName + ".png";
        }
        return material.Name + "_layerbaked.png";
    }

    /// <summary>
    /// Check if an existing albedo PNG is a blank placeholder (mostly white/near-white).
    /// Returns true if the texture is blank and safe to overwrite with baked layer data.
    /// Samples pixels across the image to avoid reading every pixel.
    /// </summary>
    private static bool IsBlankAlbedo(string pngPath)
    {
        try
        {
            using var img = Image.Load<Rgba32>(pngPath);
            int w = img.Width, h = img.Height;
            if (w == 0 || h == 0) return true;

            // Sample up to 64 pixels in a grid pattern
            int stepX = Math.Max(1, w / 8);
            int stepY = Math.Max(1, h / 8);
            int totalSampled = 0;
            int whiteSampled = 0;

            for (int y = 0; y < h; y += stepY)
            {
                for (int x = 0; x < w; x += stepX)
                {
                    var px = img[x, y];
                    totalSampled++;
                    // Consider "white" if all channels >= 250
                    if (px.R >= 250 && px.G >= 250 && px.B >= 250)
                        whiteSampled++;
                }
            }

            // If >90% of sampled pixels are white, it's a blank placeholder
            return totalSampled > 0 && (float)whiteSampled / totalSampled > 0.9f;
        }
        catch
        {
            // If we can't read it, assume it's safe to overwrite
            return true;
        }
    }

    private static string? FindBntxFile(string referencePath, string tempRoot)
    {
        string fileName = Path.GetFileName(referencePath);
        if (!fileName.EndsWith(".bntx", StringComparison.OrdinalIgnoreCase))
            fileName += ".bntx";

        foreach (string file in Directory.EnumerateFiles(tempRoot, "*.bntx", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                return file;
        }
        return null;
    }

    private static Image<Rgba32>? DecodeBntxToImage(string bntxPath)
    {
        try
        {
            var bntxBytes = File.ReadAllBytes(bntxPath);
            var decoded = BntxDecoder.Decode(bntxBytes);
            if (decoded == null || decoded.Count == 0) return null;

            var tex = decoded[0];
            return Image.LoadPixelData<Rgba32>(tex.RgbaData, tex.Width, tex.Height);
        }
        catch
        {
            return null;
        }
    }

    private static float LinearToSrgb(float linear)
    {
        if (linear <= 0.0031308f)
            return linear * 12.92f;
        return 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
    }
}
