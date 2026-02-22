#nullable enable
using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Rendering.Skeletal;

public class AnimationController
{
    public SkeletalAnimator Animator { get; }
    public SplitModelAnimationSet AnimationSet { get; }
    public string? ActiveTag { get; private set; }
    public Matrix[] SkinPose => Animator.SkinPose;

    public AnimationController(SplitModelAnimationSet animSet)
    {
        AnimationSet = animSet;
        Animator = new SkeletalAnimator(animSet.Skeleton);
    }

    public bool Play(string tag, bool loop = true, bool resetTime = true)
    {
        if (ActiveTag == tag && !resetTime)
            return true;

        if (!AnimationSet.ClipsByTag.TryGetValue(tag, out var clip))
            return false;

        ActiveTag = tag;
        Animator.Play(clip, loop, resetTime);
        return true;
    }

    public bool HasClip(string tag) => AnimationSet.ClipsByTag.ContainsKey(tag);

    public void Update(float deltaSeconds) => Animator.Update(deltaSeconds);
}
