#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader;

namespace Starfield2026.ModelLoader.Skeletal;

/// <summary>
/// Loads a COLLADA (.dae) skinned model with per-mesh textures resolved
/// through the material chain (symbol → material → effect → surface → image).
/// Each mesh gets its own texture and draw batch.
/// </summary>
public class SkinnedDaeModel : IDisposable
{
    private static readonly XNamespace Col = "http://www.collada.org/2005/11/COLLADASchema";

    private readonly List<SkinnedMesh> _meshes = new();
    private readonly List<MeshDrawBatch> _batches = new();

    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    public int PrimitiveCount { get; private set; }
    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }

    // Bind-pose vertices for bounds computation
    private VertexPositionNormalTexture[] _allBindVertices = Array.Empty<VertexPositionNormalTexture>();

    public void Load(GraphicsDevice device, string daePath, SkeletonRig rig)
    {
        ModelLoaderLog.Info($"[SkinnedDaeModel] === Load started: {daePath} ===");
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(daePath)) ?? ".";

        XDocument doc = XDocument.Load(daePath);

        // Parse all pieces independently
        Dictionary<string, GeometryData> geometries = ParseGeometries(doc);
        Dictionary<string, ControllerSkinData> skins = ParseControllers(doc, rig);
        Dictionary<string, string> materialToImage = ParseMaterialImageMap(doc);
        Dictionary<string, string> symbolToMaterial = ParseBindMaterialMap(doc);

        ModelLoaderLog.Info($"[SkinnedDaeModel] Parsed: {geometries.Count} geometries, {skins.Count} skins, {materialToImage.Count} materials, {symbolToMaterial.Count} symbols");

        _meshes.Clear();
        _batches.Clear();

        foreach ((string geometryId, GeometryData geometry) in geometries)
        {
            if (!skins.TryGetValue(geometryId, out ControllerSkinData? skinData))
            {
                ModelLoaderLog.Info($"[SkinnedDaeModel] SKIP {geometryId}: no skin controller");
                continue;
            }

            SkinnedMesh? mesh = BuildMesh(geometry, skinData);
            if (mesh is null)
            {
                ModelLoaderLog.Info($"[SkinnedDaeModel] SKIP {geometryId}: BuildMesh returned null");
                continue;
            }

            // Resolve texture for this mesh through the material chain
            string? texturePath = null;
            if (!string.IsNullOrWhiteSpace(geometry.MaterialSymbol))
            {
                string matSymbol = geometry.MaterialSymbol;
                bool gotMat = symbolToMaterial.TryGetValue(matSymbol, out string? matId);
                bool gotImage = gotMat && materialToImage.TryGetValue(matId!, out string? _);
                string? imgFile = gotImage ? materialToImage[matId!] : null;
                if (gotImage)
                    texturePath = ResolveTexturePath(baseDir, imgFile!);

                ModelLoaderLog.Info($"[SkinnedDaeModel] {geometryId}: verts={mesh.Vertices.Length} tris={mesh.Indices.Length / 3} mat='{matSymbol}' matId={matId ?? "NULL"} img={imgFile ?? "NULL"} path={texturePath ?? "NULL"} exists={texturePath != null && File.Exists(texturePath)}");
            }
            else
            {
                ModelLoaderLog.Info($"[SkinnedDaeModel] {geometryId}: verts={mesh.Vertices.Length} tris={mesh.Indices.Length / 3} (no material symbol)");
            }

            Texture2D? texture = null;
            if (!string.IsNullOrWhiteSpace(texturePath) && File.Exists(texturePath))
            {
                using var stream = File.OpenRead(texturePath);
                texture = Texture2D.FromStream(device, stream);

                // Force alpha to 255 — models use alpha test (cutout), not alpha blend
                Color[] pixels = new Color[texture.Width * texture.Height];
                texture.GetData(pixels);
                for (int p = 0; p < pixels.Length; p++)
                    pixels[p].A = 255;
                texture.SetData(pixels);
            }

            mesh.Texture = texture;
            mesh.IsFace = !string.IsNullOrEmpty(geometry.MaterialSymbol) &&
                (geometry.MaterialSymbol.Contains("Eye", StringComparison.OrdinalIgnoreCase) ||
                 geometry.MaterialSymbol.Contains("Mouth", StringComparison.OrdinalIgnoreCase));

            if (mesh.IsFace)
                ModelLoaderLog.Info($"[SkinnedDaeModel] Face mesh detected: {geometryId} (mat='{geometry.MaterialSymbol}', tex={texture != null})");

            _meshes.Add(mesh);
        }

        ModelLoaderLog.Info($"[SkinnedDaeModel] Built {_meshes.Count} meshes");

        // Build initial buffers using identity pose (bind pose)
        RebuildBuffers(device, rig.InverseBindTransforms);
        ComputeBoundsFromBindPose();

        ModelLoaderLog.Info($"[SkinnedDaeModel] Bounds: min={BoundsMin} max={BoundsMax}");
        ModelLoaderLog.Info($"[SkinnedDaeModel] === Load complete: {PrimitiveCount} primitives, {_batches.Count} batches ===");
    }

    public void UpdatePose(GraphicsDevice device, Matrix[] skinPose)
    {
        RebuildBuffers(device, skinPose);
    }

    public void ComputeSkinnedBounds(Matrix[] skinPose)
    {
        // Compute bounds from skinned positions
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        foreach (var mesh in _meshes)
        {
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                Matrix skin = ComputeSkinMatrix(mesh.Vertices[i], skinPose);
                Vector3 pos = Vector3.Transform(mesh.Vertices[i].Position, skin);
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }
        }

        if (min.X != float.MaxValue)
        {
            BoundsMin = min;
            BoundsMax = max;
        }
    }

    private static readonly DepthStencilState FaceDepthState = new()
    {
        DepthBufferEnable = true,
        DepthBufferWriteEnable = true,
        DepthBufferFunction = CompareFunction.LessEqual
    };

    public void Draw(GraphicsDevice device, BasicEffect effect)
    {
        if (VertexBuffer is null || IndexBuffer is null || _batches.Count == 0) return;

        device.SetVertexBuffer(VertexBuffer);
        device.Indices = IndexBuffer;

        // Body meshes first (standard depth)
        DrawBatches(device, effect, isFace: false);

        // Face meshes last with LessEqual depth so they draw on top at same depth
        var prevDepth = device.DepthStencilState;
        device.DepthStencilState = FaceDepthState;
        DrawBatches(device, effect, isFace: true);
        device.DepthStencilState = prevDepth;
    }

    private void DrawBatches(GraphicsDevice device, BasicEffect effect, bool isFace)
    {
        foreach (MeshDrawBatch batch in _batches)
        {
            if (batch.IsFace != isFace) continue;

            effect.Texture = batch.Texture;
            effect.TextureEnabled = batch.Texture is not null;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    batch.BaseVertex,
                    batch.StartIndex,
                    batch.PrimitiveCount);
            }
        }
    }

    private void RebuildBuffers(GraphicsDevice device, Matrix[] skinMatrices)
    {
        List<VertexPositionNormalTexture> allVertices = new();
        List<int> allIndices = new();
        _batches.Clear();

        for (int meshIndex = 0; meshIndex < _meshes.Count; meshIndex++)
        {
            SkinnedMesh mesh = _meshes[meshIndex];
            int baseVertex = allVertices.Count;
            int startIndex = allIndices.Count;

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                SkinnedVertex src = mesh.Vertices[i];
                Matrix skin = ComputeSkinMatrix(src, skinMatrices);

                Vector3 pos = Vector3.Transform(src.Position, skin);
                Vector3 nrm = Vector3.TransformNormal(src.Normal, skin);
                float len = nrm.Length();
                if (len > 0.001f) nrm /= len;

                allVertices.Add(new VertexPositionNormalTexture(pos, nrm, src.Uv));
            }

            for (int i = 0; i < mesh.Indices.Length; i++)
            {
                allIndices.Add(baseVertex + mesh.Indices[i]);
            }

            int primitiveCount = mesh.Indices.Length / 3;
            if (primitiveCount > 0)
            {
                _batches.Add(new MeshDrawBatch
                {
                    BaseVertex = 0,
                    StartIndex = startIndex,
                    PrimitiveCount = primitiveCount,
                    Texture = mesh.Texture,
                    IsFace = mesh.IsFace
                });
            }
        }

        if (allVertices.Count == 0 || allIndices.Count == 0)
        {
            PrimitiveCount = 0;
            return;
        }

        VertexBuffer?.Dispose();
        VertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration,
            allVertices.Count, BufferUsage.WriteOnly);
        VertexBuffer.SetData(allVertices.ToArray());

        IndexBuffer?.Dispose();
        IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits,
            allIndices.Count, BufferUsage.WriteOnly);
        IndexBuffer.SetData(allIndices.ToArray());

        PrimitiveCount = allIndices.Count / 3;
    }

    private static Matrix ComputeSkinMatrix(SkinnedVertex v, Matrix[] skinMatrices)
    {
        Matrix result = default;
        float total = 0f;

        for (int i = 0; i < 4; i++)
        {
            float w = v.Weights[i];
            if (w <= 0f) continue;

            int bone = v.BoneIndices[i];
            if (bone < 0 || bone >= skinMatrices.Length) continue;

            result += skinMatrices[bone] * w;
            total += w;
        }

        return total <= 0f ? Matrix.Identity : result;
    }

    private void ComputeBoundsFromBindPose()
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        foreach (var mesh in _meshes)
        {
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                min = Vector3.Min(min, mesh.Vertices[i].Position);
                max = Vector3.Max(max, mesh.Vertices[i].Position);
            }
        }

        if (min.X != float.MaxValue)
        {
            BoundsMin = min;
            BoundsMax = max;
        }
    }

    public void Dispose()
    {
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
        foreach (var mesh in _meshes)
            mesh.Texture?.Dispose();
        _meshes.Clear();
        _batches.Clear();
        VertexBuffer = null;
        IndexBuffer = null;
    }

    // =====================================================================
    // COLLADA Parsing — per-mesh geometry with material chain
    // =====================================================================

    private static Dictionary<string, GeometryData> ParseGeometries(XDocument doc)
    {
        var result = new Dictionary<string, GeometryData>(StringComparer.Ordinal);

        foreach (XElement geometry in doc.Descendants(Col + "geometry"))
        {
            string? geometryId = geometry.Attribute("id")?.Value;
            XElement? mesh = geometry.Element(Col + "mesh");
            if (string.IsNullOrWhiteSpace(geometryId) || mesh is null) continue;

            var sources = new Dictionary<string, float[]>(StringComparer.Ordinal);
            foreach (var src in mesh.Elements(Col + "source"))
            {
                string? id = src.Attribute("id")?.Value;
                var fa = src.Element(Col + "float_array");
                if (id != null && fa != null)
                    sources[id] = ParseFloats(fa.Value);
            }

            XElement? vertices = mesh.Element(Col + "vertices");
            string? positionSource = vertices?.Elements(Col + "input")
                .FirstOrDefault(x => x.Attribute("semantic")?.Value == "POSITION")
                ?.Attribute("source")?.Value.TrimStart('#');

            if (string.IsNullOrWhiteSpace(positionSource) || !sources.TryGetValue(positionSource, out float[]? positions))
                continue;

            // Handle <triangles> and <polylist> elements
            var triElems = mesh.Elements(Col + "triangles")
                .Concat(mesh.Elements(Col + "polylist")).ToList();

            foreach (var triElem in triElems)
            {
                string materialSymbol = triElem.Attribute("material")?.Value ?? string.Empty;

                var inputs = triElem.Elements(Col + "input")
                    .Select(x => new InputSpec
                    {
                        Semantic = x.Attribute("semantic")?.Value ?? "",
                        Source = x.Attribute("source")?.Value?.TrimStart('#') ?? "",
                        Offset = int.TryParse(x.Attribute("offset")?.Value, out int o) ? o : 0
                    }).ToList();

                int stride = inputs.Count == 0 ? 1 : inputs.Max(x => x.Offset) + 1;

                var pElem = triElem.Element(Col + "p");
                if (pElem == null) continue;
                int[] indexData = ParseInts(pElem.Value);
                if (indexData.Length == 0) continue;

                // Resolve VERTEX input to the actual position source
                string? vertexInputSource = inputs.FirstOrDefault(x => x.Semantic == "VERTEX")?.Source;
                if (vertexInputSource != null)
                {
                    string verticesId = vertices?.Attribute("id")?.Value ?? "";
                    if (vertexInputSource == verticesId && positionSource != null)
                    {
                        // VERTEX points to <vertices>, which in turn points to the position source
                        // Already have positions from above
                    }
                }

                string? normalSource = inputs.FirstOrDefault(x => x.Semantic == "NORMAL")?.Source;
                string? uvSource = inputs.FirstOrDefault(x => x.Semantic == "TEXCOORD")?.Source;
                float[] normals = !string.IsNullOrWhiteSpace(normalSource) && sources.TryGetValue(normalSource, out float[]? n) ? n : Array.Empty<float>();
                float[] uvs = !string.IsNullOrWhiteSpace(uvSource) && sources.TryGetValue(uvSource, out float[]? uv) ? uv : Array.Empty<float>();

                // Use a composite key to handle multiple triangle sets per geometry
                string key = triElems.Count > 1
                    ? $"{geometryId}_{materialSymbol}"
                    : geometryId;

                result[key] = new GeometryData
                {
                    Positions = positions,
                    Normals = normals,
                    Uvs = uvs,
                    Inputs = inputs,
                    Indices = indexData,
                    Stride = stride,
                    MaterialSymbol = materialSymbol
                };
            }
        }

        return result;
    }

    private static Dictionary<string, ControllerSkinData> ParseControllers(XDocument doc, SkeletonRig rig)
    {
        var result = new Dictionary<string, ControllerSkinData>(StringComparer.Ordinal);

        foreach (XElement controller in doc.Descendants(Col + "controller"))
        {
            XElement? skin = controller.Element(Col + "skin");
            if (skin is null) continue;

            string? geometryId = skin.Attribute("source")?.Value.TrimStart('#');
            if (string.IsNullOrWhiteSpace(geometryId)) continue;

            var sources = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var src in skin.Elements(Col + "source"))
            {
                string? id = src.Attribute("id")?.Value;
                if (id != null)
                    sources[id] = src;
            }

            XElement? joints = skin.Element(Col + "joints");
            XElement? vertexWeights = skin.Element(Col + "vertex_weights");
            if (joints is null || vertexWeights is null) continue;

            string? jointSourceId = joints.Elements(Col + "input")
                .FirstOrDefault(x => x.Attribute("semantic")?.Value == "JOINT")
                ?.Attribute("source")?.Value.TrimStart('#');
            string? weightSourceId = vertexWeights.Elements(Col + "input")
                .FirstOrDefault(x => x.Attribute("semantic")?.Value == "WEIGHT")
                ?.Attribute("source")?.Value.TrimStart('#');

            if (string.IsNullOrWhiteSpace(jointSourceId) || string.IsNullOrWhiteSpace(weightSourceId)) continue;
            if (!sources.TryGetValue(jointSourceId, out XElement? jointSource)) continue;
            if (!sources.TryGetValue(weightSourceId, out XElement? weightSource)) continue;

            string[] jointNames = ParseNames(
                (jointSource.Element(Col + "Name_array") ?? jointSource.Element(Col + "IDREF_array"))?.Value);
            float[] weights = ParseFloats(weightSource.Element(Col + "float_array")?.Value);
            int[] vcount = ParseInts(vertexWeights.Element(Col + "vcount")?.Value);
            int[] v = ParseInts(vertexWeights.Element(Col + "v")?.Value);
            if (vcount.Length == 0 || v.Length == 0) continue;

            // Also parse inverse bind matrices and apply to rig
            string? ibmSourceId = joints.Elements(Col + "input")
                .FirstOrDefault(x => x.Attribute("semantic")?.Value == "INV_BIND_MATRIX")
                ?.Attribute("source")?.Value.TrimStart('#');
            if (!string.IsNullOrWhiteSpace(ibmSourceId) && sources.TryGetValue(ibmSourceId, out XElement? ibmSource))
            {
                float[] ibmData = ParseFloats(ibmSource.Element(Col + "float_array")?.Value);
                int matCount = ibmData.Length / 16;
                var ibmMatrices = new Matrix[matCount];
                for (int m = 0; m < matCount; m++)
                    ibmMatrices[m] = ReadMatrixFromFloats(ibmData, m * 16);
                rig.SetInverseBindMatrices(jointNames, ibmMatrices);
            }

            var influences = new List<VertexInfluence>(vcount.Length);
            int cursor = 0;
            for (int vertexIndex = 0; vertexIndex < vcount.Length; vertexIndex++)
            {
                int count = vcount[vertexIndex];
                var pairs = new List<(int Bone, float Weight)>();
                for (int i = 0; i < count; i++)
                {
                    if (cursor + 1 >= v.Length) break;

                    int jointIndex = v[cursor++];
                    int weightIndex = v[cursor++];
                    if (jointIndex < 0 || jointIndex >= jointNames.Length) continue;
                    if (weightIndex < 0 || weightIndex >= weights.Length) continue;
                    if (!rig.TryGetBoneIndex(jointNames[jointIndex], out int boneIndex)) continue;

                    float weight = weights[weightIndex];
                    if (weight <= 0f) continue;
                    pairs.Add((boneIndex, weight));
                }

                influences.Add(VertexInfluence.FromPairs(pairs));
            }

            result[geometryId] = new ControllerSkinData
            {
                InfluencesByControlPoint = influences
            };

            ModelLoaderLog.Info($"[SkinnedDaeModel] Controller for '{geometryId}': {jointNames.Length} joints, {influences.Count} vertex influences");
        }

        return result;
    }

    private static Dictionary<string, string> ParseMaterialImageMap(XDocument doc)
    {
        // Chain: material → effect → surface → image → init_from file path

        // image id → file path
        var images = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement image in doc.Descendants(Col + "image"))
        {
            string? imageId = image.Attribute("id")?.Value;
            string? initFrom = image.Element(Col + "init_from")?.Value;
            if (!string.IsNullOrWhiteSpace(imageId) && !string.IsNullOrWhiteSpace(initFrom))
                images[imageId] = initFrom;
        }

        // effect id → image id (follow surface chain)
        var effectToImage = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement effect in doc.Descendants(Col + "effect"))
        {
            string? effectId = effect.Attribute("id")?.Value;
            if (string.IsNullOrWhiteSpace(effectId)) continue;

            XElement? surface = effect.Descendants(Col + "surface").FirstOrDefault();
            string? surfaceInitFrom = surface?.Element(Col + "init_from")?.Value;
            if (!string.IsNullOrWhiteSpace(surfaceInitFrom))
                effectToImage[effectId] = surfaceInitFrom;
        }

        // material id → file path
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement material in doc.Descendants(Col + "material"))
        {
            string? matId = material.Attribute("id")?.Value;
            XElement? instanceEffect = material.Element(Col + "instance_effect");
            string? effectUrl = instanceEffect?.Attribute("url")?.Value?.TrimStart('#');
            if (string.IsNullOrWhiteSpace(matId) || string.IsNullOrWhiteSpace(effectUrl)) continue;

            if (effectToImage.TryGetValue(effectUrl, out string? imageId) &&
                images.TryGetValue(imageId, out string? filePath))
            {
                result[matId] = filePath;
            }
        }

        return result;
    }

    private static Dictionary<string, string> ParseBindMaterialMap(XDocument doc)
    {
        // Map: material symbol (from <triangles material="...">) → material id
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (XElement instanceMaterial in doc.Descendants(Col + "instance_material"))
        {
            string? symbol = instanceMaterial.Attribute("symbol")?.Value;
            string? target = instanceMaterial.Attribute("target")?.Value?.TrimStart('#');
            if (!string.IsNullOrWhiteSpace(symbol) && !string.IsNullOrWhiteSpace(target))
                result[symbol] = target;
        }

        return result;
    }

    private static string? ResolveTexturePath(string baseDir, string imageFile)
    {
        string cleaned = imageFile.TrimStart('.', '/');
        string direct = Path.Combine(baseDir, cleaned);
        if (File.Exists(direct)) return direct;

        string inTextures = Path.Combine(baseDir, "textures", cleaned);
        if (File.Exists(inTextures)) return inTextures;

        if (!cleaned.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            string withPng = direct + ".png";
            if (File.Exists(withPng)) return withPng;
            withPng = inTextures + ".png";
            if (File.Exists(withPng)) return withPng;
        }

        return null;
    }

    private static SkinnedMesh? BuildMesh(GeometryData geometry, ControllerSkinData skin)
    {
        var remap = new Dictionary<(int Pos, int Nrm, int Uv), int>();
        var vertices = new List<SkinnedVertex>();
        var indices = new List<int>();

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
                indices.Add(existing);
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
            {
                nrm = new Vector3(
                    geometry.Normals[nrmIndex * 3],
                    geometry.Normals[nrmIndex * 3 + 1],
                    geometry.Normals[nrmIndex * 3 + 2]);
            }

            Vector2 uv = Vector2.Zero;
            if (uvIndex * 2 + 1 < geometry.Uvs.Length)
            {
                uv = new Vector2(geometry.Uvs[uvIndex * 2], 1f - geometry.Uvs[uvIndex * 2 + 1]);
            }

            VertexInfluence influence = posIndex < skin.InfluencesByControlPoint.Count
                ? skin.InfluencesByControlPoint[posIndex]
                : VertexInfluence.Default;

            var sv = new SkinnedVertex
            {
                Position = pos,
                Normal = nrm,
                Uv = uv,
                BoneIndices = influence.BoneIndices,
                Weights = influence.Weights
            };

            int newIndex = vertices.Count;
            vertices.Add(sv);
            remap[key] = newIndex;
            indices.Add(newIndex);
        }

        if (vertices.Count == 0 || indices.Count == 0)
            return null;

        return new SkinnedMesh
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray()
        };
    }

    // =====================================================================
    // Parsing helpers
    // =====================================================================

    private static Matrix ReadMatrixFromFloats(float[] vals, int offset)
    {
        return new Matrix(
            vals[offset + 0], vals[offset + 4], vals[offset + 8],  vals[offset + 12],
            vals[offset + 1], vals[offset + 5], vals[offset + 9],  vals[offset + 13],
            vals[offset + 2], vals[offset + 6], vals[offset + 10], vals[offset + 14],
            vals[offset + 3], vals[offset + 7], vals[offset + 11], vals[offset + 15]);
    }

    private static float[] ParseFloats(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<float>();
        string[] parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        float[] result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        return result;
    }

    private static int[] ParseInts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<int>();
        string[] parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int[] result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]);
        return result;
    }

    private static string[] ParseNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        return value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    // =====================================================================
    // Internal types
    // =====================================================================

    private sealed class InputSpec
    {
        public required string Semantic { get; init; }
        public required string Source { get; init; }
        public required int Offset { get; init; }
    }

    private sealed class GeometryData
    {
        public required float[] Positions { get; init; }
        public required float[] Normals { get; init; }
        public required float[] Uvs { get; init; }
        public required List<InputSpec> Inputs { get; init; }
        public required int[] Indices { get; init; }
        public required int Stride { get; init; }
        public string MaterialSymbol { get; init; } = string.Empty;
    }

    private sealed class ControllerSkinData
    {
        public required List<VertexInfluence> InfluencesByControlPoint { get; init; }
    }

    private sealed class SkinnedMesh
    {
        public required SkinnedVertex[] Vertices { get; init; }
        public required int[] Indices { get; init; }
        public Texture2D? Texture { get; set; }
        public bool IsFace { get; set; }
    }

    private sealed class MeshDrawBatch
    {
        public required int BaseVertex { get; init; }
        public required int StartIndex { get; init; }
        public required int PrimitiveCount { get; init; }
        public Texture2D? Texture { get; init; }
        public bool IsFace { get; init; }
    }

    private sealed class SkinnedVertex
    {
        public required Vector3 Position { get; init; }
        public required Vector3 Normal { get; init; }
        public required Vector2 Uv { get; init; }
        public required int[] BoneIndices { get; init; }
        public required float[] Weights { get; init; }
    }

    private readonly struct VertexInfluence
    {
        public static VertexInfluence Default => new(new[] { 0, 0, 0, 0 }, new[] { 1f, 0f, 0f, 0f });

        public VertexInfluence(int[] boneIndices, float[] weights)
        {
            BoneIndices = boneIndices;
            Weights = weights;
        }

        public int[] BoneIndices { get; }
        public float[] Weights { get; }

        public static VertexInfluence FromPairs(List<(int Bone, float Weight)> pairs)
        {
            if (pairs.Count == 0) return Default;

            pairs.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            int[] bones = new[] { 0, 0, 0, 0 };
            float[] weights = new[] { 0f, 0f, 0f, 0f };

            float total = 0f;
            int count = Math.Min(4, pairs.Count);
            for (int i = 0; i < count; i++)
            {
                bones[i] = pairs[i].Bone;
                weights[i] = pairs[i].Weight;
                total += pairs[i].Weight;
            }

            if (total <= 0f)
            {
                weights[0] = 1f;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    weights[i] /= total;
            }

            return new VertexInfluence(bones, weights);
        }
    }
}

public struct VertexPositionNormalTexture : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TextureCoordinate;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public VertexPositionNormalTexture(Vector3 position, Vector3 normal, Vector2 texCoord)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = texCoord;
    }
}
