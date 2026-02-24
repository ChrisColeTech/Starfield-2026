#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.DTOs;

public sealed class Skeleton
{
    public IReadOnlyList<Bone> Bones { get; }
    public Matrix[] BindLocalTransforms { get; }
    public Matrix[] BindWorldTransforms { get; }
    public Matrix[] InverseBindTransforms { get; }

    /// <summary>
    /// Bone indices forming the root chain: root → single-child descendants
    /// down to (and including) the first bone that has multiple children.
    /// Used by ClipSampler to strip root motion translation.
    /// </summary>
    public int[] RootChain { get; }

    private readonly Dictionary<string, int> _nameToIndex = new();
    private readonly Dictionary<string, int> _nodeIdToIndex = new();

    public Skeleton(List<Bone> bones)
    {
        Bones = bones;
        int count = bones.Count;
        BindLocalTransforms = new Matrix[count];
        BindWorldTransforms = new Matrix[count];
        InverseBindTransforms = new Matrix[count];

        for (int i = 0; i < count; i++)
        {
            var bone = bones[i];
            BindLocalTransforms[i] = bone.LocalTransform;

            BindWorldTransforms[i] = bone.ParentIndex < 0
                ? bone.LocalTransform
                : bone.LocalTransform * BindWorldTransforms[bone.ParentIndex];

            Matrix.Invert(ref BindWorldTransforms[i], out InverseBindTransforms[i]);

            _nameToIndex[bone.Name] = i;
            _nodeIdToIndex[bone.NodeId] = i;
        }

        RootChain = BuildRootChain(bones);
    }

    private static int[] BuildRootChain(List<Bone> bones)
    {
        // Count children per bone
        var childCount = new int[bones.Count];
        int rootIndex = -1;
        for (int i = 0; i < bones.Count; i++)
        {
            if (bones[i].ParentIndex < 0)
                rootIndex = i;
            else
                childCount[bones[i].ParentIndex]++;
        }

        if (rootIndex < 0) return System.Array.Empty<int>();

        // Walk from root through single-child bones, stop after first branch
        var chain = new List<int> { rootIndex };
        int current = rootIndex;
        while (childCount[current] == 1)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].ParentIndex == current)
                {
                    chain.Add(i);
                    current = i;
                    break;
                }
            }
        }

        // Include direct children of the last chain bone (the branching bone)
        for (int i = 0; i < bones.Count; i++)
        {
            if (bones[i].ParentIndex == current && !chain.Contains(i))
                chain.Add(i);
        }

        return chain.ToArray();
    }

    public bool TryGetBoneIndex(string name, out int index)
    {
        if (_nameToIndex.TryGetValue(name, out index)) return true;
        if (_nodeIdToIndex.TryGetValue(name, out index)) return true;
        index = -1;
        return false;
    }

    public void SetInverseBindMatrices(string[] jointNames, Matrix[] matrices)
    {
        for (int i = 0; i < jointNames.Length && i < matrices.Length; i++)
        {
            if (TryGetBoneIndex(jointNames[i], out int boneIdx))
                InverseBindTransforms[boneIdx] = matrices[i];
        }
    }
}
