using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using MiniToolbox.Trpak.Decoders;
using MiniToolbox.Core.Texture;
using System.Numerics;

namespace MiniToolbox.Trpak.Exporters;

/// <summary>
/// Bakes eye albedo textures from EyeClearCoat material parameters.
/// In-game, the EyeClearCoat shader uses BaseColorLayer1-4 + LayerMaskMap
/// to composite the eye color at runtime. Since DAE/Blender don't support
/// this shader, we bake a flat albedo texture from the material data.
///
/// The compositing formula (from gftool EyeClearCoat.fsh):
///   layerMask = texture(LayerMaskMap, uv);
///   layerWeight = clamp(1.0 - dot(vec4(1.0), layerMask), 0, 1);
///   layerWeight = mix(layerWeight, 1.0, layerMask.r);
///   albedo = baseColor * layerWeight;
///
/// Additionally, we overlay emission colors for a more accurate bake:
///   emission += EmissionColorLayer1 * EmissionIntensityLayer1 * layerMask.r
///   emission += EmissionColorLayer3 * EmissionIntensityLayer3 * layerMask.b
///   emission += EmissionColorLayer5 * EmissionIntensityLayer5 * (1 - sum(mask))
/// </summary>
public static class EyeTextureBaker
{
    /// <summary>
    /// Check if a material uses the EyeClearCoat shader and can be baked.
    /// </summary>
    public static bool IsEyeMaterial(TrinityMaterial material)
        => string.Equals(material.ShaderName, "EyeClearCoat", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Bake an eye albedo texture and save it, replacing the blank placeholder.
    /// Returns the path to the baked texture, or null if baking fails.
    /// </summary>
    public static string? BakeEyeTexture(TrinityMaterial material, string tempRoot, string texOutDir)
    {
        if (!IsEyeMaterial(material)) return null;

        // Find the LayerMaskMap texture
        var lymRef = material.Textures.FirstOrDefault(t =>
            string.Equals(t.Name, "LayerMaskMap", StringComparison.OrdinalIgnoreCase));
        if (lymRef == null) return null;

        // Find the BNTX file for the layer mask
        string? lymBntxPath = FindBntxFile(lymRef.FilePath, tempRoot);
        if (lymBntxPath == null || !File.Exists(lymBntxPath)) return null;

        // Decode the layer mask texture
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
                Vector3 color = baseColors[0] * maskR      // Layer 1 (pupil)
                              + baseColors[1] * maskG      // Layer 2
                              + baseColors[2] * maskB      // Layer 3 (iris)
                              + baseColors[3] * maskA      // Layer 4
                              + Vector3.One * remainder;   // Remainder = white (sclera)

                // Add emission contribution (these provide the actual visible color in-game)
                Vector3 emission = emissionColors[0] * emissionIntensities[0] * maskR
                                 + emissionColors[1] * emissionIntensities[1] * maskG
                                 + emissionColors[2] * emissionIntensities[2] * maskB
                                 + emissionColors[3] * emissionIntensities[3] * maskA
                                 + emissionColors[4] * emissionIntensities[4] * remainder;

                // Combine: base color + full emission (the game relies on emission for eye visibility)
                color += emission;

                // Apply sRGB gamma correction (linear → sRGB) for correct display
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

        // Save the baked texture, overwriting the blank placeholder _alb
        string albFileName = GetAlbedoFileName(material);
        string outPath = Path.Combine(texOutDir, albFileName);
        result.SaveAsPng(outPath);

        Console.WriteLine($"  Baked eye texture: {Path.GetFileName(outPath)} ({width}x{height})");
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
        var albRef = material.Textures.FirstOrDefault(t =>
            string.Equals(t.Name, "BaseColorMap", StringComparison.OrdinalIgnoreCase));
        if (albRef != null)
        {
            // Return the filename with .png extension instead of .bntx
            string fileName = Path.GetFileNameWithoutExtension(albRef.FilePath);
            return fileName + ".png";
        }
        return material.Name + "_eye_baked.png";
    }

    private static string? FindBntxFile(string referencePath, string tempRoot)
    {
        // The reference path from the material might be relative or absolute
        // Try to find the .bntx file in the temp extraction directory
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

    /// <summary>
    /// Convert linear-space value to sRGB gamma.
    /// The standard sRGB transfer function.
    /// </summary>
    private static float LinearToSrgb(float linear)
    {
        if (linear <= 0.0031308f)
            return linear * 12.92f;
        return 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
    }
}
