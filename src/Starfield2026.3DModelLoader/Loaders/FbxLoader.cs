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
                    string texFile = material.TextureDiffuse.FilePath;
                    foreach (var dir in searchDirs)
                    {
                        string candidate = Path.Combine(dir, texFile);
                        if (File.Exists(candidate)) return candidate;

                        string nameNoExt = Path.GetFileNameWithoutExtension(texFile);
                        foreach (var ext in new[] { "_ALB.png", ".png", ".jpg" })
                        {
                            candidate = Path.Combine(dir, nameNoExt + ext);
                            if (File.Exists(candidate)) return candidate;
                        }
                    }
                }
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
        }
        else
        {
            effect.TextureEnabled = false;
            effect.DiffuseColor = new Vector3(0.6f, 0.6f, 0.65f);
        }

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, 0, 0, _primitiveCount);
        }
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
