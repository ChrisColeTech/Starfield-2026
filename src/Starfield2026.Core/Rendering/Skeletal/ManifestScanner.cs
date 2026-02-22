#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Starfield2026.Core.Save;

namespace Starfield2026.Core.Rendering.Skeletal;

public static class ManifestScanner
{
    /// <summary>
    /// Ensures the characters table is in sync with manifest files on disk.
    /// Only rescans if the count of manifests on disk differs from the DB.
    /// </summary>
    public static void Sync(GameDatabase db, string modelsRoot)
    {
        if (!Directory.Exists(modelsRoot))
            return;

        var manifestPaths = new List<string>();
        foreach (string path in Directory.EnumerateFiles(modelsRoot, "manifest.json", SearchOption.AllDirectories))
        {
            // Only include manifests that have a clips array (skeletal models)
            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("clips", out _) ||
                    doc.RootElement.TryGetProperty("Clips", out _))
                {
                    manifestPaths.Add(path);
                }
            }
            catch { }
        }

        int dbCount = db.GetCharacterCount();
        if (dbCount == manifestPaths.Count)
            return;

        // Counts differ — rebuild
        var entries = new List<(string name, string category, string manifestPath)>();
        foreach (string manifestPath in manifestPaths)
        {
            string name = InferNameFromManifest(manifestPath);
            string folder = Path.GetDirectoryName(manifestPath) ?? "";
            string relative = Path.GetRelativePath(modelsRoot, folder).Replace('\\', '/');
            string[] parts = relative.Split('/');
            string category = parts.Length >= 3 ? parts[^3] : (parts.Length >= 2 ? parts[0] : "Default");

            entries.Add((name, category, manifestPath));
        }

        db.RebuildCharacters(entries);
    }

    /// <summary>
    /// Infers the character display name from the first clip's sourceName in the manifest.
    /// e.g. sourceName "tr0000_00_00533_speak02_start" → name "tr0000_00"
    /// Falls back to the folder name if no sourceName is found.
    /// </summary>
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

            // Extract prefix: split on '_' and take first two segments
            // e.g. "tr0000_00_00533_speak02_start" → "tr0000_00"
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
