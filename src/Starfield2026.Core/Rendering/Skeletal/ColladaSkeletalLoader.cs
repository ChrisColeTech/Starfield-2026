#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Rendering.Skeletal;

public static class ColladaSkeletalLoader
{
    private static readonly XNamespace Col = "http://www.collada.org/2005/11/COLLADASchema";

    // ─── Skeleton ───────────────────────────────────────────────────

    public static SkeletonRig LoadSkeleton(string daePath)
    {
        var doc = XDocument.Load(daePath);
        var root = doc.Root!;

        // Find the visual scene
        var visualScene = root.Descendants(Col + "visual_scene").FirstOrDefault()
            ?? throw new InvalidDataException("No <visual_scene> found in " + daePath);

        // Find joint root nodes
        var bones = new List<SkeletonBone>();
        var nodeStack = new Stack<(XElement node, int parentIndex)>();

        // Push top-level nodes (in reverse so first child is processed first)
        var topNodes = visualScene.Elements(Col + "node").Reverse().ToList();
        foreach (var n in topNodes)
            nodeStack.Push((n, -1));

        while (nodeStack.Count > 0)
        {
            var (node, parentIdx) = nodeStack.Pop();
            string? type = (string?)node.Attribute("type");
            bool isJoint = string.Equals(type, "JOINT", StringComparison.OrdinalIgnoreCase);

            int myIndex = -1;

            if (isJoint)
            {
                string name = (string?)node.Attribute("name") ?? (string?)node.Attribute("sid") ?? $"bone_{bones.Count}";
                string nodeId = (string?)node.Attribute("id") ?? name;
                string? sid = (string?)node.Attribute("sid");

                Matrix localTransform = ReadNodeTransform(node);
                myIndex = bones.Count;
                bones.Add(new SkeletonBone(myIndex, sid ?? name, nodeId, parentIdx, localTransform));
            }

            // Push children in reverse order
            var children = node.Elements(Col + "node").Reverse().ToList();
            int childParent = isJoint ? myIndex : parentIdx;
            foreach (var child in children)
                nodeStack.Push((child, childParent));
        }

        if (bones.Count == 0)
            throw new InvalidDataException("No JOINT nodes found in " + daePath);

        return new SkeletonRig(bones);
    }

    // ─── Clip ───────────────────────────────────────────────────────

    public static SkeletalAnimationClip LoadClip(string clipDaePath, SkeletonRig rig, string clipName)
    {
        var doc = XDocument.Load(clipDaePath);
        var root = doc.Root!;

        var animations = root.Descendants(Col + "animation")
            .Where(a => a.Elements(Col + "sampler").Any())
            .ToList();

        var tracks = new List<BoneAnimationTrack>();
        float maxTime = 0f;

        foreach (var anim in animations)
        {
            var channel = anim.Element(Col + "channel");
            if (channel == null) continue;

            string target = (string?)channel.Attribute("target") ?? "";
            // Target format: "BoneName/transform" or "BoneId/matrix"
            string boneName = target.Split('/')[0];

            // Try to match bone by name or node id
            if (!rig.TryGetBoneIndex(boneName, out int boneIndex))
                continue;

            var sampler = anim.Element(Col + "sampler");
            if (sampler == null) continue;

            // Get input (time) and output (transforms) sources
            string? inputSourceId = GetSamplerSourceId(sampler, "INPUT");
            string? outputSourceId = GetSamplerSourceId(sampler, "OUTPUT");
            if (inputSourceId == null || outputSourceId == null) continue;

            float[] times = ReadFloatSource(anim, inputSourceId);
            float[] values = ReadFloatSource(anim, outputSourceId);

            if (times.Length == 0) continue;

            // Determine if output is matrices (16 floats per keyframe) or other
            int stride = values.Length / times.Length;
            var keyframes = new List<AnimationKeyframe>(times.Length);

            for (int i = 0; i < times.Length; i++)
            {
                Matrix m;
                if (stride >= 16)
                {
                    m = ReadMatrixFromFloats(values, i * 16);
                }
                else
                {
                    // Fallback: use bind pose
                    m = rig.BindLocalTransforms[boneIndex];
                }
                keyframes.Add(new AnimationKeyframe(times[i], m));
                if (times[i] > maxTime) maxTime = times[i];
            }

            tracks.Add(new BoneAnimationTrack(boneIndex, keyframes));
        }

        return new SkeletalAnimationClip(clipName, maxTime, tracks);
    }

