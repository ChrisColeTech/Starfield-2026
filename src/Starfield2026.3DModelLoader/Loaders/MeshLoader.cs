#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Starfield2026.ModelLoader.DTOs;
using static Starfield2026.ModelLoader.Loaders.ColladaHelpers;

namespace Starfield2026.ModelLoader.Loaders;

public static class MeshLoader
{
    public static List<MeshData> Load(XDocument doc)
    {
        var result = new List<MeshData>();

        foreach (var geometry in doc.Descendants(Col + "geometry"))
        {
            string? geometryId = geometry.Attribute("id")?.Value;
            var mesh = geometry.Element(Col + "mesh");
            if (string.IsNullOrWhiteSpace(geometryId) || mesh is null) continue;

            var sources = new Dictionary<string, float[]>(StringComparer.Ordinal);
            foreach (var src in mesh.Elements(Col + "source"))
            {
                string? id = src.Attribute("id")?.Value;
                var fa = src.Element(Col + "float_array");
                if (id != null && fa != null)
                    sources[id] = ParseFloats(fa.Value);
            }

            var vertices = mesh.Element(Col + "vertices");
            string? posSourceId = vertices?.Elements(Col + "input")
                .FirstOrDefault(x => x.Attribute("semantic")?.Value == "POSITION")
                ?.Attribute("source")?.Value.TrimStart('#');

            if (string.IsNullOrWhiteSpace(posSourceId) || !sources.TryGetValue(posSourceId, out float[]? positions))
                continue;

            var triSets = mesh.Elements(Col + "triangles")
                .Concat(mesh.Elements(Col + "polylist")).ToList();

            foreach (var triSet in triSets)
            {
                string matSymbol = triSet.Attribute("material")?.Value ?? "";

                var inputs = triSet.Elements(Col + "input")
                    .Select(x => new InputBinding(
                        x.Attribute("semantic")?.Value ?? "",
                        x.Attribute("source")?.Value?.TrimStart('#') ?? "",
                        int.TryParse(x.Attribute("offset")?.Value, out int o) ? o : 0))
                    .ToList();

                int stride = inputs.Count == 0 ? 1 : inputs.Max(x => x.Offset) + 1;

                var pElem = triSet.Element(Col + "p");
                if (pElem == null) continue;
                int[] indexData = ParseInts(pElem.Value);
                if (indexData.Length == 0) continue;

                string? nrmSourceId = inputs.FirstOrDefault(x => x.Semantic == "NORMAL")?.SourceId;
                string? uvSourceId = inputs.FirstOrDefault(x => x.Semantic == "TEXCOORD")?.SourceId;

                result.Add(new MeshData
                {
                    GeometryId = geometryId,
                    Positions = positions,
                    Normals = !string.IsNullOrWhiteSpace(nrmSourceId) && sources.TryGetValue(nrmSourceId, out var n) ? n : System.Array.Empty<float>(),
                    UVs = !string.IsNullOrWhiteSpace(uvSourceId) && sources.TryGetValue(uvSourceId, out var uv) ? uv : System.Array.Empty<float>(),
                    Indices = indexData,
                    Stride = stride,
                    Inputs = inputs,
                    MaterialSymbol = matSymbol,
                });
            }
        }

        return result;
    }
}
