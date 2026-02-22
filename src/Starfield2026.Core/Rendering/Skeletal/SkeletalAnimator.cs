#nullable enable
using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Rendering.Skeletal;

public class SkeletalAnimator
{
    public SkeletonRig Rig { get; }
    public SkeletalAnimationClip? ActiveClip { get; private set; }
    public bool Loop { get; set; }
    public float CurrentTimeSeconds { get; private set; }
    public Matrix[] LocalPose { get; }
    public Matrix[] WorldPose { get; }
    public Matrix[] SkinPose { get; }

    public SkeletalAnimator(SkeletonRig rig)
    {
        Rig = rig;
        int count = rig.Bones.Count;
        LocalPose = new Matrix[count];
        WorldPose = new Matrix[count];
        SkinPose = new Matrix[count];
        ResetToBindPose();
    }

    public void Play(SkeletalAnimationClip clip, bool loop = true, bool resetTime = true)
    {
        ActiveClip = clip;
        Loop = loop;
        if (resetTime)
            CurrentTimeSeconds = 0f;
    }

    public void Stop()
    {
        ActiveClip = null;
        CurrentTimeSeconds = 0f;
        ResetToBindPose();
    }

    public void Update(float deltaSeconds)
    {
        var clip = ActiveClip;
        if (clip == null || clip.DurationSeconds <= 0f)
            return;

        // Advance time
        CurrentTimeSeconds += deltaSeconds;
        if (Loop)
        {
            if (CurrentTimeSeconds >= clip.DurationSeconds)
                CurrentTimeSeconds %= clip.DurationSeconds;
        }
        else
        {
            if (CurrentTimeSeconds > clip.DurationSeconds)
                CurrentTimeSeconds = clip.DurationSeconds;
        }

        // Reset local pose to bind
        for (int i = 0; i < Rig.Bones.Count; i++)
            LocalPose[i] = Rig.BindLocalTransforms[i];

        // Sample animation tracks
        foreach (var track in clip.Tracks)
        {
            if (track.BoneIndex >= 0 && track.BoneIndex < LocalPose.Length)
                LocalPose[track.BoneIndex] = track.Sample(CurrentTimeSeconds);
        }

        // Build world pose from hierarchy
        for (int i = 0; i < Rig.Bones.Count; i++)
        {
            int parent = Rig.Bones[i].ParentIndex;
            if (parent < 0)
                WorldPose[i] = LocalPose[i];
            else
                WorldPose[i] = LocalPose[i] * WorldPose[parent];
        }

        // Compute skin pose: inverseBind * world
        for (int i = 0; i < Rig.Bones.Count; i++)
            SkinPose[i] = Rig.InverseBindTransforms[i] * WorldPose[i];
    }

    private void ResetToBindPose()
    {
        for (int i = 0; i < Rig.Bones.Count; i++)
        {
            LocalPose[i] = Rig.BindLocalTransforms[i];
            WorldPose[i] = Rig.BindWorldTransforms[i];
            SkinPose[i] = Matrix.Identity;
        }
    }
}
