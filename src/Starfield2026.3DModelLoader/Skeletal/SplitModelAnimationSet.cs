#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Starfield2026.ModelLoader;

namespace Starfield2026.ModelLoader.Skeletal;

/// <summary>
/// How to source animation clips when loading a character.
/// </summary>
public enum AnimationLoadMode
{
    /// <summary>Load only the model's own clips. No shared animations.</summary>
    Own,
    /// <summary>Load own clips first, then fill missing tags from shared reference.</summary>
    FillMissing,
    /// <summary>Ignore own clips entirely. All animations come from shared reference.</summary>
    SharedOnly,
}

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
        ("Jump",        new[] { "jump", "leap" }),
        ("Land",        new[] { "land" }),
        ("Run",         new[] { "run", "dash" }),
        ("Walk",        new[] { "walk" }),
        ("Idle",        new[] { "wait", "idle", "stand" }),
        ("Speak",       new[] { "speak", "talk" }),
        ("Turn",        new[] { "turn" }),
        ("Greet",       new[] { "greet", "hello" }),
        ("BallThrow",   new[] { "ballthrow", "ball_throw" }),
    };

    /// <summary>
    /// Path to the reference character whose animations can be shared.
    /// Set once at startup by ModelLoaderGame.
    /// </summary>
    public static string? SharedAnimationFolder { get; set; }

    /// <summary>
    /// Controls how animations are loaded for all characters.
    /// </summary>
    public static AnimationLoadMode LoadMode { get; set; } = AnimationLoadMode.FillMissing;

    /// <summary>
    /// Which tags to fill from shared when using FillMissing mode.
    /// Ignored in Own and SharedOnly modes.
    /// </summary>
    public static HashSet<string> FillTags { get; set; } = new() { "Jump", "Land" };

    public static SplitModelAnimationSet Load(string groupFolderPath, string modelName = "model")
    {
        ModelLoaderLog.Info($"[AnimSet] Loading animation set from: {groupFolderPath} (mode={LoadMode})");
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

        // Step 1: Load own clips (unless SharedOnly)
        if (LoadMode != AnimationLoadMode.SharedOnly && manifest.Clips != null)
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

                string? tag = ResolveTagForEntry(entry, sourceName);
                ModelLoaderLog.Info($"[AnimSet] Clip '{clipId}' (source='{sourceName}', idx={entry.Index}): tag={tag ?? "(none)"}");
                if (tag != null && !clipsByTag.ContainsKey(tag))
                    clipsByTag[tag] = clip;
            }
        }

        // Step 2: Load from shared reference
        if (LoadMode == AnimationLoadMode.SharedOnly)
            LoadSharedClips(skeleton, clips, clipsByTag, tagsToFill: null); // null = load all
        else if (LoadMode == AnimationLoadMode.FillMissing)
            LoadSharedClips(skeleton, clips, clipsByTag, tagsToFill: FillTags);

        // Idle fallback
        if (!clipsByTag.ContainsKey("Idle") && clips.Count > 0)
        {
            clipsByTag["Idle"] = clips.Values.First();
            ModelLoaderLog.Info("[AnimSet] No 'Idle' tag found, using first clip as Idle fallback");
        }

        ModelLoaderLog.Info($"[AnimSet] Animation set complete: {clips.Count} clips, {clipsByTag.Count} tagged");
        return new SplitModelAnimationSet(modelPath, skeleton, clips, clipsByTag);
    }

    /// <summary>
    /// Load clips from the shared reference folder and retarget to the target skeleton.
    /// If tagsToFill is null, loads ALL shared clips.
    /// If tagsToFill is set, only loads clips whose tag is in the set AND not already present.
    /// </summary>
    private static void LoadSharedClips(
        SkeletonRig skeleton,
        Dictionary<string, SkeletalAnimationClip> clips,
        Dictionary<string, SkeletalAnimationClip> clipsByTag,
        HashSet<string>? tagsToFill)
    {
        if (string.IsNullOrEmpty(SharedAnimationFolder)) return;

        string refManifestPath = Path.Combine(SharedAnimationFolder, "manifest.json");
        if (!File.Exists(refManifestPath)) return;

        // If filling specific tags, check if any are actually missing
        if (tagsToFill != null)
        {
            bool anyMissing = false;
            foreach (var tag in tagsToFill)
            {
                if (!clipsByTag.ContainsKey(tag)) { anyMissing = true; break; }
            }
            if (!anyMissing) return;
        }

        ManifestData refManifest;
        using (var refStream = File.OpenRead(refManifestPath))
        {
            refManifest = JsonSerializer.Deserialize<ManifestData>(refStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new ManifestData();
        }
        if (refManifest.Clips == null) return;

        var boneMap = BoneMapping.GetMapForRig(skeleton);

        foreach (var entry in refManifest.Clips)
        {
            string sourceName = entry.SourceName ?? entry.Name ?? $"clip_{entry.Index:D3}";
            string? tag = ResolveTagForEntry(entry, sourceName);
            if (tag == null) continue;

            // Skip if already have this tag
            if (clipsByTag.ContainsKey(tag)) continue;

            // In FillMissing mode, only load tags in the fill set
            if (tagsToFill != null && !tagsToFill.Contains(tag)) continue;

            string clipFile = entry.File ?? $"clips/clip_{entry.Index:D3}.dae";
            string clipPath = Path.Combine(SharedAnimationFolder, clipFile);
            if (!File.Exists(clipPath)) continue;

            SkeletalAnimationClip clip;
            if (boneMap != null)
                clip = ColladaSkeletalLoader.LoadClipRetargeted(clipPath, skeleton, boneMap, sourceName);
            else
                clip = ColladaSkeletalLoader.LoadClip(clipPath, skeleton, sourceName);

            string clipId = $"shared_{tag.ToLowerInvariant()}";
            clips[clipId] = clip;
            clipsByTag[tag] = clip;
            ModelLoaderLog.Info($"[AnimSet] Shared '{tag}': {clip.Tracks.Count} tracks, remap={boneMap != null}");
        }
    }

    /// <summary>
    /// Resolve tag for a clip entry: semanticName > pattern match > slot map.
    /// </summary>
    private static string? ResolveTagForEntry(ClipEntry entry, string sourceName)
    {
        string? tag = entry.SemanticName;
        if (string.IsNullOrWhiteSpace(tag))
            tag = ResolveTag(sourceName);
        if (string.IsNullOrWhiteSpace(tag))
        {
            int sourceSlot = ParseSourceSlot(entry.Name, entry.Index);
            tag = MapOverworldSlot(sourceSlot);
        }
        return tag;
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

    private static int ParseSourceSlot(string? name, int fallbackIndex)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallbackIndex;

        int lastUnderscore = name.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
        {
            if (int.TryParse(name.Substring(lastUnderscore + 1), out int slot))
                return slot;
        }
        return fallbackIndex;
    }

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
