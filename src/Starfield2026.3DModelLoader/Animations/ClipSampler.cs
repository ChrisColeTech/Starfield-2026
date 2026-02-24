#nullable enable
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.DTOs;

namespace Starfield2026.ModelLoader.Animations;

public static class ClipSampler
{
    public static void Sample(AnimationClip clip, float time, Matrix[] localPose, Matrix[] bindLocal,
        Skeleton? skeleton = null)
    {
        for (int i = 0; i < bindLocal.Length; i++)
            localPose[i] = bindLocal[i];

        foreach (var track in clip.Tracks)
        {
            if (track.BoneIndex >= 0 && track.BoneIndex < localPose.Length)
                localPose[track.BoneIndex] = track.Sample(time);
        }

        // Strip root motion: lock translation to bind pose for the root chain.
        // The root chain is every bone from the root down through single-child
        // nodes until the first branch (e.g. root → origin → waist in PZLA).
        if (skeleton != null)
        {
            foreach (int i in skeleton.RootChain)
            {
                var pose = localPose[i];
                var bind = bindLocal[i];
                pose.M41 = bind.M41;
                pose.M42 = bind.M42;
                pose.M43 = bind.M43;
                localPose[i] = pose;
            }
        }
    }
}
