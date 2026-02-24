#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Starfield2026.ModelLoader.Animations;

public static class MapManifestScanner
{
    public static List<(string name, string category, string subfolder, string manifestPath)> Scan(string mapsRoot)
    {
        var entries = new List<(string name, string category, string subfolder, string manifestPath)>();
        if (!Directory.Exists(mapsRoot))
            return entries;

        foreach (string path in Directory.EnumerateFiles(mapsRoot, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (!doc.RootElement.TryGetProperty("modelFile", out _) &&
                    !doc.RootElement.TryGetProperty("ModelFile", out _))
                    continue;
            }
            catch { continue; }

            string name = InferName(path);
            string folder = Path.GetDirectoryName(path) ?? "";
            string relative = Path.GetRelativePath(mapsRoot, folder).Replace('\\', '/');
            string[] parts = relative.Split('/');

            // Maps/<source>/maps/<model> → category = source, subfolder = ""
            // Maps/<source>/maps/<sub>/<model> → category = source, subfolder = sub
            string category = parts.Length >= 1 ? parts[0] : "Default";
            string subfolder = parts.Length >= 4 ? parts[2] : "";

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
