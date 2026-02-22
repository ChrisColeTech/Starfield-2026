#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.Skeletal;

public record SkeletonBone(int Index, string Name, string NodeId, int ParentIndex, Matrix BindLocalTransform);

public class SkeletonRig
{
    public IReadOnlyList<SkeletonBone> Bones { get; }
    public Matrix[] BindLocalTransforms { get; }
    public Matrix[] BindWorldTransforms { get; }
    public Matrix[] InverseBindTransforms { get; }

    private readonly Dictionary<string, int> _nameToIndex = new();
    private readonly Dictionary<string, int> _nodeIdToIndex = new();

    public SkeletonRig(List<SkeletonBone> bones)
    {
        Bones = bones;
        int count = bones.Count;
        BindLocalTransforms = new Matrix[count];
        BindWorldTransforms = new Matrix[count];
        InverseBindTransforms = new Matrix[count];

        for (int i = 0; i < count; i++)
        {
            var bone = bones[i];
            BindLocalTransforms[i] = bone.BindLocalTransform;

            if (bone.ParentIndex < 0)
                BindWorldTransforms[i] = bone.BindLocalTransform;
            else
                BindWorldTransforms[i] = bone.BindLocalTransform * BindWorldTransforms[bone.ParentIndex];

            Matrix.Invert(ref BindWorldTransforms[i], out InverseBindTransforms[i]);

            _nameToIndex[bone.Name] = i;
            _nodeIdToIndex[bone.NodeId] = i;
        }
    }

    public bool TryGetBoneIndex(string name, out int index)
    {
        if (_nameToIndex.TryGetValue(name, out index))
            return true;
        if (_nodeIdToIndex.TryGetValue(name, out index))
            return true;
        index = -1;
        return false;
    }

    /// <summary>
    /// Override inverse bind matrices with values from the COLLADA file.
    /// These are authoritative and should replace the computed ones.
    /// </summary>
    public void SetInverseBindMatrices(string[] jointNames, Matrix[] matrices)
    {
        for (int i = 0; i < jointNames.Length && i < matrices.Length; i++)
        {
            if (TryGetBoneIndex(jointNames[i], out int boneIdx))
                InverseBindTransforms[boneIdx] = matrices[i];
        }
    }
}
