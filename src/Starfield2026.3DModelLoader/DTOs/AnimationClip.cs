#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.DTOs;

public sealed record Keyframe(float Time, Matrix Transform);

public sealed class BoneTrack
{
    public int BoneIndex { get; }
    public IReadOnlyList<Keyframe> Keyframes { get; }

    public BoneTrack(int boneIndex, List<Keyframe> keyframes)
    {
        BoneIndex = boneIndex;
        Keyframes = keyframes;
    }

    public Matrix Sample(float time)
    {
        var frames = Keyframes;
        if (frames.Count == 0) return Matrix.Identity;
        if (frames.Count == 1 || time <= frames[0].Time) return frames[0].Transform;
        if (time >= frames[^1].Time) return frames[^1].Transform;

        int lo = 0, hi = frames.Count - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (frames[mid].Time <= time) lo = mid;
            else hi = mid;
        }

        var a = frames[lo];
        var b = frames[hi];
        float range = b.Time - a.Time;
        float t = range > 0 ? (time - a.Time) / range : 0f;

        a.Transform.Decompose(out var scaleA, out var rotA, out var transA);
        b.Transform.Decompose(out var scaleB, out var rotB, out var transB);

        return Matrix.CreateScale(Vector3.Lerp(scaleA, scaleB, t))
             * Matrix.CreateFromQuaternion(Quaternion.Slerp(rotA, rotB, t))
             * Matrix.CreateTranslation(Vector3.Lerp(transA, transB, t));
    }
}

public sealed class AnimationClip
{
    public string Name { get; }
    public float Duration { get; }
    public IReadOnlyList<BoneTrack> Tracks { get; }

    public AnimationClip(string name, float duration, List<BoneTrack> tracks)
    {
        Name = name;
        Duration = duration;
        Tracks = tracks;
    }
}
