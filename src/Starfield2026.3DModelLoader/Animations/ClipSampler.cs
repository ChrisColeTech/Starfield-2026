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

        // Strip root motion: lock translation to bind pose for root chain bones
        // (root and its direct children) so baked locomotion doesn't shift the model.
        if (skeleton != null)
        {
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                int parent = skeleton.Bones[i].ParentIndex;
                if (parent < 0 || skeleton.Bones[parent].ParentIndex < 0)
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
}
