#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.DTOs;
using Starfield2026.ModelLoader.Loaders;

namespace Starfield2026.ModelLoader.Rendering;

public sealed class SkinnedModel : IDisposable
{
    private readonly List<(SkinnedVertex[] Vertices, int[] Indices, Texture2D? Texture, bool IsFace)> _meshes = new();
    private readonly List<MeshBatch> _batches = new();

    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }

    private static readonly DepthStencilState FaceDepthState = new()
    {
        DepthBufferEnable = true,
        DepthBufferWriteEnable = true,
        DepthBufferFunction = CompareFunction.LessEqual
    };

    public void Load(GraphicsDevice device, string daePath, Skeleton skeleton)
    {
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(daePath)) ?? ".";
        var doc = XDocument.Load(daePath);

        var geometries = MeshLoader.Load(doc);
        var skins = SkinLoader.Load(doc, skeleton);
        var materialToImage = TextureResolver.ParseMaterialImageMap(doc);
        var symbolToMaterial = TextureResolver.ParseBindMaterialMap(doc);

        // Fallback: v2 DAEs may lack bind_material entirely.
        // Build synthetic mapping by matching symbol suffixes to material IDs.
        if (symbolToMaterial.Count == 0 && materialToImage.Count > 0)
        {
            foreach (var geometry in geometries)
            {
                string sym = geometry.MaterialSymbol;
                if (string.IsNullOrEmpty(sym) || symbolToMaterial.ContainsKey(sym)) continue;

                // Extract suffix after "_Mtl_" (e.g. "Mdl_0_Mtl_BodyA00" → "BodyA00")
                int idx = sym.LastIndexOf("_Mtl_", StringComparison.OrdinalIgnoreCase);
                string suffix = idx >= 0 ? sym.Substring(idx + 5) : sym;

                foreach (var matId in materialToImage.Keys)
                {
                    if (matId.Contains(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        symbolToMaterial[sym] = matId;
                        break;
                    }
                }
            }
        }

        _meshes.Clear();
        _batches.Clear();

        foreach (var geometry in geometries)
        {
            if (!skins.TryGetValue(geometry.GeometryId, out var skinData))
                continue;

            var vertices = MeshBuilder.Build(geometry, skinData, out int[] indices);
            if (vertices is null) continue;

            Texture2D? texture = LoadTexture(device, baseDir, geometry.MaterialSymbol,
                symbolToMaterial, materialToImage);

            bool isFace = !string.IsNullOrEmpty(geometry.MaterialSymbol) &&
                (geometry.MaterialSymbol.Contains("Eye", StringComparison.OrdinalIgnoreCase) ||
                 geometry.MaterialSymbol.Contains("Mouth", StringComparison.OrdinalIgnoreCase));

            _meshes.Add((vertices, indices, texture, isFace));
        }

        RebuildBuffers(device, skeleton.InverseBindTransforms);
        ComputeBoundsFromBindPose();
    }

    public void UpdatePose(GraphicsDevice device, Matrix[] skinPose)
    {
        RebuildBuffers(device, skinPose);
    }

    public void ComputeSkinnedBounds(Matrix[] skinPose)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        foreach (var (vertices, _, _, _) in _meshes)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 pos = CpuSkinner.TransformPosition(ref vertices[i], skinPose);
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

    public void Draw(GraphicsDevice device, BasicEffect effect)
    {
        if (VertexBuffer is null || IndexBuffer is null || _batches.Count == 0) return;

        device.SetVertexBuffer(VertexBuffer);
        device.Indices = IndexBuffer;

        DrawBatches(device, effect, isFace: false);

        var prevDepth = device.DepthStencilState;
        device.DepthStencilState = FaceDepthState;
        DrawBatches(device, effect, isFace: true);
        device.DepthStencilState = prevDepth;
    }

    public void Dispose()
    {
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
        foreach (var (_, _, texture, _) in _meshes)
            texture?.Dispose();
        _meshes.Clear();
        _batches.Clear();
        VertexBuffer = null;
        IndexBuffer = null;
    }

    private void DrawBatches(GraphicsDevice device, BasicEffect effect, bool isFace)
    {
        foreach (var batch in _batches)
        {
            if (batch.IsFace != isFace) continue;

            effect.Texture = batch.Texture;
            effect.TextureEnabled = batch.Texture is not null;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList, 0, batch.StartIndex, batch.PrimitiveCount);
            }
        }
    }

    private void RebuildBuffers(GraphicsDevice device, Matrix[] skinMatrices)
    {
        var allVertices = new List<VertexPositionNormalTexture>();
        var allIndices = new List<int>();
        _batches.Clear();

        foreach (var (vertices, meshIndices, texture, isFace) in _meshes)
        {
            int baseVertex = allVertices.Count;
            int startIndex = allIndices.Count;

            var transformed = new VertexPositionNormalTexture[vertices.Length];
            CpuSkinner.Transform(vertices, transformed, skinMatrices);
            allVertices.AddRange(transformed);

            for (int i = 0; i < meshIndices.Length; i++)
                allIndices.Add(baseVertex + meshIndices[i]);

            int primCount = meshIndices.Length / 3;
            if (primCount > 0)
            {
                _batches.Add(new MeshBatch
                {
                    StartIndex = startIndex,
                    PrimitiveCount = primCount,
                    Texture = texture,
                    IsFace = isFace
                });
            }
        }

        if (allVertices.Count == 0) return;

        VertexBuffer?.Dispose();
        VertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration,
            allVertices.Count, BufferUsage.WriteOnly);
        VertexBuffer.SetData(allVertices.ToArray());

        IndexBuffer?.Dispose();
        IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits,
            allIndices.Count, BufferUsage.WriteOnly);
        IndexBuffer.SetData(allIndices.ToArray());
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

        using var stream = File.OpenRead(texturePath);
        var texture = Texture2D.FromStream(device, stream);

        Color[] pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        for (int p = 0; p < pixels.Length; p++)
            pixels[p].A = 255;
        texture.SetData(pixels);

        return texture;
    }

    private void ComputeBoundsFromBindPose()
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        foreach (var (vertices, _, _, _) in _meshes)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i].Position);
                max = Vector3.Max(max, vertices[i].Position);
            }
        }

        if (min.X != float.MaxValue)
        {
            BoundsMin = min;
            BoundsMax = max;
        }
    }
}
