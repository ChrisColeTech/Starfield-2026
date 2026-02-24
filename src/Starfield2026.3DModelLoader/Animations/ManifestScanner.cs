#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Starfield2026.ModelLoader.Animations;

public static class ManifestScanner
{
    public static List<(string name, string category, string subfolder, string manifestPath)> Scan(string modelsRoot)
    {
        var entries = new List<(string name, string category, string subfolder, string manifestPath)>();
        if (!Directory.Exists(modelsRoot))
            return entries;

        foreach (string path in Directory.EnumerateFiles(modelsRoot, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (!doc.RootElement.TryGetProperty("clips", out _) &&
                    !doc.RootElement.TryGetProperty("Clips", out _))
                    continue;
            }
            catch { continue; }

            string name = InferName(path);
            string folder = Path.GetDirectoryName(path) ?? "";
            string relative = Path.GetRelativePath(modelsRoot, folder).Replace('\\', '/');
            string[] parts = relative.Split('/');
            string category = parts.Length >= 1 ? parts[0] : "Default";
            string subfolder = parts.Length >= 3 ? parts[1] : "";

            if (string.Equals(category, "Maps", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(category, "SharedAnimations", StringComparison.OrdinalIgnoreCase))
                continue;

            entries.Add((name, category, subfolder, path));
        }

        return entries;
    }

    private static string InferName(string manifestPath)
    {
        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var doc = JsonDocument.Parse(stream);

            if (doc.RootElement.TryGetProperty("name", out var nameProp) ||
                doc.RootElement.TryGetProperty("Name", out nameProp))
            {
                string? name = nameProp.GetString();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            if (doc.RootElement.TryGetProperty("id", out var idProp) ||
                doc.RootElement.TryGetProperty("Id", out idProp))
            {
                string? id = idProp.GetString();
                if (!string.IsNullOrEmpty(id))
                    return id;
            }

            return FolderName(manifestPath);
        }
        catch
        {
            return FolderName(manifestPath);
        }
    }

    private static string FolderName(string manifestPath)
        => Path.GetFileName(Path.GetDirectoryName(manifestPath) ?? "");
}
