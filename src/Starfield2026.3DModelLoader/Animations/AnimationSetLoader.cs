#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.DTOs;
using Starfield2026.ModelLoader.Loaders;

namespace Starfield2026.ModelLoader.Animations;

public static class AnimationSetLoader
{
    public static AnimationSet Load(
        string folderPath,
        string modelName = "model",
        Func<string, Skeleton, string?>? resolveSharedFolder = null,
        AnimationLoadMode loadMode = AnimationLoadMode.FillMissing,
        HashSet<string>? fillTags = null)
    {
        string manifestPath = Path.Combine(folderPath, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Manifest not found", manifestPath);

        using var stream = File.OpenRead(manifestPath);
        var manifest = JsonSerializer.Deserialize<Manifest>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("Failed to parse manifest.json");

        string modelFile = manifest.ModelFile ?? (modelName + ".dae");
        string modelPath = Path.Combine(folderPath, modelFile);

        var skeleton = SkeletonLoader.Load(modelPath);
        var clipsById = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        var clipsByTag = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);

        string? sharedFolder = resolveSharedFolder?.Invoke(folderPath, skeleton);

        bool useShared = loadMode == AnimationLoadMode.SharedOnly && sharedFolder != null;
        if (!useShared && manifest.Clips != null)
            LoadOwnClips(folderPath, skeleton, manifest.Clips, clipsById, clipsByTag);

        if (loadMode != AnimationLoadMode.Own && sharedFolder != null)
        {
            var tagsToFill = loadMode == AnimationLoadMode.FillMissing ? fillTags : null;
            LoadSharedClips(sharedFolder, skeleton, clipsById, clipsByTag, tagsToFill);
        }

        if (!clipsByTag.ContainsKey("Idle") && clipsById.Count > 0)
            clipsByTag["Idle"] = clipsById.Values.First();

        return new AnimationSet(modelPath, skeleton, clipsById, clipsByTag);
    }

    private static void LoadOwnClips(
        string folderPath, Skeleton skeleton, ClipEntry[] entries,
        Dictionary<string, AnimationClip> clipsById,
        Dictionary<string, AnimationClip> clipsByTag)
    {
        foreach (var entry in entries)
        {
            string clipFile = entry.File ?? $"animations/clip_{entry.Index:D3}.dae";
            string clipPath = Path.Combine(folderPath, clipFile);
            if (!File.Exists(clipPath)) continue;

            string clipId = entry.Id ?? entry.Name ?? $"clip_{entry.Index:D3}";
            string sourceName = entry.SourceName ?? entry.Name ?? clipId;

            var clip = ClipLoader.Load(clipPath, skeleton, sourceName);
            clipsById[clipId] = clip;

            string? tag = ResolveTag(entry, sourceName);
            if (tag != null && !clipsByTag.ContainsKey(tag))
                clipsByTag[tag] = clip;
        }
    }

    private static void LoadSharedClips(
        string sharedFolder, Skeleton skeleton,
        Dictionary<string, AnimationClip> clipsById,
        Dictionary<string, AnimationClip> clipsByTag,
        HashSet<string>? tagsToFill)
    {
        string clipsDir = Path.Combine(sharedFolder, "clips");
        if (!Directory.Exists(clipsDir)) return;

        if (tagsToFill != null)
        {
            var missing = tagsToFill.Where(t => !clipsByTag.ContainsKey(t)).ToList();
            if (missing.Count == 0) return;
        }

        string[] clipFiles = Directory.GetFiles(clipsDir, "*.dae");
        Array.Sort(clipFiles, StringComparer.OrdinalIgnoreCase);

        foreach (string clipPath in clipFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(clipPath);
            string? tag = TagResolver.FromName(fileName);
            if (tag == null)
            {
                int slot = TagResolver.ParseSlotFromName(fileName, -1);
                if (slot >= 0) tag = TagResolver.FromSlot(slot);
            }
            if (tag == null || clipsByTag.ContainsKey(tag)) continue;
            if (tagsToFill != null && !tagsToFill.Contains(tag)) continue;

            var clip = ClipLoader.Load(clipPath, skeleton, fileName);
            clip = RetargetTranslations(clip, skeleton);
            clipsById[$"shared_{tag.ToLowerInvariant()}"] = clip;
            clipsByTag[tag] = clip;
        }
    }

    private static AnimationClip RetargetTranslations(AnimationClip clip, Skeleton skeleton)
    {
        var newTracks = new List<BoneTrack>(clip.Tracks.Count);
        foreach (var track in clip.Tracks)
        {
            if (track.BoneIndex < 0 || track.BoneIndex >= skeleton.Bones.Count)
            {
                newTracks.Add(track);
                continue;
            }

            var bindTranslation = skeleton.BindLocalTransforms[track.BoneIndex].Translation;
            var newFrames = new List<Keyframe>(track.Keyframes.Count);
            foreach (var kf in track.Keyframes)
            {
                kf.Transform.Decompose(out var scale, out var rotation, out _);
                var retargeted = Matrix.CreateScale(scale)
                               * Matrix.CreateFromQuaternion(rotation)
                               * Matrix.CreateTranslation(bindTranslation);
                newFrames.Add(new Keyframe(kf.Time, retargeted));
            }
            newTracks.Add(new BoneTrack(track.BoneIndex, newFrames));
        }
        return new AnimationClip(clip.Name, clip.Duration, newTracks);
    }

    private static string? ResolveTag(ClipEntry entry, string sourceName)
    {
        string? tag = entry.SemanticName;
        if (string.IsNullOrWhiteSpace(tag))
            tag = TagResolver.FromName(sourceName);
        if (string.IsNullOrWhiteSpace(tag))
        {
            int slot = TagResolver.ParseSlotFromName(entry.Name, entry.Index);
            tag = TagResolver.FromSlot(slot);
        }
        return tag;
    }
}
