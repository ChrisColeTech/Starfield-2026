#nullable enable
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.DTOs;

namespace Starfield2026.ModelLoader.Animations;

public static class ClipSampler
{
    public static void Sample(AnimationClip clip, float time, Matrix[] localPose, Matrix[] bindLocal)
    {
        for (int i = 0; i < bindLocal.Length; i++)
            localPose[i] = bindLocal[i];

        foreach (var track in clip.Tracks)
        {
            if (track.BoneIndex >= 0 && track.BoneIndex < localPose.Length)
                localPose[track.BoneIndex] = track.Sample(time);
        }
    }
}
