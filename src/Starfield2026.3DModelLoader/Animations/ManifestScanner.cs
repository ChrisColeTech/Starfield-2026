#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Starfield2026.ModelLoader.Animations;

public static class ManifestScanner
{
    public static List<(string name, string category, string manifestPath)> Scan(string modelsRoot)
    {
        var entries = new List<(string name, string category, string manifestPath)>();
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

            if (string.Equals(category, "Maps", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(category, "SharedAnimations", StringComparison.OrdinalIgnoreCase))
                continue;

            entries.Add((name, category, path));
        }

        return entries;
    }

    private static string InferName(string manifestPath)
    {
        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("clips", out var clips) &&
                !doc.RootElement.TryGetProperty("Clips", out clips))
                return FolderName(manifestPath);

            if (clips.GetArrayLength() == 0)
                return FolderName(manifestPath);

            var first = clips[0];
            string? sourceName = null;
            if (first.TryGetProperty("sourceName", out var sn))
                sourceName = sn.GetString();
            else if (first.TryGetProperty("SourceName", out sn))
                sourceName = sn.GetString();

            if (string.IsNullOrEmpty(sourceName))
                return FolderName(manifestPath);

            string[] segments = sourceName.Split('_');
            return segments.Length >= 2 ? segments[0] + "_" + segments[1] : segments[0];
        }
        catch
        {
            return FolderName(manifestPath);
        }
    }

    private static string FolderName(string manifestPath)
        => Path.GetFileName(Path.GetDirectoryName(manifestPath) ?? "");
}
