#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Starfield2026.ModelLoader.DTOs;

namespace Starfield2026.ModelLoader.Helpers;

public static class SharedAnimationResolver
{
    public static string DetectFamily(Skeleton skeleton, string? characterPath = null)
    {
        // Check folder path first for v2 variants
        if (!string.IsNullOrEmpty(characterPath))
        {
            string normalized = characterPath.Replace('\\', '/');
            if (normalized.Contains("/sun-moon-v2/", StringComparison.OrdinalIgnoreCase))
                return "sun-moon-v2";
        }

        if (skeleton.TryGetBoneIndex("Waist", out _) && skeleton.TryGetBoneIndex("LThigh", out _))
            return "sun-moon";
        if (skeleton.TryGetBoneIndex("waist", out _) && skeleton.TryGetBoneIndex("left_leg_01", out _))
        {
            if (skeleton.TryGetBoneIndex("foot_base", out _))
                return "scarlet";
            return "plza";
        }
        return "sun-moon";
    }

    public static string? Resolve(
        string characterFolderPath,
        Skeleton skeleton,
        Dictionary<string, string> folders)
    {
        var bodyType = TrainerGender.Classify(characterFolderPath);
        string bodyName = TrainerGender.GetSharedFolderName(bodyType);
        string family = DetectFamily(skeleton, characterFolderPath);

        string key = $"{family}/{bodyName}";
        if (folders.TryGetValue(key, out var folder))
            return folder;

        string fallbackBody = bodyType switch
        {
            TrainerGender.BodyType.Woman => "man",
            TrainerGender.BodyType.Man   => "woman",
            TrainerGender.BodyType.Boy   => "girl",
            TrainerGender.BodyType.Girl  => "boy",
            _ => "",
        };
        string fallbackKey = $"{family}/{fallbackBody}";
        if (folders.TryGetValue(fallbackKey, out folder))
            return folder;

        return null;
    }

    public static void ScanFolders(string sharedRoot, Dictionary<string, string> folders)
    {
        if (!Directory.Exists(sharedRoot)) return;

        foreach (var familyDir in Directory.GetDirectories(sharedRoot))
        {
            string familyName = Path.GetFileName(familyDir).ToLowerInvariant();
            foreach (var bodyDir in Directory.GetDirectories(familyDir))
            {
                string bodyName = Path.GetFileName(bodyDir).ToLowerInvariant();
                string clipsDir = Path.Combine(bodyDir, "clips");
                if (Directory.Exists(clipsDir))
                    folders[$"{familyName}/{bodyName}"] = bodyDir;
            }
        }
    }
}
