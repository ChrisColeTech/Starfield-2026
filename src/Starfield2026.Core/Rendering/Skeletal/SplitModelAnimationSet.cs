#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Starfield2026.Core.Rendering.Skeletal;

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
    // Known tag patterns: order matters — first match wins
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
    };

    public static SplitModelAnimationSet Load(string groupFolderPath, string modelName = "model")
    {
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

        // Load skeleton
        var skeleton = ColladaSkeletalLoader.LoadSkeleton(modelPath);

        // Load clips
        var clips = new Dictionary<string, SkeletalAnimationClip>();
        var clipsByTag = new Dictionary<string, SkeletalAnimationClip>();

        if (manifest.Clips != null)
        {
            foreach (var entry in manifest.Clips)
            {
                string clipFile = entry.File ?? $"animations/clip_{entry.Index:D3}.dae";
                string clipPath = Path.Combine(groupFolderPath, clipFile);
                if (!File.Exists(clipPath)) continue;

                string clipId = entry.Id ?? $"clip_{entry.Index:D3}";
                string sourceName = entry.SourceName ?? clipId;

                var clip = ColladaSkeletalLoader.LoadClip(clipPath, skeleton, sourceName);
                clips[clipId] = clip;

                // Map to semantic tag
                string? tag = ResolveTag(sourceName);
                if (tag != null && !clipsByTag.ContainsKey(tag))
                    clipsByTag[tag] = clip;
            }
        }

        // If no Idle tag found, use the first clip
        if (!clipsByTag.ContainsKey("Idle") && clips.Count > 0)
            clipsByTag["Idle"] = clips.Values.First();

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

    // ─── Manifest JSON model ────────────────────────────────────────

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
        public string? SourceName { get; set; }
        public string? File { get; set; }
        public int FrameCount { get; set; }
        public int Fps { get; set; }
    }
}
