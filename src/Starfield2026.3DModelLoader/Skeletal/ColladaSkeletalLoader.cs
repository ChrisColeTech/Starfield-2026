#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader;

namespace Starfield2026.ModelLoader.Skeletal;

public static class ColladaSkeletalLoader
{
    private static readonly XNamespace Col = "http://www.collada.org/2005/11/COLLADASchema";

    // --- Skeleton ---

    public static SkeletonRig LoadSkeleton(string daePath)
    {
        ModelLoaderLog.Info($"[Skeleton] Loading skeleton from: {daePath}");
        var doc = XDocument.Load(daePath);
        var root = doc.Root!;

        var visualScene = root.Descendants(Col + "visual_scene").FirstOrDefault()
            ?? throw new InvalidDataException("No <visual_scene> found in " + daePath);

        var bones = new List<SkeletonBone>();
        var nodeStack = new Stack<(XElement node, int parentIndex)>();

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

            var children = node.Elements(Col + "node").Reverse().ToList();
            int childParent = isJoint ? myIndex : parentIdx;
            foreach (var child in children)
                nodeStack.Push((child, childParent));
        }

        if (bones.Count == 0)
            throw new InvalidDataException("No JOINT nodes found in " + daePath);

        ModelLoaderLog.Info($"[Skeleton] Loaded {bones.Count} bones from {Path.GetFileName(daePath)}");
        for (int i = 0; i < Math.Min(bones.Count, 10); i++)
            ModelLoaderLog.Info($"  Bone[{i}]: {bones[i].Name} (parent={bones[i].ParentIndex})");
        if (bones.Count > 10)
            ModelLoaderLog.Info($"  ... and {bones.Count - 10} more bones");

