#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Starfield2026.ModelLoader.Rendering;

internal static class TextureResolver
{
    private static readonly XNamespace Col = "http://www.collada.org/2005/11/COLLADASchema";

    internal static Dictionary<string, string> ParseMaterialImageMap(XDocument doc)
    {
        var images = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var image in doc.Descendants(Col + "image"))
        {
            string? imageId = image.Attribute("id")?.Value;
            string? initFrom = image.Element(Col + "init_from")?.Value;
            if (!string.IsNullOrWhiteSpace(imageId) && !string.IsNullOrWhiteSpace(initFrom))
                images[imageId] = initFrom;
        }

        var effectToImage = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var effect in doc.Descendants(Col + "effect"))
        {
            string? effectId = effect.Attribute("id")?.Value;
            if (string.IsNullOrWhiteSpace(effectId)) continue;

            var surface = effect.Descendants(Col + "surface").FirstOrDefault();
            string? surfaceInitFrom = surface?.Element(Col + "init_from")?.Value;
            if (!string.IsNullOrWhiteSpace(surfaceInitFrom))
                effectToImage[effectId] = surfaceInitFrom;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var material in doc.Descendants(Col + "material"))
        {
            string? matId = material.Attribute("id")?.Value;
            string? effectUrl = material.Element(Col + "instance_effect")
                ?.Attribute("url")?.Value?.TrimStart('#');
            if (string.IsNullOrWhiteSpace(matId) || string.IsNullOrWhiteSpace(effectUrl))
                continue;

            if (effectToImage.TryGetValue(effectUrl, out string? imageId) &&
                images.TryGetValue(imageId, out string? filePath))
                result[matId] = filePath;
        }

        return result;
    }

    internal static Dictionary<string, string> ParseBindMaterialMap(XDocument doc)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var inst in doc.Descendants(Col + "instance_material"))
        {
            string? symbol = inst.Attribute("symbol")?.Value;
            string? target = inst.Attribute("target")?.Value?.TrimStart('#');
            if (!string.IsNullOrWhiteSpace(symbol) && !string.IsNullOrWhiteSpace(target))
                result[symbol] = target;
        }
        return result;
    }

    internal static string? ResolvePath(string baseDir, string imageFile)
    {
        string cleaned = imageFile.TrimStart('.', '/');
        string direct = Path.Combine(baseDir, cleaned);
        if (File.Exists(direct)) return direct;

        string inTextures = Path.Combine(baseDir, "textures", cleaned);
        if (File.Exists(inTextures)) return inTextures;

        if (!cleaned.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            string withPng = direct + ".png";
            if (File.Exists(withPng)) return withPng;
            withPng = inTextures + ".png";
            if (File.Exists(withPng)) return withPng;
        }

        return null;
    }
}
