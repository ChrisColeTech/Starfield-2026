#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Assimp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Starfield2026.ModelLoader.Loaders;

public class FbxModel : IDisposable
{
    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private Texture2D? _texture;
    private int _primitiveCount;
    private Vector3[]? _meshPositions;
    private int[]? _meshIndices;

    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }
    public Vector3 Center => (BoundsMin + BoundsMax) * 0.5f;
    public float Radius => (BoundsMax - BoundsMin).Length() * 0.5f;
    public bool IsLoaded => _vertexBuffer != null;

    public void Load(GraphicsDevice device, string fbxPath, string? textureOverride = null)
    {
        Dispose();

        if (!File.Exists(fbxPath))
        {
            ModelLoaderLog.Info($"[FbxModel] File not found: {fbxPath}");
            return;
        }

        try
        {
            using var context = new AssimpContext();
            var scene = context.ImportFile(fbxPath, PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals);

            if (scene == null || scene.MeshCount == 0)
            {
                ModelLoaderLog.Info($"[FbxModel] No meshes in: {fbxPath}");
                return;
            }

            var vertices = new List<VertexPositionNormalTexture>();
            var indices = new List<int>();

            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);

            string? texPath = textureOverride ?? FindTexture(fbxPath, scene);

            for (int m = 0; m < scene.MeshCount; m++)
            {
                var mesh = scene.Meshes[m];
                int baseVertex = vertices.Count;

                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    var pos = mesh.Vertices[i];
                    var nrm = mesh.HasNormals ? mesh.Normals[i] : new Assimp.Vector3D(0, 1, 0);
                    var uv = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0][i] : new Assimp.Vector3D(0, 0, 0);

                    var vertex = new VertexPositionNormalTexture(
                        new Vector3(pos.X, pos.Y, pos.Z),
                        new Vector3(nrm.X, nrm.Y, nrm.Z),
                        new Vector2(uv.X, 1f - uv.Y));

                    vertices.Add(vertex);

                    min = Vector3.Min(min, vertex.Position);
                    max = Vector3.Max(max, vertex.Position);
                }

                for (int i = 0; i < mesh.FaceCount; i++)
                {
                    var face = mesh.Faces[i];
                    if (face.IndexCount == 3)
                    {
                        indices.Add(baseVertex + face.Indices[0]);
                        indices.Add(baseVertex + face.Indices[1]);
                        indices.Add(baseVertex + face.Indices[2]);
                    }
                }
            }

            if (vertices.Count == 0)
            {
                ModelLoaderLog.Info($"[FbxModel] No vertices: {fbxPath}");
                return;
            }

            _vertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration,
                vertices.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(vertices.ToArray());

            _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits,
                indices.Count, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices.ToArray());

            _primitiveCount = indices.Count / 3;
            BoundsMin = min;
            BoundsMax = max;

            // Store mesh data for raycasting
            _meshPositions = new Vector3[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
                _meshPositions[i] = vertices[i].Position;
            _meshIndices = indices.ToArray();

            if (texPath != null && File.Exists(texPath))
            {
                try
                {
                    using var stream = File.OpenRead(texPath);
                    _texture = Texture2D.FromStream(device, stream);
                    ModelLoaderLog.Info($"[FbxModel] Loaded texture: {Path.GetFileName(texPath)}");
                }
                catch (Exception ex)
                {
                    ModelLoaderLog.Info($"[FbxModel] Failed to load texture {texPath}: {ex.Message}");
                }
            }

            ModelLoaderLog.Info($"[FbxModel] Loaded: {Path.GetFileName(fbxPath)} ({vertices.Count} verts, {_primitiveCount} tris)");
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[FbxModel] Failed to load {fbxPath}: {ex.Message}");
        }
    }

    private static string? FindTexture(string modelPath, Scene scene)
    {
        string modelDir = Path.GetDirectoryName(modelPath) ?? "";
        string parentDir = Path.GetDirectoryName(modelDir) ?? "";
        string textureDir = Path.Combine(parentDir, "Textures");
        string coloredDir = Path.Combine(textureDir, "Colored");

        var searchDirs = new[] { coloredDir, textureDir, modelDir, parentDir };

        foreach (var material in scene.Materials)
        {
                if (material.HasTextureDiffuse && material.TextureDiffuse.FilePath != null)
                {
                    string texFile = material.TextureDiffuse.FilePath.Replace('\\', '/');
                    string texName = Path.GetFileName(texFile);
                    foreach (var dir in searchDirs)
                    {
                        string candidate = Path.Combine(dir, texName);
                        if (File.Exists(candidate)) return candidate;

                        string nameNoExt = Path.GetFileNameWithoutExtension(texName);
                        foreach (var ext in new[] { "_ALB.png", ".png", ".jpg" })
                        {
                            candidate = Path.Combine(dir, nameNoExt + ext);
                            if (File.Exists(candidate)) return candidate;
                        }
                    }
                }
            }

        // Fallback for Unity-style packs where diffuse path is missing or stripped.
        string stem = Path.GetFileNameWithoutExtension(modelPath).ToLowerInvariant();
        string? family = stem switch
        {
            var s when s.Contains("flower") => "Flower01_ALB.png",
            var s when s.Contains("grass") => "Grass01_ALB.png",
            var s when s.Contains("bush") => "Bush01_ALB.png",
            var s when s.Contains("tree") => "Leaf01_ALB.png",
            var s when s.Contains("bridge") => "Bridge01_ALB.png",
            var s when s.Contains("rock") || s.Contains("mountain") || s.Contains("pebble") => "Rock01_ALB.png",
            _ => null,
        };

        if (family != null)
        {
            foreach (var dir in searchDirs)
            {
                string candidate = Path.Combine(dir, family);
                if (File.Exists(candidate)) return candidate;
            }
        }

        // Last resort: first ALB texture in Colored, then Textures.
        if (Directory.Exists(coloredDir))
        {
            foreach (string p in Directory.EnumerateFiles(coloredDir, "*_ALB.png"))
                return p;
        }
        if (Directory.Exists(textureDir))
        {
            foreach (string p in Directory.EnumerateFiles(textureDir, "*_ALB.png"))
                return p;
        }

        return null;
    }

    public void Draw(GraphicsDevice device, BasicEffect effect, Matrix world)
    {
        if (_vertexBuffer == null || _indexBuffer == null) return;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        effect.World = world;

        if (_texture != null)
        {
            effect.TextureEnabled = true;
            effect.Texture = _texture;
            effect.DiffuseColor = Vector3.One;
            effect.Alpha = 1f;
        }
        else
        {
            effect.TextureEnabled = false;
            effect.DiffuseColor = new Vector3(0.6f, 0.6f, 0.65f);
            effect.Alpha = 1f;
        }

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, 0, 0, _primitiveCount);
        }
    }

    /// <summary>
    /// Sample the mesh height at a world XZ position by casting a vertical ray down.
    /// Returns the highest Y intersection, or null if no hit.
    /// </summary>
    public float? SampleHeight(Vector3 worldPos, Matrix world)
    {
        if (_meshPositions == null || _meshIndices == null) return null;

        Matrix inv = Matrix.Invert(world);

        Vector3 localPos = Vector3.Transform(worldPos, inv);
        float bestY = float.MinValue;
        bool hit = false;

        for (int i = 0; i < _meshIndices.Length; i += 3)
        {
            Vector3 v0 = _meshPositions[_meshIndices[i]];
            Vector3 v1 = _meshPositions[_meshIndices[i + 1]];
            Vector3 v2 = _meshPositions[_meshIndices[i + 2]];

            // Check if localPos.XZ is inside triangle XZ projection
            float? y = PointInTriangleY(localPos.X, localPos.Z, v0, v1, v2);
            if (y.HasValue && y.Value > bestY)
            {
                bestY = y.Value;
                hit = true;
            }
        }

        if (!hit) return null;

        // Transform the hit point back to world space
        Vector3 hitLocal = new Vector3(localPos.X, bestY, localPos.Z);
        Vector3 hitWorld = Vector3.Transform(hitLocal, world);
        return hitWorld.Y;
    }

    private static float? PointInTriangleY(float px, float pz, Vector3 a, Vector3 b, Vector3 c)
    {
        // Barycentric coordinate test on XZ plane
        float ax = a.X, az = a.Z;
        float bx = b.X, bz = b.Z;
        float cx = c.X, cz = c.Z;

        float d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
        if (MathF.Abs(d) < 1e-8f) return null;

        float u = ((bz - cz) * (px - cx) + (cx - bx) * (pz - cz)) / d;
        float v = ((cz - az) * (px - cx) + (ax - cx) * (pz - cz)) / d;
        float w = 1f - u - v;

        if (u < -0.001f || v < -0.001f || w < -0.001f) return null;

        return u * a.Y + v * b.Y + w * c.Y;
    }

    public void Dispose()
    {
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _indexBuffer?.Dispose();
        _indexBuffer = null;
        _texture?.Dispose();
        _texture = null;
        _primitiveCount = 0;
    }
}
