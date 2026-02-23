#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.DTOs;

namespace Starfield2026.ModelLoader.Rendering;

internal static class MeshBuilder
{
    internal static SkinnedVertex[]? Build(
        MeshData geometry,
        SkinData skin,
        out int[] indices)
    {
        var remap = new Dictionary<(int Pos, int Nrm, int Uv), int>();
        var verts = new List<SkinnedVertex>();
        var idxList = new List<int>();

        int posOffset = geometry.Inputs.FirstOrDefault(x => x.Semantic == "VERTEX")?.Offset ?? 0;
        int nrmOffset = geometry.Inputs.FirstOrDefault(x => x.Semantic == "NORMAL")?.Offset ?? posOffset;
        int uvOffset = geometry.Inputs.FirstOrDefault(x => x.Semantic == "TEXCOORD")?.Offset ?? posOffset;

        for (int i = 0; i < geometry.Indices.Length; i += geometry.Stride)
        {
            int posIndex = geometry.Indices[i + posOffset];
            int nrmIndex = geometry.Indices[i + nrmOffset];
            int uvIndex = geometry.Indices[i + uvOffset];

            var key = (posIndex, nrmIndex, uvIndex);
            if (remap.TryGetValue(key, out int existing))
            {
                idxList.Add(existing);
                continue;
            }

            if (posIndex * 3 + 2 >= geometry.Positions.Length)
                continue;

            Vector3 pos = new(
                geometry.Positions[posIndex * 3],
                geometry.Positions[posIndex * 3 + 1],
                geometry.Positions[posIndex * 3 + 2]);

            Vector3 nrm = Vector3.UnitY;
            if (nrmIndex * 3 + 2 < geometry.Normals.Length)
                nrm = new Vector3(
                    geometry.Normals[nrmIndex * 3],
                    geometry.Normals[nrmIndex * 3 + 1],
                    geometry.Normals[nrmIndex * 3 + 2]);

            Vector2 uv = Vector2.Zero;
            if (uvIndex * 2 + 1 < geometry.UVs.Length)
                uv = new Vector2(geometry.UVs[uvIndex * 2], 1f - geometry.UVs[uvIndex * 2 + 1]);

            var influence = posIndex < skin.Influences.Count
                ? skin.Influences[posIndex]
                : VertexInfluence.Default;

            verts.Add(new SkinnedVertex
            {
                Position = pos,
                Normal = nrm,
                Uv = uv,
                Bone0 = influence.BoneIndices[0],
                Bone1 = influence.BoneIndices[1],
                Bone2 = influence.BoneIndices[2],
                Bone3 = influence.BoneIndices[3],
                Weight0 = influence.Weights[0],
                Weight1 = influence.Weights[1],
                Weight2 = influence.Weights[2],
                Weight3 = influence.Weights[3],
            });

            remap[key] = verts.Count - 1;
            idxList.Add(verts.Count - 1);
        }

        indices = idxList.ToArray();
        return verts.Count == 0 ? null : verts.ToArray();
    }
}
