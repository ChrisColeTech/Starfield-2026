#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.Skeletal;

public record AnimationKeyframe(float TimeSeconds, Matrix Transform);

public class BoneAnimationTrack
{
    public int BoneIndex { get; }
    public IReadOnlyList<AnimationKeyframe> Keyframes { get; }

    public BoneAnimationTrack(int boneIndex, List<AnimationKeyframe> keyframes)
    {
        BoneIndex = boneIndex;
        Keyframes = keyframes;
    }

    public Matrix Sample(float timeSeconds)
    {
        var frames = Keyframes;
        if (frames.Count == 0)
            return Matrix.Identity;
        if (frames.Count == 1 || timeSeconds <= frames[0].TimeSeconds)
            return frames[0].Transform;
        if (timeSeconds >= frames[^1].TimeSeconds)
            return frames[^1].Transform;

        // Find surrounding keyframes
        int lo = 0, hi = frames.Count - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (frames[mid].TimeSeconds <= timeSeconds)
                lo = mid;
            else
                hi = mid;
        }

        var a = frames[lo];
        var b = frames[hi];
        float range = b.TimeSeconds - a.TimeSeconds;
        float t = range > 0 ? (timeSeconds - a.TimeSeconds) / range : 0f;

        // Decompose, interpolate, recompose
        a.Transform.Decompose(out var scaleA, out var rotA, out var transA);
        b.Transform.Decompose(out var scaleB, out var rotB, out var transB);

        var scale = Vector3.Lerp(scaleA, scaleB, t);
        var rot = Quaternion.Slerp(rotA, rotB, t);
        var trans = Vector3.Lerp(transA, transB, t);

        return Matrix.CreateScale(scale)
             * Matrix.CreateFromQuaternion(rot)
             * Matrix.CreateTranslation(trans);
    }
}

public class SkeletalAnimationClip
{
    public string Name { get; }
    public float DurationSeconds { get; }
    public IReadOnlyList<BoneAnimationTrack> Tracks { get; }

    public SkeletalAnimationClip(string name, float durationSeconds, List<BoneAnimationTrack> tracks)
    {
        Name = name;
        DurationSeconds = durationSeconds;
        Tracks = tracks;
    }
}
