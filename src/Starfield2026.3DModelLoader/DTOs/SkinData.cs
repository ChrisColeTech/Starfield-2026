#nullable enable
using System;
using System.Collections.Generic;

namespace Starfield2026.ModelLoader.DTOs;

public sealed class SkinData
{
    public required string GeometryId { get; init; }
    public required List<VertexInfluence> Influences { get; init; }
}

public readonly struct VertexInfluence
{
    public static VertexInfluence Default => new(new[] { 0, 0, 0, 0 }, new[] { 1f, 0f, 0f, 0f });

    public int[] BoneIndices { get; }
    public float[] Weights { get; }

    public VertexInfluence(int[] boneIndices, float[] weights)
    {
        BoneIndices = boneIndices;
        Weights = weights;
    }

    public static VertexInfluence FromPairs(List<(int Bone, float Weight)> pairs)
    {
        if (pairs.Count == 0) return Default;

        pairs.Sort((a, b) => b.Weight.CompareTo(a.Weight));
        int[] bones = { 0, 0, 0, 0 };
        float[] weights = { 0f, 0f, 0f, 0f };

        float total = 0f;
        int count = Math.Min(4, pairs.Count);
        for (int i = 0; i < count; i++)
        {
            bones[i] = pairs[i].Bone;
            weights[i] = pairs[i].Weight;
            total += pairs[i].Weight;
        }

        if (total > 0f)
            for (int i = 0; i < 4; i++)
                weights[i] /= total;
        else
            weights[0] = 1f;

        return new VertexInfluence(bones, weights);
    }
}