    // ─── Mesh geometry parsing ──────────────────────────────────────

    public static (Vector3[] positions, Vector3[] normals, Vector2[] texCoords, int[] indices)
        LoadGeometry(string daePath)
    {
        var doc = XDocument.Load(daePath);
        var root = doc.Root!;

        var mesh = root.Descendants(Col + "mesh").FirstOrDefault()
            ?? throw new InvalidDataException("No <mesh> found in " + daePath);

        // Parse sources
        var sources = new Dictionary<string, float[]>();
        foreach (var src in mesh.Elements(Col + "source"))
        {
            string id = (string?)src.Attribute("id") ?? "";
            var fa = src.Element(Col + "float_array");
            if (fa != null)
                sources["#" + id] = ParseFloats((string)fa);
        }

        // Find vertices element for position semantic
        var verticesElem = mesh.Element(Col + "vertices");
        string? posSourceId = null;
        if (verticesElem != null)
        {
            string verticesId = "#" + ((string?)verticesElem.Attribute("id") ?? "");
            var posInput = verticesElem.Elements(Col + "input")
                .FirstOrDefault(i => (string?)i.Attribute("semantic") == "POSITION");
            posSourceId = (string?)posInput?.Attribute("source");

            // Map the vertices ID to the position source
            if (posSourceId != null && sources.ContainsKey(posSourceId))
                sources[verticesId] = sources[posSourceId];
        }

        // Parse triangles or polylist
        var triElem = mesh.Element(Col + "triangles") ?? mesh.Element(Col + "polylist");
        if (triElem == null)
            throw new InvalidDataException("No <triangles> or <polylist> found");

        var inputs = triElem.Elements(Col + "input").ToList();
        int maxOffset = inputs.Max(i => int.Parse((string?)i.Attribute("offset") ?? "0")) + 1;

        string? vertexSource = null, normalSource = null, texCoordSource = null;
        int vertexOffset = 0, normalOffset = 0, texCoordOffset = 0;

        foreach (var inp in inputs)
        {
            string semantic = (string?)inp.Attribute("semantic") ?? "";
            string source = (string?)inp.Attribute("source") ?? "";
            int offset = int.Parse((string?)inp.Attribute("offset") ?? "0");

            switch (semantic)
            {
                case "VERTEX":
                    vertexSource = source;
                    vertexOffset = offset;
                    break;
                case "NORMAL":
                    normalSource = source;
                    normalOffset = offset;
                    break;
                case "TEXCOORD":
                    texCoordSource = source;
                    texCoordOffset = offset;
                    break;
            }
        }

        var pElem = triElem.Element(Col + "p")
            ?? throw new InvalidDataException("No <p> element in triangles");
        int[] pData = ParseInts((string)pElem);

        int triCount = pData.Length / maxOffset;

        float[]? posData = vertexSource != null && sources.ContainsKey(vertexSource) ? sources[vertexSource] : null;
        float[]? normData = normalSource != null && sources.ContainsKey(normalSource) ? sources[normalSource] : null;
        float[]? tcData = texCoordSource != null && sources.ContainsKey(texCoordSource) ? sources[texCoordSource] : null;

        // Build unique vertices (dedup by index tuple)
        var uniqueVerts = new Dictionary<(int pos, int norm, int tc), int>();
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var indexList = new List<int>();

        for (int i = 0; i < triCount; i++)
        {
            int baseIdx = i * maxOffset;
            int posIdx = pData[baseIdx + vertexOffset];
            int normIdx = normData != null ? pData[baseIdx + normalOffset] : 0;
            int tcIdx = tcData != null ? pData[baseIdx + texCoordOffset] : 0;

            var key = (posIdx, normIdx, tcIdx);
            if (!uniqueVerts.TryGetValue(key, out int vertIndex))
            {
                vertIndex = positions.Count;
                uniqueVerts[key] = vertIndex;

                if (posData != null && posIdx * 3 + 2 < posData.Length)
                    positions.Add(new Vector3(posData[posIdx * 3], posData[posIdx * 3 + 1], posData[posIdx * 3 + 2]));
                else
                    positions.Add(Vector3.Zero);

                if (normData != null && normIdx * 3 + 2 < normData.Length)
                    normals.Add(new Vector3(normData[normIdx * 3], normData[normIdx * 3 + 1], normData[normIdx * 3 + 2]));
                else
                    normals.Add(Vector3.UnitY);

                if (tcData != null && tcIdx * 2 + 1 < tcData.Length)
                    texCoords.Add(new Vector2(tcData[tcIdx * 2], 1f - tcData[tcIdx * 2 + 1]));
                else
                    texCoords.Add(Vector2.Zero);
            }

            indexList.Add(vertIndex);
        }

        return (positions.ToArray(), normals.ToArray(), texCoords.ToArray(), indexList.ToArray());
    }

