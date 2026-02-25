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
    public static bool NeedsLayerBaking(TrinityMaterial material, bool verbose = false)
    {
        // EyeClearCoat is handled separately
        if (EyeTextureBaker.IsEyeMaterial(material))
            return false;

        bool hasLayerMask = material.Textures.Any(t =>
            string.Equals(t.Name, "LayerMaskMap", StringComparison.OrdinalIgnoreCase));
        if (!hasLayerMask)
            return false;

        // Multi-layer mode: BaseColorLayer1-4
        bool hasBaseColorLayer = material.Vec4Params.Any(p =>
            p.Name != null && p.Name.StartsWith("BaseColorLayer", StringComparison.OrdinalIgnoreCase));
        if (hasBaseColorLayer)
            return true;

        // Single-color tint mode: BaseColor (used by hair, skin, clothing palette variants)
        bool hasBaseColor = material.Vec4Params.Any(p =>
            string.Equals(p.Name, "BaseColor", StringComparison.OrdinalIgnoreCase));

        return hasBaseColor;
    }

    /// <summary>
    /// For albedo textures shared by multiple layered materials with DIFFERENT BaseColorLayer
    /// values, pick the best representative material to bake with. Returns a map of
    /// albedoFileName → chosen material name. Materials not in this map either aren't shared
    /// or all agree on colors (any material can bake). Materials in the map should only bake
    /// if they are the chosen representative.
    /// </summary>
    public static Dictionary<string, string> FindSharedAlbedoRepresentatives(IReadOnlyList<TrinityMaterial> materials)
    {
        // Group materials by their albedo texture filename
        var groups = new Dictionary<string, List<TrinityMaterial>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mat in materials)
        {
            if (!NeedsLayerBaking(mat)) continue;
            string albFileName = GetAlbedoFileName(mat);
            if (!groups.TryGetValue(albFileName, out var list))
            {
                list = new List<TrinityMaterial>();
                groups[albFileName] = list;
            }
            list.Add(mat);
        }

        var representatives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, mats) in groups)
        {
            if (mats.Count <= 1) continue;

            // Check if all materials in this group have the same BaseColorLayer values
            bool hasConflict = false;
            var refColors = ExtractBaseColors(mats[0]);
            for (int m = 1; m < mats.Count; m++)
            {
                var colors = ExtractBaseColors(mats[m]);
                for (int i = 0; i < 4; i++)
                {
                    if (Vector3.DistanceSquared(refColors[i], colors[i]) > 0.001f)
                    {
                        hasConflict = true;
                        goto doneChecking;
                    }
                }
            }
            doneChecking:

            if (!hasConflict) continue;

            // Pick the material with the most color energy (highest sum of color magnitudes).
            // This avoids picking a material with all-black layers (the pm1106 bug).
            TrinityMaterial best = mats[0];
            float bestEnergy = ColorEnergy(ExtractBaseColors(mats[0]));
            for (int m = 1; m < mats.Count; m++)
            {
                float energy = ColorEnergy(ExtractBaseColors(mats[m]));
                if (energy > bestEnergy)
                {
                    bestEnergy = energy;
                    best = mats[m];
                }
            }
            representatives[name] = best.Name;
        }
        return representatives;
    }

    private static float ColorEnergy(Vector3[] colors)
    {
        float sum = 0;
        foreach (var c in colors)
            sum += c.LengthSquared();
        return sum;
    }

    /// <summary>
    /// Bake the layered material's albedo from LayerMaskMap + color parameters.
    /// Each material gets its own per-material baked file ({materialName}_alb.png).
    /// Patches the material's BaseColorMap TextureRef to point to the new file
    /// so the DAE exporter picks up the correct texture for each submesh.
    /// Returns the path to the baked texture, or null if baking fails/skipped.
    /// </summary>
    public static string? BakeLayeredTexture(TrinityMaterial material, string tempRoot, string texOutDir,
        Dictionary<string, string>? sharedAlbedoReps = null, HashSet<string>? alreadyBaked = null)
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

        // Extract BaseColor param (layer 0 / remainder color)
        var baseColorParam = material.Vec4Params.FirstOrDefault(p =>
            string.Equals(p.Name, "BaseColor", StringComparison.OrdinalIgnoreCase));
        Vector3 baseColor0 = baseColorParam?.Value != null
            ? new Vector3(baseColorParam.Value.W, baseColorParam.Value.X, baseColorParam.Value.Y)
            : Vector3.One;

        // Check if there's anything to bake (base colors OR emission with intensity)
        bool hasAnyBaseColor = false;
        for (int i = 0; i < 4; i++)
        {
            if (baseColors[i].LengthSquared() > 0.001f)
            {
                hasAnyBaseColor = true;
                break;
            }
        }

        bool hasAnyEmission = false;
        for (int i = 0; i < 5; i++)
        {
            if (emissionColors[i].LengthSquared() > 0.001f && emissionIntensities[i] > 0.001f)
            {
                hasAnyEmission = true;
                break;
            }
        }

        bool hasAnyColor = hasAnyBaseColor || hasAnyEmission;

        // Emission-dominant: base layers are all black but emission has color.
        // In this case, use max(base, emission) instead of additive to prevent
        // white remainder from washing out the emission colors.
        bool emissionDominant = !hasAnyBaseColor && hasAnyEmission;

        // Per-material filename
        string outFileName = SanitizeFileName(material.Name) + "_alb.png";
        string outPath = Path.Combine(texOutDir, outFileName);

        if (!hasAnyColor)
        {
            // All layers are black with no emission — don't bake
            return null;
        }

        // Load original albedo BNTX as the base layer for the remainder.
        // The compositing formula uses the original texture where the mask is zero,
        // and layer colors where the mask has values.
        Image<Rgba32>? albedoImage = null;
        var albRef = material.Textures.FirstOrDefault(t =>
            string.Equals(t.Name, "BaseColorMap", StringComparison.OrdinalIgnoreCase));
        if (albRef != null)
        {
            string? albBntxPath = FindBntxFile(albRef.FilePath, tempRoot);
            if (albBntxPath != null && File.Exists(albBntxPath))
                albedoImage = DecodeBntxToImage(albBntxPath);
        }

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

                // Sample original albedo pixel for remainder (unmasked areas keep original texture)
                Vector3 remainderColor;
                if (albedoImage != null)
                {
                    // Map to albedo image coordinates (may differ in resolution)
                    int ax = x * albedoImage.Width / width;
                    int ay = y * albedoImage.Height / height;
                    ax = Math.Min(ax, albedoImage.Width - 1);
                    ay = Math.Min(ay, albedoImage.Height - 1);
                    var albPixel = albedoImage[ax, ay];
                    remainderColor = new Vector3(albPixel.R / 255f, albPixel.G / 255f, albPixel.B / 255f);
                }
                else
                {
                    remainderColor = baseColor0;
                }

                // Blend: layer colors where masked, original albedo where unmasked
                Vector3 color = baseColors[0] * maskR
                              + baseColors[1] * maskG
                              + baseColors[2] * maskB
                              + baseColors[3] * maskA
                              + remainderColor * remainder;

                // Add emission contribution
                Vector3 emission = emissionColors[0] * emissionIntensities[0] * maskR
                                 + emissionColors[1] * emissionIntensities[1] * maskG
                                 + emissionColors[2] * emissionIntensities[2] * maskB
                                 + emissionColors[3] * emissionIntensities[3] * maskA
                                 + emissionColors[4] * emissionIntensities[4] * remainder;

                color += emission;
                color = Vector3.Clamp(color, Vector3.Zero, Vector3.One);

                result[x, y] = new Rgba32(
                    (byte)(color.X * 255),
                    (byte)(color.Y * 255),
                    (byte)(color.Z * 255),
                    255);
            }
        }

        maskImage.Dispose();
        albedoImage?.Dispose();

        result.SaveAsPng(outPath);

        // Patch the material's BaseColorMap texture reference to point to the baked file
        var albRefPatch = material.Textures.FirstOrDefault(t =>
            string.Equals(t.Name, "BaseColorMap", StringComparison.OrdinalIgnoreCase));
        if (albRefPatch != null)
            albRefPatch.FilePath = Path.GetFileNameWithoutExtension(outFileName);

        Console.WriteLine($"  Baked layer texture: {outFileName} ({width}x{height}) [{material.ShaderName}]");
        return outPath;
    }

    private static string SanitizeFileName(string name)
    {
        // Replace any invalid filename chars
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static Vector3[] ExtractBaseColors(TrinityMaterial material)
    {
        var colors = new Vector3[4];

        // Try multi-layer mode first: BaseColorLayer1-4
        bool hasMultiLayer = false;
        for (int i = 0; i < 4; i++)
        {
            string paramName = $"BaseColorLayer{i + 1}";
            var param = material.Vec4Params.FirstOrDefault(p =>
                string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
            if (param?.Value != null)
            {
                colors[i] = new Vector3(param.Value.W, param.Value.X, param.Value.Y);
                hasMultiLayer = true;
            }
            else
            {
                colors[i] = Vector3.Zero;
            }
        }

        // Fallback: single BaseColor tint (hair, skin, clothing)
        // In single-color mode, the mask R channel drives the tint blend
        if (!hasMultiLayer)
        {
            var baseColor = material.Vec4Params.FirstOrDefault(p =>
                string.Equals(p.Name, "BaseColor", StringComparison.OrdinalIgnoreCase));
            if (baseColor?.Value != null)
                colors[0] = new Vector3(baseColor.Value.W, baseColor.Value.X, baseColor.Value.Y);
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

    internal static string GetAlbedoFileName(TrinityMaterial material)
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

            float totalLuminance = 0;

            for (int y = 0; y < h; y += stepY)
            {
                for (int x = 0; x < w; x += stepX)
                {
                    var px = img[x, y];
                    totalSampled++;
                    // Luminance (perceptual)
                    totalLuminance += (px.R * 0.299f + px.G * 0.587f + px.B * 0.114f) / 255f;
                }
            }

            // Average luminance > 0.85 means the texture is effectively a white/near-white placeholder
            // (real textured albedos like skin, clothing, fire have much lower average luminance)
            float avgLuminance = totalSampled > 0 ? totalLuminance / totalSampled : 1f;
            return avgLuminance > 0.85f;
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

    private static float GetFloatParam(TrinityMaterial material, string name)
    {
        var param = material.FloatParams.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return param?.Value ?? 0f;
    }

    /// <summary>
    /// Apply hue shift to turn white/gray albedo into colored body parts.
    /// The game shader uses HueShiftBias + MidAreaHueOffset/DarkAreaHueOffset
    /// to tint the base albedo at runtime. We bake this into the texture.
    /// The shift uses luminance to blend between mid and dark area offsets,
    /// and forces saturation on near-gray pixels so the hue is visible.
    /// </summary>
    private static Vector3 ApplyHueShift(Vector3 rgb, float bias, float midHueOffset, float midShift,
                                          float darkHueOffset, float darkShift)
    {
        // Convert RGB to HSL
        float r = rgb.X, g = rgb.Y, b = rgb.Z;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float l = (max + min) * 0.5f;
        float s = 0f;
        float h = 0f;

        if (max != min)
        {
            float d = max - min;
            s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

            if (max == r)
                h = ((g - b) / d + (g < b ? 6f : 0f)) / 6f;
            else if (max == g)
                h = ((b - r) / d + 2f) / 6f;
            else
                h = ((r - g) / d + 4f) / 6f;
        }

        // Blend between mid and dark hue offsets based on luminance
        // Dark (l<0.3) = dark offset, Mid (l~0.5) = mid offset, Light = mid offset
        float t = Math.Clamp((l - 0.2f) / 0.3f, 0f, 1f); // 0=dark, 1=mid/light
        float hueOffset = darkHueOffset * (1f - t) + midHueOffset * t;
        float satShift = darkShift * (1f - t) + midShift * t;

        // Apply hue shift (offset is in degrees, convert to 0-1 range)
        h += (hueOffset / 360f) * bias;
        h = h % 1f;
        if (h < 0) h += 1f;

        // Apply saturation shift
        s = Math.Clamp(s + satShift * bias, 0f, 1f);

        // Force saturation on near-gray pixels so hue shift produces visible color
        if (s < 0.1f && bias > 0.1f)
            s = Math.Clamp(bias * 0.5f, 0f, 1f);

        // Convert HSL back to RGB
        return HslToRgb(h, s, l);
    }

    private static Vector3 HslToRgb(float h, float s, float l)
    {
        if (s < 0.001f)
            return new Vector3(l, l, l);

        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;

        float r = HueToRgb(p, q, h + 1f / 3f);
        float g = HueToRgb(p, q, h);
        float b = HueToRgb(p, q, h - 1f / 3f);

        return new Vector3(
            Math.Clamp(r, 0f, 1f),
            Math.Clamp(g, 0f, 1f),
            Math.Clamp(b, 0f, 1f));
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }
}
