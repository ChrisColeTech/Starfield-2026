#nullable enable
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.DTOs;

namespace Starfield2026.ModelLoader.Animations;

public sealed class ClipPlayer
{
    private readonly Skeleton _skeleton;

    public AnimationClip? ActiveClip { get; private set; }
    public bool Loop { get; private set; }
    public float CurrentTime { get; private set; }
    public float Speed { get; set; } = 1f;

    public Matrix[] LocalPose { get; }
    public Matrix[] WorldPose { get; }
    public Matrix[] SkinPose { get; }

    public ClipPlayer(Skeleton skeleton)
    {
        _skeleton = skeleton;
        int count = skeleton.Bones.Count;
        LocalPose = new Matrix[count];
        WorldPose = new Matrix[count];
        SkinPose = new Matrix[count];
        ResetToBindPose();
    }

    public void Play(AnimationClip clip, bool loop = true, bool resetTime = true)
    {
        ActiveClip = clip;
        Loop = loop;
        if (resetTime)
            CurrentTime = 0f;
    }

    public void Stop()
    {
        ActiveClip = null;
        CurrentTime = 0f;
        ResetToBindPose();
    }

    public void Update(float deltaSeconds)
    {
        var clip = ActiveClip;
        if (clip is null || clip.Duration <= 0f)
            return;

        CurrentTime += deltaSeconds * Speed;

        if (Loop)
        {
            if (CurrentTime >= clip.Duration)
                CurrentTime %= clip.Duration;
        }
        else
        {
            if (CurrentTime > clip.Duration)
                CurrentTime = clip.Duration;
        }

        ClipSampler.Sample(clip, CurrentTime, LocalPose, _skeleton.BindLocalTransforms);
        PoseResolver.Resolve(_skeleton, LocalPose, WorldPose, SkinPose);
    }

    private void ResetToBindPose()
    {
        for (int i = 0; i < _skeleton.Bones.Count; i++)
        {
            LocalPose[i] = _skeleton.BindLocalTransforms[i];
            WorldPose[i] = _skeleton.BindWorldTransforms[i];
            SkinPose[i] = Matrix.Identity;
        }
    }
}
