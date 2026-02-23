#nullable enable
using System.Collections.Generic;
using Starfield2026.ModelLoader.DTOs;

namespace Starfield2026.ModelLoader.Animations;

public sealed class AnimationSet
{
    public string ModelPath { get; }
    public Skeleton Skeleton { get; }
    public IReadOnlyDictionary<string, AnimationClip> ClipsById { get; }
    public IReadOnlyDictionary<string, AnimationClip> ClipsByTag { get; }

    public AnimationSet(
        string modelPath,
        Skeleton skeleton,
        Dictionary<string, AnimationClip> clipsById,
        Dictionary<string, AnimationClip> clipsByTag)
    {
        ModelPath = modelPath;
        Skeleton = skeleton;
        ClipsById = clipsById;
        ClipsByTag = clipsByTag;
    }

    public bool HasTag(string tag) => ClipsByTag.ContainsKey(tag);

    public AnimationClip? GetByTag(string tag)
        => ClipsByTag.TryGetValue(tag, out var clip) ? clip : null;
}
