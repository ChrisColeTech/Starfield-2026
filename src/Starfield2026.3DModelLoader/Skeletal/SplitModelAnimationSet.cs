#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Starfield2026.ModelLoader;

namespace Starfield2026.ModelLoader.Skeletal;

public class SplitModelAnimationSet
{
    public string ModelPath { get; }
    public SkeletonRig Skeleton { get; }
    public IReadOnlyDictionary<string, SkeletalAnimationClip> Clips { get; }
    public IReadOnlyDictionary<string, SkeletalAnimationClip> ClipsByTag { get; }

    public SplitModelAnimationSet(
        string modelPath,
        SkeletonRig skeleton,
        Dictionary<string, SkeletalAnimationClip> clips,
        Dictionary<string, SkeletalAnimationClip> clipsByTag)
    {
        ModelPath = modelPath;
        Skeleton = skeleton;
        Clips = clips;
        ClipsByTag = clipsByTag;
    }
}

public static class SplitModelAnimationSetLoader
{
    private static readonly (string tag, string[] patterns)[] TagPatterns = new[]
    {
        ("BattleIdle",  new[] { "battle_idle", "battle_wait", "battlewait" }),
        ("BattleAttack", new[] { "battle_attack", "attack01", "attack_01" }),
        ("BattleHit",   new[] { "battle_damage", "damage01", "hit01" }),
        ("BattleFaint", new[] { "battle_down", "down01", "faint" }),
        ("Run",         new[] { "run", "dash" }),
        ("Walk",        new[] { "walk" }),
        ("Idle",        new[] { "wait", "idle", "stand" }),
        ("Speak",       new[] { "speak", "talk" }),
        ("Turn",        new[] { "turn" }),
        ("Greet",       new[] { "greet", "hello" }),
        ("BallThrow",   new[] { "ballthrow", "ball_throw" }),
    };

    public static SplitModelAnimationSet Load(string groupFolderPath, string modelName = "model")
    {
        ModelLoaderLog.Info($"[AnimSet] Loading animation set from: {groupFolderPath}");
        string manifestPath = Path.Combine(groupFolderPath, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Manifest not found", manifestPath);

        using var stream = File.OpenRead(manifestPath);
        var manifest = JsonSerializer.Deserialize<ManifestData>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("Failed to parse manifest.json");

        string modelFile = manifest.ModelFile ?? (modelName + ".dae");
        string modelPath = Path.Combine(groupFolderPath, modelFile);
        ModelLoaderLog.Info($"[AnimSet] Manifest: version={manifest.Version}, format={manifest.Format}, model={modelFile}, clips={manifest.Clips?.Length ?? 0}");

        var skeleton = ColladaSkeletalLoader.LoadSkeleton(modelPath);

        var clips = new Dictionary<string, SkeletalAnimationClip>();
        var clipsByTag = new Dictionary<string, SkeletalAnimationClip>();

        if (manifest.Clips != null)
        {
            foreach (var entry in manifest.Clips)
            {
                string clipFile = entry.File ?? $"animations/clip_{entry.Index:D3}.dae";
                string clipPath = Path.Combine(groupFolderPath, clipFile);
                if (!File.Exists(clipPath)) continue;

                string clipId = entry.Id ?? entry.Name ?? $"clip_{entry.Index:D3}";
                string sourceName = entry.SourceName ?? entry.Name ?? clipId;

                var clip = ColladaSkeletalLoader.LoadClip(clipPath, skeleton, sourceName);
                clips[clipId] = clip;

                // Tag resolution: semanticName > pattern match on name > slot map from source anim number
                string? tag = entry.SemanticName;
                if (string.IsNullOrWhiteSpace(tag))
                    tag = ResolveTag(sourceName);
                if (string.IsNullOrWhiteSpace(tag))
                {
                    int sourceSlot = ParseSourceSlot(entry.Name, entry.Index);
                    tag = MapOverworldSlot(sourceSlot);
                }

                ModelLoaderLog.Info($"[AnimSet] Clip '{clipId}' (source='{sourceName}', idx={entry.Index}): tag={tag ?? "(none)"}, file={clipFile}");
                if (tag != null && !clipsByTag.ContainsKey(tag))
                    clipsByTag[tag] = clip;
            }
        }

        if (!clipsByTag.ContainsKey("Idle") && clips.Count > 0)
        {
            clipsByTag["Idle"] = clips.Values.First();
            ModelLoaderLog.Info("[AnimSet] No 'Idle' tag found, using first clip as Idle fallback");
        }

        ModelLoaderLog.Info($"[AnimSet] Animation set complete: {clips.Count} clips, {clipsByTag.Count} tagged");
        return new SplitModelAnimationSet(modelPath, skeleton, clips, clipsByTag);
    }

    private static string? ResolveTag(string sourceName)
    {
        string lower = sourceName.ToLowerInvariant();
        foreach (var (tag, patterns) in TagPatterns)
        {
            foreach (var pattern in patterns)
            {
                if (lower.Contains(pattern))
                    return tag;
            }
        }
        return null;
    }

    /// <summary>
    /// Parse the source animation slot number from a clip name like "anim_7" or "Motion_17".
    /// Falls back to the sequential clip index if parsing fails.
    /// </summary>
    private static int ParseSourceSlot(string? name, int fallbackIndex)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallbackIndex;

        // Try to extract number after last underscore: "anim_7" → 7, "Motion_17" → 17
        int lastUnderscore = name.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
        {
            if (int.TryParse(name.Substring(lastUnderscore + 1), out int slot))
                return slot;
        }
        return fallbackIndex;
    }

    /// <summary>
    /// Sun/Moon overworld character animation slots (GARC a/2/0/0).
    /// Slot numbers are sparse — not all characters have all slots.
    /// </summary>
    private static string? MapOverworldSlot(int slot) => slot switch
    {
        0   => "Idle",
        1   => "Walk",
        2   => "Run",
        4   => "Jump",
        5   => "Land",
        7   => "ShortAction1",
        8   => "LongAction1",
        9   => "ShortAction2",
        17  => "MediumAction",
        20  => "Action",
        23  => "Action2",
        30  => "ShortAction3",
        31  => "ShortAction4",
        52  => "IdleVariant",
        54  => "ShortAction5",
        55  => "LongAction2",
        56  => "ShortAction6",
        72  => "Action5",
        123 => "LongAction3",
        124 => "Action6",
        125 => "Action7",
        126 => "Action8",
        127 => "Action9",
        _ => null
    };

    private class ManifestData
    {
        public int Version { get; set; }
        public string? Format { get; set; }
        public string? ModelFile { get; set; }
        public string? AnimationMode { get; set; }
        public string[]? Textures { get; set; }
        public ClipEntry[]? Clips { get; set; }
    }

    private class ClipEntry
    {
        public int Index { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? SourceName { get; set; }
        public string? SemanticName { get; set; }
        public string? File { get; set; }
        public int FrameCount { get; set; }
        public int Fps { get; set; }
    }
}
