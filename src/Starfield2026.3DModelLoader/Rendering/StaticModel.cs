#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Assimp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.DTOs;
using Starfield2026.ModelLoader.Loaders;

namespace Starfield2026.ModelLoader.Rendering;

/// <summary>
/// Loads and renders static (unskinned) DAE and FBX models with textures.
/// Used for map props, terrain, and other non-animated geometry.
/// </summary>
public sealed class StaticModel : IDisposable
{
    private readonly List<MeshBatch> _batches = new();
    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;

    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }
    public Vector3 Center => (BoundsMin + BoundsMax) * 0.5f;
    public float Radius => (BoundsMax - BoundsMin).Length() * 0.5f;
    public bool IsLoaded => _vertexBuffer != null;
    public int VertexCount { get; private set; }
    public int BatchCount => _batches.Count;

    public void Load(GraphicsDevice device, string daePath)
    {
        Dispose();

        string baseDir = Path.GetDirectoryName(Path.GetFullPath(daePath)) ?? ".";
        var doc = XDocument.Load(daePath);

        var geometries = MeshLoader.Load(doc);
        var materialToImage = TextureResolver.ParseMaterialImageMap(doc);
        var symbolToMaterial = TextureResolver.ParseBindMaterialMap(doc);

        ModelLoaderLog.Info($"[StaticModel] Geometries found: {geometries.Count}");
        ModelLoaderLog.Info($"[StaticModel] Materials: {symbolToMaterial.Count}, Images: {materialToImage.Count}");

        var allVertices = new List<VertexPositionNormalTexture>();
        var allIndices = new List<int>();
        _batches.Clear();

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        foreach (var mesh in geometries)
        {
            var vertices = BuildVertices(mesh);
            if (vertices == null || vertices.Length == 0)
            {
                ModelLoaderLog.Info($"[StaticModel] Skipped geometry '{mesh.GeometryId}': no vertices built");
                continue;
            }

            int[] indices = BuildIndices(mesh);
            if (indices.Length == 0) continue;

            Texture2D? texture = LoadTexture(device, baseDir, mesh.MaterialSymbol,
                symbolToMaterial, materialToImage);

            int baseVertex = allVertices.Count;
            int startIndex = allIndices.Count;

            allVertices.AddRange(vertices);
            for (int i = 0; i < indices.Length; i++)
                allIndices.Add(baseVertex + indices[i]);

            int primCount = indices.Length / 3;
            if (primCount > 0)
            {
                _batches.Add(new MeshBatch
                {
                    StartIndex = startIndex,
                    PrimitiveCount = primCount,
                    Texture = texture,
                    IsFace = false
                });
            }

            foreach (var v in vertices)
            {
                min = Vector3.Min(min, v.Position);
                max = Vector3.Max(max, v.Position);
            }

            ModelLoaderLog.Info($"[StaticModel] Geometry '{mesh.GeometryId}': {vertices.Length} verts, {primCount} tris, tex={texture != null}");
        }

        if (allVertices.Count == 0)
        {
            ModelLoaderLog.Info("[StaticModel] No vertices produced — model is empty");
            return;
        }

        BoundsMin = min;
        BoundsMax = max;
        VertexCount = allVertices.Count;

        _vertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration,
            allVertices.Count, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(allVertices.ToArray());

        _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits,
            allIndices.Count, BufferUsage.WriteOnly);
        _indexBuffer.SetData(allIndices.ToArray());

        ModelLoaderLog.Info($"[StaticModel] Loaded: {allVertices.Count} total verts, {_batches.Count} batches, bounds {min} -> {max}");
    }

    public void LoadFbx(GraphicsDevice device, string fbxPath)
    {
        Dispose();

        string baseDir = Path.GetDirectoryName(Path.GetFullPath(fbxPath)) ?? ".";

        using var importer = new AssimpContext();
        var scene = importer.ImportFile(fbxPath,
            PostProcessSteps.Triangulate |
            PostProcessSteps.GenerateNormals |
            PostProcessSteps.PreTransformVertices |
            PostProcessSteps.FlipUVs);

        if (scene == null || !scene.HasMeshes)
        {
            ModelLoaderLog.Info($"[StaticModel] FBX has no meshes: {fbxPath}");
            return;
        }

        ModelLoaderLog.Info($"[StaticModel] FBX meshes: {scene.MeshCount}, materials: {scene.MaterialCount}");

        var allVertices = new List<VertexPositionNormalTexture>();
        var allIndices = new List<int>();
        _batches.Clear();

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        foreach (var mesh in scene.Meshes)
        {
            if (!mesh.HasVertices) continue;

            var vertices = new VertexPositionNormalTexture[mesh.VertexCount];
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var p = mesh.Vertices[i];
                var pos = new Vector3(p.X, p.Y, p.Z);

                var nrm = Vector3.Up;
                if (mesh.HasNormals)
                {
                    var n = mesh.Normals[i];
                    nrm = new Vector3(n.X, n.Y, n.Z);
                }

                var uv = Vector2.Zero;
                if (mesh.HasTextureCoords(0))
                {
                    var t = mesh.TextureCoordinateChannels[0][i];
                    uv = new Vector2(t.X, t.Y);
                }

                vertices[i] = new VertexPositionNormalTexture(pos, nrm, uv);
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }

            var indices = mesh.GetIndices();
            if (indices.Length == 0) continue;

            Texture2D? texture = LoadFbxMaterialTexture(device, scene, mesh.MaterialIndex, baseDir);

            int baseVertex = allVertices.Count;
            int startIndex = allIndices.Count;

            allVertices.AddRange(vertices);
            for (int i = 0; i < indices.Length; i++)
                allIndices.Add(baseVertex + indices[i]);

            int primCount = indices.Length / 3;
            if (primCount > 0)
            {
                _batches.Add(new MeshBatch
                {
                    StartIndex = startIndex,
                    PrimitiveCount = primCount,
                    Texture = texture,
                    IsFace = false
                });
            }

            ModelLoaderLog.Info($"[StaticModel] FBX mesh '{mesh.Name}': {vertices.Length} verts, {primCount} tris, tex={texture != null}");
        }

        if (allVertices.Count == 0)
        {
            ModelLoaderLog.Info("[StaticModel] FBX produced no vertices — model is empty");
            return;
        }

        BoundsMin = min;
        BoundsMax = max;
        VertexCount = allVertices.Count;

        _vertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration,
            allVertices.Count, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(allVertices.ToArray());

        _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits,
            allIndices.Count, BufferUsage.WriteOnly);
        _indexBuffer.SetData(allIndices.ToArray());

        ModelLoaderLog.Info($"[StaticModel] FBX loaded: {allVertices.Count} total verts, {_batches.Count} batches, bounds {min} -> {max}");
    }

    private static Texture2D? LoadFbxMaterialTexture(GraphicsDevice device, Scene scene, int materialIndex, string baseDir)
    {
        if (materialIndex < 0 || materialIndex >= scene.MaterialCount)
        {
            ModelLoaderLog.Info($"[StaticModel] FBX tex: material index {materialIndex} out of range");
            return null;
        }

        var material = scene.Materials[materialIndex];
        ModelLoaderLog.Info($"[StaticModel] FBX tex: material '{material.Name}', hasDiffuse={material.HasTextureDiffuse}");

        if (!material.HasTextureDiffuse)
            return null;

        string texFile = material.TextureDiffuse.FilePath;
        ModelLoaderLog.Info($"[StaticModel] FBX tex: diffuse path='{texFile}'");

        if (string.IsNullOrWhiteSpace(texFile))
            return null;

        string? resolved = ResolveFbxTexturePath(baseDir, texFile);
        ModelLoaderLog.Info($"[StaticModel] FBX tex: resolved='{resolved}', exists={resolved != null && File.Exists(resolved)}");

        if (resolved == null || !File.Exists(resolved))
            return null;

        try
        {
            using var stream = File.OpenRead(resolved);
            var tex = Texture2D.FromStream(device, stream);
            return tex;
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[StaticModel] FBX tex: load failed: {ex.Message}");
            return null;
        }
    }

    private static string? ResolveFbxTexturePath(string baseDir, string texFile)
    {
        string cleaned = texFile.Replace('\\', '/').TrimStart('.', '/');

        string direct = Path.Combine(baseDir, cleaned);
        if (File.Exists(direct)) return direct;

        string fileName = Path.GetFileName(cleaned);
        string inBase = Path.Combine(baseDir, fileName);
        if (File.Exists(inBase)) return inBase;

        string inTextures = Path.Combine(baseDir, "textures", fileName);
        if (File.Exists(inTextures)) return inTextures;

        // Try parent directory's textures folder
        string? parentDir = Path.GetDirectoryName(baseDir);
        if (parentDir != null)
        {
            string inParentTextures = Path.Combine(parentDir, "textures", fileName);
            if (File.Exists(inParentTextures)) return inParentTextures;
        }

        return null;
    }

    public void Draw(GraphicsDevice device, BasicEffect effect)
    {
        if (_vertexBuffer == null || _indexBuffer == null || _batches.Count == 0) return;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        foreach (var batch in _batches)
        {
            if (batch.Texture is not null)
            {
                effect.TextureEnabled = true;
                effect.VertexColorEnabled = false;
                effect.Texture = batch.Texture;
            }
            else
            {
                effect.TextureEnabled = false;
                effect.VertexColorEnabled = false;
                effect.DiffuseColor = new Vector3(0.6f, 0.6f, 0.65f);
            }

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(
                    Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, 0, batch.StartIndex, batch.PrimitiveCount);
            }
        }
    }

    public void DrawAlphaTested(GraphicsDevice device, BasicEffect effect, AlphaTestEffect alphaTestEffect)
    {
        if (_vertexBuffer == null || _indexBuffer == null || _batches.Count == 0) return;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        foreach (var batch in _batches)
        {
            if (batch.Texture is not null)
            {
                alphaTestEffect.Texture = batch.Texture;

                foreach (var pass in alphaTestEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawIndexedPrimitives(
                        Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, 0, batch.StartIndex, batch.PrimitiveCount);
                }
            }
            else
            {
                effect.TextureEnabled = false;
                effect.VertexColorEnabled = false;
                effect.DiffuseColor = new Vector3(0.6f, 0.6f, 0.65f);

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawIndexedPrimitives(
                        Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, 0, batch.StartIndex, batch.PrimitiveCount);
                }
            }
        }
    }

    public void Dispose()
    {
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _indexBuffer?.Dispose();
        _indexBuffer = null;

        foreach (var batch in _batches)
            batch.Texture?.Dispose();
        _batches.Clear();

        BoundsMin = Vector3.Zero;
        BoundsMax = Vector3.Zero;
        VertexCount = 0;
    }

    private static VertexPositionNormalTexture[]? BuildVertices(MeshData mesh)
    {
        int posOffset = -1, nrmOffset = -1, uvOffset = -1;
        foreach (var inp in mesh.Inputs)
        {
            switch (inp.Semantic)
            {
                case "VERTEX": posOffset = inp.Offset; break;
                case "NORMAL": nrmOffset = inp.Offset; break;
                case "TEXCOORD": uvOffset = inp.Offset; break;
            }
        }

        if (posOffset < 0) return null;

        int stride = mesh.Stride;
        if (stride == 0) return null;

        int vertexCount = mesh.Indices.Length / stride;
        if (vertexCount == 0) return null;

        var vertices = new VertexPositionNormalTexture[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            int baseIdx = i * stride;

            int pi = mesh.Indices[baseIdx + posOffset] * 3;
            Vector3 pos = pi + 2 < mesh.Positions.Length
                ? new Vector3(mesh.Positions[pi], mesh.Positions[pi + 1], mesh.Positions[pi + 2])
                : Vector3.Zero;

            Vector3 nrm = Vector3.Up;
            if (nrmOffset >= 0)
            {
                int ni = mesh.Indices[baseIdx + nrmOffset] * 3;
                if (ni + 2 < mesh.Normals.Length)
                    nrm = new Vector3(mesh.Normals[ni], mesh.Normals[ni + 1], mesh.Normals[ni + 2]);
            }

            Vector2 uv = Vector2.Zero;
            if (uvOffset >= 0)
            {
                int ui = mesh.Indices[baseIdx + uvOffset] * 2;
                if (ui + 1 < mesh.UVs.Length)
                    uv = new Vector2(mesh.UVs[ui], 1f - mesh.UVs[ui + 1]);
            }

            vertices[i] = new VertexPositionNormalTexture(pos, nrm, uv);
        }

        return vertices;
    }

    private static int[] BuildIndices(MeshData mesh)
    {
        int vertexCount = mesh.Indices.Length / mesh.Stride;
        var indices = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++)
            indices[i] = i;
        return indices;
    }

    private static Texture2D? LoadTexture(
        GraphicsDevice device, string baseDir, string materialSymbol,
        Dictionary<string, string> symbolToMaterial,
        Dictionary<string, string> materialToImage)
    {
        if (string.IsNullOrWhiteSpace(materialSymbol)) return null;
        if (!symbolToMaterial.TryGetValue(materialSymbol, out string? matId)) return null;
        if (!materialToImage.TryGetValue(matId, out string? imgFile)) return null;

        string? texturePath = TextureResolver.ResolvePath(baseDir, imgFile);
        if (string.IsNullOrWhiteSpace(texturePath) || !File.Exists(texturePath)) return null;

        try
        {
            using var stream = File.OpenRead(texturePath);
            var texture = Texture2D.FromStream(device, stream);

            Color[] pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            for (int p = 0; p < pixels.Length; p++)
                pixels[p].A = 255;
            texture.SetData(pixels);

            return texture;
        }
        catch
        {
            return null;
        }
    }
}
