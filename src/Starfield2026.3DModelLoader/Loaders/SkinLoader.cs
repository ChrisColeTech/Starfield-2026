#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Starfield2026.ModelLoader.DTOs;
using static Starfield2026.ModelLoader.Loaders.ColladaHelpers;

namespace Starfield2026.ModelLoader.Loaders;

public static class SkinLoader
{
    public static Dictionary<string, SkinData> Load(XDocument doc, Skeleton skeleton)
    {
        var result = new Dictionary<string, SkinData>(System.StringComparer.Ordinal);

        foreach (var controller in doc.Descendants(Col + "controller"))
        {
            var skin = controller.Element(Col + "skin");
            if (skin is null) continue;

            string? geometryId = skin.Attribute("source")?.Value.TrimStart('#');
            if (string.IsNullOrWhiteSpace(geometryId)) continue;

            var sources = new Dictionary<string, XElement>(System.StringComparer.Ordinal);
            foreach (var src in skin.Elements(Col + "source"))
            {
                string? id = src.Attribute("id")?.Value;
                if (id != null) sources[id] = src;
            }

            var joints = skin.Element(Col + "joints");
            var vertexWeights = skin.Element(Col + "vertex_weights");
            if (joints is null || vertexWeights is null) continue;

            string? jointSourceId = joints.Elements(Col + "input")
                .FirstOrDefault(x => x.Attribute("semantic")?.Value == "JOINT")
                ?.Attribute("source")?.Value.TrimStart('#');
            string? weightSourceId = vertexWeights.Elements(Col + "input")
                .FirstOrDefault(x => x.Attribute("semantic")?.Value == "WEIGHT")
                ?.Attribute("source")?.Value.TrimStart('#');

            if (string.IsNullOrWhiteSpace(jointSourceId) || string.IsNullOrWhiteSpace(weightSourceId)) continue;
            if (!sources.TryGetValue(jointSourceId, out var jointSource)) continue;
            if (!sources.TryGetValue(weightSourceId, out var weightSource)) continue;

            string[] jointNames = ParseNames(
                (jointSource.Element(Col + "Name_array") ?? jointSource.Element(Col + "IDREF_array"))?.Value);
            float[] weights = ParseFloatsNullable(weightSource.Element(Col + "float_array")?.Value);
            int[] vcount = ParseIntsNullable(vertexWeights.Element(Col + "vcount")?.Value);
            int[] v = ParseIntsNullable(vertexWeights.Element(Col + "v")?.Value);
            if (vcount.Length == 0 || v.Length == 0) continue;

            var influences = new List<VertexInfluence>(vcount.Length);
            int cursor = 0;

            for (int vi = 0; vi < vcount.Length; vi++)
            {
                int count = vcount[vi];
                var pairs = new List<(int Bone, float Weight)>();

                for (int i = 0; i < count; i++)
                {
                    if (cursor + 1 >= v.Length) break;
                    int jointIndex = v[cursor++];
                    int weightIndex = v[cursor++];
                    if (jointIndex < 0 || jointIndex >= jointNames.Length) continue;
                    if (weightIndex < 0 || weightIndex >= weights.Length) continue;
                    if (!skeleton.TryGetBoneIndex(jointNames[jointIndex], out int boneIndex)) continue;

                    float w = weights[weightIndex];
                    if (w > 0f) pairs.Add((boneIndex, w));
                }

                influences.Add(VertexInfluence.FromPairs(pairs));
            }

            result[geometryId] = new SkinData
            {
                GeometryId = geometryId,
                Influences = influences
            };
        }

        return result;
    }
}
