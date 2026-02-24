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

            string? tag = entry.SemanticName;
            if (string.IsNullOrWhiteSpace(tag))
                tag = TagResolver.FromName(sourceName);
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
                int underscoreIdx = fileName.LastIndexOf('_');
                if (underscoreIdx >= 0 && int.TryParse(fileName.AsSpan(underscoreIdx + 1), out int slot))
                    tag = TagResolver.FromSlot(slot);
            }
            if (tag == null || clipsByTag.ContainsKey(tag)) continue;
            if (tagsToFill != null && !tagsToFill.Contains(tag)) continue;

            var sourceSkeletonForRetarget = SkeletonLoader.Load(clipPath);
            var clip = ClipLoader.Load(clipPath, skeleton, fileName);
            clip = Retarget(clip, sourceSkeletonForRetarget, skeleton);
            clipsById[$"shared_{tag.ToLowerInvariant()}"] = clip;
            clipsByTag[tag] = clip;
        }
    }

    private static AnimationClip Retarget(AnimationClip clip, Skeleton source, Skeleton target)
    {
        var newTracks = new List<BoneTrack>(clip.Tracks.Count);
        foreach (var track in clip.Tracks)
        {
            if (track.BoneIndex < 0 || track.BoneIndex >= target.Bones.Count)
            {
                newTracks.Add(track);
                continue;
            }

            string boneName = target.Bones[track.BoneIndex].Name;
            if (!source.TryGetBoneIndex(boneName, out int srcIdx))
            {
                newTracks.Add(track);
                continue;
            }

            // Build rotation-only correction matrix: tgtBindRot * inverse(srcBindRot)
            var srcBind = source.BindLocalTransforms[srcIdx];
            var tgtBind = target.BindLocalTransforms[track.BoneIndex];

            var srcRotOnly = srcBind;
            srcRotOnly.M41 = 0; srcRotOnly.M42 = 0; srcRotOnly.M43 = 0;
            var tgtRotOnly = tgtBind;
            tgtRotOnly.M41 = 0; tgtRotOnly.M42 = 0; tgtRotOnly.M43 = 0;

            Matrix.Invert(ref srcRotOnly, out var srcRotInv);
            var correction = srcRotInv * tgtRotOnly;

            var tgtTranslation = tgtBind.Translation;

            var newFrames = new List<Keyframe>(track.Keyframes.Count);
            foreach (var kf in track.Keyframes)
            {
                // Strip translation from clip frame
                var clipRotOnly = kf.Transform;
                clipRotOnly.M41 = 0; clipRotOnly.M42 = 0; clipRotOnly.M43 = 0;

                // Apply correction: clip rotation remapped to target bind space
                var retargeted = clipRotOnly * correction;
                retargeted.M41 = tgtTranslation.X;
                retargeted.M42 = tgtTranslation.Y;
                retargeted.M43 = tgtTranslation.Z;

                newFrames.Add(new Keyframe(kf.Time, retargeted));
            }
            newTracks.Add(new BoneTrack(track.BoneIndex, newFrames));
        }
        return new AnimationClip(clip.Name, clip.Duration, newTracks);
    }

}
