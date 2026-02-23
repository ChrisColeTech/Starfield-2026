#nullable enable
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.Rendering;

internal static class CpuSkinner
{
    public static void Transform(
        SkinnedVertex[] source,
        VertexPositionNormalTexture[] destination,
        Matrix[] skinMatrices)
    {
        for (int i = 0; i < source.Length; i++)
        {
            ref var src = ref source[i];
            Matrix skin = ComputeSkinMatrix(ref src, skinMatrices);

            Vector3 pos = Vector3.Transform(src.Position, skin);
            Vector3 nrm = Vector3.TransformNormal(src.Normal, skin);
            float len = nrm.Length();
            if (len > 0.001f) nrm /= len;

            destination[i] = new VertexPositionNormalTexture(pos, nrm, src.Uv);
        }
    }

    public static Vector3 TransformPosition(ref SkinnedVertex v, Matrix[] skinMatrices)
    {
        Matrix skin = ComputeSkinMatrix(ref v, skinMatrices);
        return Vector3.Transform(v.Position, skin);
    }

    private static Matrix ComputeSkinMatrix(ref SkinnedVertex v, Matrix[] skinMatrices)
    {
        Matrix result = default;
        float total = 0f;

        AddBone(ref result, ref total, v.Bone0, v.Weight0, skinMatrices);
        AddBone(ref result, ref total, v.Bone1, v.Weight1, skinMatrices);
        AddBone(ref result, ref total, v.Bone2, v.Weight2, skinMatrices);
        AddBone(ref result, ref total, v.Bone3, v.Weight3, skinMatrices);

        return total <= 0f ? Matrix.Identity : result;
    }

    private static void AddBone(ref Matrix result, ref float total, int bone, float weight, Matrix[] matrices)
    {
        if (weight <= 0f || bone < 0 || bone >= matrices.Length) return;
        result += matrices[bone] * weight;
        total += weight;
    }
}
