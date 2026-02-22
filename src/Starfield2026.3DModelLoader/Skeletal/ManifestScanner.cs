#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Starfield2026.ModelLoader.Skeletal;

public static class ManifestScanner
{
    /// <summary>
    /// Scans the models root directory for manifest.json files that contain skeletal clips.
    /// Returns a list of character entries (name, category, manifestPath).
    /// The caller is responsible for syncing these with a database.
    /// </summary>
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

            string name = InferNameFromManifest(path);
            string folder = Path.GetDirectoryName(path) ?? "";
            string relative = Path.GetRelativePath(modelsRoot, folder).Replace('\\', '/');
            string[] parts = relative.Split('/');
            string category = parts.Length >= 1 ? parts[0] : "Default";

            // Skip non-character folders (e.g. Maps)
            if (string.Equals(category, "Maps", StringComparison.OrdinalIgnoreCase))
                continue;

            entries.Add((name, category, path));
        }

        return entries;
    }

    private static string InferNameFromManifest(string manifestPath)
    {
        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var doc = JsonDocument.Parse(stream);

            JsonElement clips;
            if (!doc.RootElement.TryGetProperty("clips", out clips) &&
                !doc.RootElement.TryGetProperty("Clips", out clips))
                return FallbackName(manifestPath);

            if (clips.GetArrayLength() == 0)
                return FallbackName(manifestPath);

            var firstClip = clips[0];
            string? sourceName = null;
            if (firstClip.TryGetProperty("sourceName", out var sn))
                sourceName = sn.GetString();
            else if (firstClip.TryGetProperty("SourceName", out sn))
                sourceName = sn.GetString();

            if (string.IsNullOrEmpty(sourceName))
                return FallbackName(manifestPath);

            string[] segments = sourceName.Split('_');
            if (segments.Length >= 2)
                return segments[0] + "_" + segments[1];

            return segments[0];
        }
        catch
        {
            return FallbackName(manifestPath);
        }
    }

    private static string FallbackName(string manifestPath)
    {
        string folder = Path.GetDirectoryName(manifestPath) ?? "";
        return Path.GetFileName(folder);
    }
}
