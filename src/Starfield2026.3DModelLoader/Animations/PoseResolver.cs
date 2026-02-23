#nullable enable
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.DTOs;

namespace Starfield2026.ModelLoader.Animations;

public static class PoseResolver
{
    public static void Resolve(Skeleton skeleton, Matrix[] localPose, Matrix[] worldPose, Matrix[] skinPose)
    {
        var bones = skeleton.Bones;

        for (int i = 0; i < bones.Count; i++)
        {
            int parent = bones[i].ParentIndex;
            worldPose[i] = parent < 0
                ? localPose[i]
                : localPose[i] * worldPose[parent];

            skinPose[i] = skeleton.InverseBindTransforms[i] * worldPose[i];
        }
    }
}