        return new SkeletonRig(bones);
    }

    // --- Clip ---

    public static SkeletalAnimationClip LoadClip(string clipDaePath, SkeletonRig rig, string clipName)
    {
        ModelLoaderLog.Info($"[Clip] Loading clip '{clipName}' from: {clipDaePath}");
        var doc = XDocument.Load(clipDaePath);
        var root = doc.Root!;

        // Build a GLOBAL source dictionary — sources may live in parent animation elements
        var floatSources = new Dictionary<string, float[]>(StringComparer.Ordinal);
        foreach (var src in root.Descendants(Col + "source"))
        {
            string? id = src.Attribute("id")?.Value;
            var fa = src.Element(Col + "float_array");
            if (id != null && fa != null)
            {
                float[] data = ParseFloats((string)fa);
                if (data.Length > 0)
                    floatSources[id] = data;
            }
        }

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

            // Only process full matrix transform channels — per-component channels
            // (e.g. rotateX.ANGLE) have stride < 16 and would overwrite good matrix tracks
            if (!target.EndsWith("/transform", StringComparison.Ordinal))
                continue;

            string boneName = target[..target.IndexOf('/')];

            if (!rig.TryGetBoneIndex(boneName, out int boneIndex))
                continue;

            var sampler = anim.Element(Col + "sampler");
            if (sampler == null) continue;

            string? inputSourceId = GetSamplerSourceId(sampler, "INPUT")?.TrimStart('#');
            string? outputSourceId = GetSamplerSourceId(sampler, "OUTPUT")?.TrimStart('#');
            if (inputSourceId == null || outputSourceId == null) continue;

            if (!floatSources.TryGetValue(inputSourceId, out float[]? times)) continue;
            if (!floatSources.TryGetValue(outputSourceId, out float[]? values)) continue;

            int matrixCount = values.Length / 16;
            int keyCount = Math.Min(times.Length, matrixCount);
            if (keyCount == 0) continue;

            var keyframes = new List<AnimationKeyframe>(keyCount);
            for (int i = 0; i < keyCount; i++)
            {
                Matrix m = ReadMatrixFromFloats(values, i * 16);
                keyframes.Add(new AnimationKeyframe(times[i], m));
                if (times[i] > maxTime) maxTime = times[i];
            }

            tracks.Add(new BoneAnimationTrack(boneIndex, keyframes));
        }

        ModelLoaderLog.Info($"[Clip] Clip '{clipName}': {tracks.Count} tracks, duration={maxTime:F3}s");
        return new SkeletalAnimationClip(clipName, maxTime, tracks);
    }

    // --- Mesh geometry parsing ---

    public static (Vector3[] positions, Vector3[] normals, Vector2[] texCoords, int[] indices, int[] skinWeightIndices)
        LoadGeometry(string daePath)
    {
        ModelLoaderLog.Info($"[Geometry] Loading geometry from: {daePath}");
        var doc = XDocument.Load(daePath);
        var root = doc.Root!;

        var meshes = root.Descendants(Col + "mesh").ToList();
        if (meshes.Count == 0)
            throw new InvalidDataException("No <mesh> found in " + daePath);

        var allPositions = new List<Vector3>();
        var allNormals = new List<Vector3>();
        var allTexCoords = new List<Vector2>();
        var allIndices = new List<int>();
        var allSkinWeightIndices = new List<int>();

        int skinVertexOffset = 0;

        foreach (var mesh in meshes)
        {
            var sources = new Dictionary<string, float[]>();
            foreach (var src in mesh.Elements(Col + "source"))
            {
                string id = (string?)src.Attribute("id") ?? "";
                var fa = src.Element(Col + "float_array");
                if (fa != null)
                    sources["#" + id] = ParseFloats((string)fa);
            }

            var verticesElem = mesh.Element(Col + "vertices");
            string? posSourceId = null;
            int meshPositionCount = 0;
            if (verticesElem != null)
            {
                string verticesId = "#" + ((string?)verticesElem.Attribute("id") ?? "");
                var posInput = verticesElem.Elements(Col + "input")
                    .FirstOrDefault(i => (string?)i.Attribute("semantic") == "POSITION");
                posSourceId = (string?)posInput?.Attribute("source");

                if (posSourceId != null && sources.ContainsKey(posSourceId))
                {
                    sources[verticesId] = sources[posSourceId];
                    meshPositionCount = sources[posSourceId].Length / 3;
                }
            }

            var triElems = mesh.Elements(Col + "triangles")
                .Concat(mesh.Elements(Col + "polylist")).ToList();

            foreach (var triElem in triElems)
            {
                var inputs = triElem.Elements(Col + "input").ToList();
                if (inputs.Count == 0) continue;
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
                        case "VERTEX": vertexSource = source; vertexOffset = offset; break;
                        case "NORMAL": normalSource = source; normalOffset = offset; break;
                        case "TEXCOORD": texCoordSource = source; texCoordOffset = offset; break;
                    }
                }

                var pElem = triElem.Element(Col + "p");
                if (pElem == null) continue;
                int[] pData = ParseInts((string)pElem);

                int triCount = pData.Length / maxOffset;

                float[]? posData = vertexSource != null && sources.ContainsKey(vertexSource) ? sources[vertexSource] : null;
                float[]? normData = normalSource != null && sources.ContainsKey(normalSource) ? sources[normalSource] : null;
                float[]? tcData = texCoordSource != null && sources.ContainsKey(texCoordSource) ? sources[texCoordSource] : null;

                var uniqueVerts = new Dictionary<(int pos, int norm, int tc), int>();

                for (int i = 0; i < triCount; i++)
                {
                    int baseIdx = i * maxOffset;
                    int posIdx = pData[baseIdx + vertexOffset];
                    int normIdx = normData != null ? pData[baseIdx + normalOffset] : 0;
                    int tcIdx = tcData != null ? pData[baseIdx + texCoordOffset] : 0;

                    var key = (posIdx, normIdx, tcIdx);
                    if (!uniqueVerts.TryGetValue(key, out int vertIndex))
                    {
                        vertIndex = allPositions.Count;
                        uniqueVerts[key] = vertIndex;

                        if (posData != null && posIdx * 3 + 2 < posData.Length)
                            allPositions.Add(new Vector3(posData[posIdx * 3], posData[posIdx * 3 + 1], posData[posIdx * 3 + 2]));
                        else
                            allPositions.Add(Vector3.Zero);

                        if (normData != null && normIdx * 3 + 2 < normData.Length)
                            allNormals.Add(new Vector3(normData[normIdx * 3], normData[normIdx * 3 + 1], normData[normIdx * 3 + 2]));
                        else
                            allNormals.Add(Vector3.UnitY);

                        if (tcData != null && tcIdx * 2 + 1 < tcData.Length)
                            allTexCoords.Add(new Vector2(tcData[tcIdx * 2], 1f - tcData[tcIdx * 2 + 1]));
                        else
                            allTexCoords.Add(Vector2.Zero);

                        allSkinWeightIndices.Add(skinVertexOffset + posIdx);
                    }

                    allIndices.Add(vertIndex);
                }
            }

            skinVertexOffset += meshPositionCount;
        }

        ModelLoaderLog.Info($"[Geometry] Loaded: {allPositions.Count} vertices, {allIndices.Count / 3} triangles, {allSkinWeightIndices.Count} skin indices, {meshes.Count} mesh(es)");
        return (allPositions.ToArray(), allNormals.ToArray(), allTexCoords.ToArray(), allIndices.ToArray(), allSkinWeightIndices.ToArray());
    }

    // --- Skin data parsing ---

    public struct VertexBoneWeight
    {
        public int Bone0, Bone1, Bone2, Bone3;
        public float Weight0, Weight1, Weight2, Weight3;
    }

    public static (VertexBoneWeight[] weights, string[] jointNames, Matrix[]? inverseBindMatrices) LoadSkinWeights(string daePath)
    {
        ModelLoaderLog.Info($"[Skin] Loading skin weights from: {daePath}");
        var doc = XDocument.Load(daePath);
        var root = doc.Root!;

        var skins = root.Descendants(Col + "skin").ToList();
        if (skins.Count == 0)
            throw new InvalidDataException("No <skin> found in " + daePath);

        var allJointNames = new List<string>();
        var jointNameSet = new HashSet<string>();
        var allWeights = new List<VertexBoneWeight>();
        Matrix[]? inverseBindMatrices = null;

        foreach (var skin in skins)
        {
            var jointsInput = skin.Element(Col + "joints")?.Elements(Col + "input")
                .FirstOrDefault(i => (string?)i.Attribute("semantic") == "JOINT");
            string jointsSourceId = (string?)jointsInput?.Attribute("source") ?? "";

            var jointSource = FindSource(skin, jointsSourceId);
            var nameArray = jointSource?.Element(Col + "Name_array") ?? jointSource?.Element(Col + "IDREF_array");
            string[] skinJointNames = nameArray != null
                ? ((string)nameArray).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();

            if (inverseBindMatrices == null)
            {
                var ibmInput = skin.Element(Col + "joints")?.Elements(Col + "input")
                    .FirstOrDefault(i => (string?)i.Attribute("semantic") == "INV_BIND_MATRIX");
                if (ibmInput != null)
                {
                    string ibmSourceId = (string?)ibmInput.Attribute("source") ?? "";
                    var ibmSource = FindSource(skin, ibmSourceId);
                    if (ibmSource != null)
                    {
                        var fa = ibmSource.Element(Col + "float_array");
                        if (fa != null)
                        {
                            float[] ibmData = ParseFloats((string)fa);
                            int matCount = ibmData.Length / 16;
                            inverseBindMatrices = new Matrix[matCount];
                            for (int m = 0; m < matCount; m++)
                                inverseBindMatrices[m] = ReadMatrixFromFloats(ibmData, m * 16);
                        }
                    }
                }
            }

            int[] jointRemap = new int[skinJointNames.Length];
            for (int i = 0; i < skinJointNames.Length; i++)
            {
                if (!jointNameSet.Contains(skinJointNames[i]))
                {
                    jointNameSet.Add(skinJointNames[i]);
                    allJointNames.Add(skinJointNames[i]);
                }
                jointRemap[i] = allJointNames.IndexOf(skinJointNames[i]);
            }

            var vertexWeightsElem = skin.Element(Col + "vertex_weights");
            if (vertexWeightsElem == null) continue;

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

            int[] vcount = ParseInts((string)(vertexWeightsElem.Element(Col + "vcount") ?? new XElement("x", "")));
            int[] v = ParseInts((string)(vertexWeightsElem.Element(Col + "v") ?? new XElement("x", "")));

            int vIdx = 0;
            for (int i = 0; i < vcount.Length; i++)
            {
                int count = vcount[i];
                var boneList = new List<(int bone, float weight)>(count);

                for (int j = 0; j < count; j++)
                {
                    int boneIdx = v[vIdx + jointOffset];
                    int weightIdx = v[vIdx + weightOffset];
                    float w = weightIdx < weightValues.Length ? weightValues[weightIdx] : 0f;
                    int remapped = boneIdx < jointRemap.Length ? jointRemap[boneIdx] : 0;
                    boneList.Add((remapped, w));
                    vIdx += stride;
                }

                boneList.Sort((a, b) => b.weight.CompareTo(a.weight));
                var vw = new VertexBoneWeight();

                float totalWeight = 0f;
                if (boneList.Count > 0) { vw.Bone0 = boneList[0].bone; vw.Weight0 = boneList[0].weight; totalWeight += vw.Weight0; }
                if (boneList.Count > 1) { vw.Bone1 = boneList[1].bone; vw.Weight1 = boneList[1].weight; totalWeight += vw.Weight1; }
                if (boneList.Count > 2) { vw.Bone2 = boneList[2].bone; vw.Weight2 = boneList[2].weight; totalWeight += vw.Weight2; }
                if (boneList.Count > 3) { vw.Bone3 = boneList[3].bone; vw.Weight3 = boneList[3].weight; totalWeight += vw.Weight3; }

                if (totalWeight > 0f)
                {
                    float inv = 1f / totalWeight;
                    vw.Weight0 *= inv;
                    vw.Weight1 *= inv;
                    vw.Weight2 *= inv;
                    vw.Weight3 *= inv;
                }

                allWeights.Add(vw);
            }
        }

        ModelLoaderLog.Info($"[Skin] Loaded: {allWeights.Count} vertex weights, {allJointNames.Count} joints, IBM={inverseBindMatrices?.Length ?? 0}");
        for (int i = 0; i < Math.Min(allJointNames.Count, 10); i++)
            ModelLoaderLog.Info($"  Joint[{i}]: {allJointNames[i]}");
        if (allJointNames.Count > 10)
            ModelLoaderLog.Info($"  ... and {allJointNames.Count - 10} more joints");
        return (allWeights.ToArray(), allJointNames.ToArray(), inverseBindMatrices);
    }

    // --- Helpers ---

    private static Matrix ReadNodeTransform(XElement node)
    {
        var matrixElem = node.Element(Col + "matrix");
        if (matrixElem != null)
        {
            float[] vals = ParseFloats((string)matrixElem);
            if (vals.Length >= 16)
                return ReadMatrixFromFloats(vals, 0);
        }

        Vector3 translation = Vector3.Zero;
        Vector3 scaleVec = Vector3.One;
        Matrix rotation = Matrix.Identity;

        var translate = node.Element(Col + "translate");
        if (translate != null)
        {
            float[] t = ParseFloats((string)translate);
            if (t.Length >= 3)
                translation = new Vector3(t[0], t[1], t[2]);
        }

        foreach (var rotate in node.Elements(Col + "rotate"))
        {
            float[] r = ParseFloats((string)rotate);
            if (r.Length >= 4)
                rotation *= Matrix.CreateFromAxisAngle(
                    new Vector3(r[0], r[1], r[2]),
                    MathHelper.ToRadians(r[3]));
        }

        var scale = node.Element(Col + "scale");
        if (scale != null)
        {
            float[] s = ParseFloats((string)scale);
            if (s.Length >= 3)
                scaleVec = new Vector3(s[0], s[1], s[2]);
        }

        return Matrix.CreateScale(scaleVec) * rotation * Matrix.CreateTranslation(translation);
    }

    private static Matrix ReadMatrixFromFloats(float[] vals, int offset)
    {
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