    // ─── Skin data parsing ──────────────────────────────────────────

    public struct VertexBoneWeight
    {
        public int Bone0, Bone1, Bone2, Bone3;
        public float Weight0, Weight1, Weight2, Weight3;
    }

    public static (VertexBoneWeight[] weights, string[] jointNames) LoadSkinWeights(string daePath)
    {
        var doc = XDocument.Load(daePath);
        var root = doc.Root!;

        var skin = root.Descendants(Col + "skin").FirstOrDefault()
            ?? throw new InvalidDataException("No <skin> found in " + daePath);

        // Parse joint names
        var jointsInput = skin.Element(Col + "joints")?.Elements(Col + "input")
            .FirstOrDefault(i => (string?)i.Attribute("semantic") == "JOINT");
        string jointsSourceId = (string?)jointsInput?.Attribute("source") ?? "";

        var jointSource = FindSource(skin, jointsSourceId);
        var nameArray = jointSource?.Element(Col + "Name_array") ?? jointSource?.Element(Col + "IDREF_array");
        string[] jointNames = nameArray != null
            ? ((string)nameArray).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

        // Parse weights source
        var vertexWeightsElem = skin.Element(Col + "vertex_weights")
            ?? throw new InvalidDataException("No <vertex_weights> in skin");

        var weightInput = vertexWeightsElem.Elements(Col + "input")
            .FirstOrDefault(i => (string?)i.Attribute("semantic") == "WEIGHT");
        string weightSourceId = (string?)weightInput?.Attribute("source") ?? "";
        int weightOffset = int.Parse((string?)weightInput?.Attribute("offset") ?? "1");

        var jointInput = vertexWeightsElem.Elements(Col + "input")
            .FirstOrDefault(i => (string?)i.Attribute("semantic") == "JOINT");
        int jointOffset = int.Parse((string?)jointInput?.Attribute("offset") ?? "0");
        int stride = Math.Max(jointOffset, weightOffset) + 1;

        var weightSource = FindSource(skin, weightSourceId);
        float[] weightValues = weightSource != null
            ? ParseFloats((string)(weightSource.Element(Col + "float_array") ?? new XElement("x", "")))
            : Array.Empty<float>();

        // Parse vcount and v
        int[] vcount = ParseInts((string)(vertexWeightsElem.Element(Col + "vcount") ?? new XElement("x", "")));
        int[] v = ParseInts((string)(vertexWeightsElem.Element(Col + "v") ?? new XElement("x", "")));

        int vertexCount = vcount.Length;
        var weights = new VertexBoneWeight[vertexCount];
        int vIdx = 0;

        for (int i = 0; i < vertexCount; i++)
        {
            int count = vcount[i];
            var boneList = new List<(int bone, float weight)>(count);

            for (int j = 0; j < count; j++)
            {
                int boneIdx = v[vIdx + jointOffset];
                int weightIdx = v[vIdx + weightOffset];
                float w = weightIdx < weightValues.Length ? weightValues[weightIdx] : 0f;
                boneList.Add((boneIdx, w));
                vIdx += stride;
            }

            // Sort by weight descending, take top 4
            boneList.Sort((a, b) => b.weight.CompareTo(a.weight));
            var vw = new VertexBoneWeight();

            float totalWeight = 0f;
            if (boneList.Count > 0) { vw.Bone0 = boneList[0].bone; vw.Weight0 = boneList[0].weight; totalWeight += vw.Weight0; }
            if (boneList.Count > 1) { vw.Bone1 = boneList[1].bone; vw.Weight1 = boneList[1].weight; totalWeight += vw.Weight1; }
            if (boneList.Count > 2) { vw.Bone2 = boneList[2].bone; vw.Weight2 = boneList[2].weight; totalWeight += vw.Weight2; }
            if (boneList.Count > 3) { vw.Bone3 = boneList[3].bone; vw.Weight3 = boneList[3].weight; totalWeight += vw.Weight3; }

            // Normalize
            if (totalWeight > 0f)
            {
                float inv = 1f / totalWeight;
                vw.Weight0 *= inv;
                vw.Weight1 *= inv;
                vw.Weight2 *= inv;
                vw.Weight3 *= inv;
            }

            weights[i] = vw;
        }

        return (weights, jointNames);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static Matrix ReadNodeTransform(XElement node)
    {
        var matrixElem = node.Element(Col + "matrix");
        if (matrixElem != null)
        {
            float[] vals = ParseFloats((string)matrixElem);
            if (vals.Length >= 16)
                return ReadMatrixFromFloats(vals, 0);
        }

        // Fallback: compose from translate/rotate/scale
        Matrix result = Matrix.Identity;

        var translate = node.Element(Col + "translate");
        if (translate != null)
        {
            float[] t = ParseFloats((string)translate);
            if (t.Length >= 3)
                result *= Matrix.CreateTranslation(t[0], t[1], t[2]);
        }

        foreach (var rotate in node.Elements(Col + "rotate"))
        {
            float[] r = ParseFloats((string)rotate);
            if (r.Length >= 4)
                result *= Matrix.CreateFromAxisAngle(
                    new Vector3(r[0], r[1], r[2]),
                    MathHelper.ToRadians(r[3]));
        }

        var scale = node.Element(Col + "scale");
        if (scale != null)
        {
            float[] s = ParseFloats((string)scale);
            if (s.Length >= 3)
                result *= Matrix.CreateScale(s[0], s[1], s[2]);
        }

        return result;
    }

    private static Matrix ReadMatrixFromFloats(float[] vals, int offset)
    {
        // COLLADA stores row-major, XNA uses column-major
        return new Matrix(
            vals[offset + 0], vals[offset + 4], vals[offset + 8],  vals[offset + 12],
            vals[offset + 1], vals[offset + 5], vals[offset + 9],  vals[offset + 13],
            vals[offset + 2], vals[offset + 6], vals[offset + 10], vals[offset + 14],
            vals[offset + 3], vals[offset + 7], vals[offset + 11], vals[offset + 15]);
    }

    private static string? GetSamplerSourceId(XElement sampler, string semantic)
    {
        return (string?)sampler.Elements(Col + "input")
            .FirstOrDefault(i => (string?)i.Attribute("semantic") == semantic)
            ?.Attribute("source");
    }

    private static float[] ReadFloatSource(XElement animScope, string sourceId)
    {
        var source = FindSource(animScope, sourceId);
        if (source == null) return Array.Empty<float>();
        var fa = source.Element(Col + "float_array");
        if (fa == null) return Array.Empty<float>();
        return ParseFloats((string)fa);
    }

    private static XElement? FindSource(XElement scope, string sourceId)
    {
        string id = sourceId.TrimStart('#');
        return scope.Descendants(Col + "source")
            .FirstOrDefault(s => (string?)s.Attribute("id") == id);
    }

    private static float[] ParseFloats(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        return result;
    }

    private static int[] ParseInts(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
        return result;
    }
}
