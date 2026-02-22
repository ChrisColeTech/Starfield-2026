#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering.Skeletal;

public class SkinnedDaeModel : IDisposable
{
    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    public int PrimitiveCount { get; private set; }
    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }
    public Texture2D? Texture { get; private set; }

    // Bind-pose data for CPU skinning
    private Vector3[] _bindPositions = Array.Empty<Vector3>();
    private Vector3[] _bindNormals = Array.Empty<Vector3>();
    private Vector2[] _texCoords = Array.Empty<Vector2>();
    private int[] _indices = Array.Empty<int>();
    private ColladaSkeletalLoader.VertexBoneWeight[] _boneWeights = Array.Empty<ColladaSkeletalLoader.VertexBoneWeight>();
    private int[] _skinBoneMap = Array.Empty<int>(); // maps skin joint index → rig bone index
    private VertexPositionNormalTexture[] _cpuVertices = Array.Empty<VertexPositionNormalTexture>();

    public void Load(GraphicsDevice device, string daePath, SkeletonRig rig, string? texturePath = null)
    {
        // Load geometry
        var (positions, normals, texCoords, indices) = ColladaSkeletalLoader.LoadGeometry(daePath);
        _bindPositions = positions;
        _bindNormals = normals;
        _texCoords = texCoords;
        _indices = indices;

        // Load skin weights
        var (weights, jointNames) = ColladaSkeletalLoader.LoadSkinWeights(daePath);

        // Build joint name → rig bone index map
        _skinBoneMap = new int[jointNames.Length];
        for (int i = 0; i < jointNames.Length; i++)
        {
            if (!rig.TryGetBoneIndex(jointNames[i], out int boneIdx))
                boneIdx = 0; // fallback to root
            _skinBoneMap[i] = boneIdx;
        }

        // Map weight bone indices (skin-local) through skinBoneMap
        _boneWeights = new ColladaSkeletalLoader.VertexBoneWeight[positions.Length];
        for (int i = 0; i < positions.Length && i < weights.Length; i++)
        {
            var w = weights[i];
            w.Bone0 = w.Bone0 < _skinBoneMap.Length ? _skinBoneMap[w.Bone0] : 0;
            w.Bone1 = w.Bone1 < _skinBoneMap.Length ? _skinBoneMap[w.Bone1] : 0;
            w.Bone2 = w.Bone2 < _skinBoneMap.Length ? _skinBoneMap[w.Bone2] : 0;
            w.Bone3 = w.Bone3 < _skinBoneMap.Length ? _skinBoneMap[w.Bone3] : 0;
            _boneWeights[i] = w;
        }

        // Build initial vertex buffer
        _cpuVertices = new VertexPositionNormalTexture[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            _cpuVertices[i] = new VertexPositionNormalTexture(positions[i], normals[i], texCoords[i]);
        }

        VertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration,
            _cpuVertices.Length, BufferUsage.WriteOnly);
        VertexBuffer.SetData(_cpuVertices);

        IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits,
            _indices.Length, BufferUsage.WriteOnly);
        IndexBuffer.SetData(_indices);

        PrimitiveCount = _indices.Length / 3;

        // Compute bind-pose bounds
        ComputeBounds(_bindPositions);

        // Load texture if provided
        if (texturePath != null && File.Exists(texturePath))
            Texture = LoadTexture(device, texturePath);
    }

    public void UpdatePose(GraphicsDevice device, Matrix[] skinPose)
    {
        for (int i = 0; i < _cpuVertices.Length; i++)
        {
            var w = _boneWeights[i];
            var bindPos = _bindPositions[i];
            var bindNorm = _bindNormals[i];

            Matrix blended =
                skinPose[w.Bone0] * w.Weight0 +
                skinPose[w.Bone1] * w.Weight1 +
                skinPose[w.Bone2] * w.Weight2 +
                skinPose[w.Bone3] * w.Weight3;

            _cpuVertices[i].Position = Vector3.Transform(bindPos, blended);
            _cpuVertices[i].Normal = Vector3.TransformNormal(bindNorm, blended);
        }

        VertexBuffer?.SetData(_cpuVertices);
    }

    public void ComputeSkinnedBounds(Matrix[] skinPose)
    {
        var positions = new Vector3[_cpuVertices.Length];
        for (int i = 0; i < _cpuVertices.Length; i++)
        {
            var w = _boneWeights[i];
            Matrix blended =
                skinPose[w.Bone0] * w.Weight0 +
                skinPose[w.Bone1] * w.Weight1 +
                skinPose[w.Bone2] * w.Weight2 +
                skinPose[w.Bone3] * w.Weight3;
            positions[i] = Vector3.Transform(_bindPositions[i], blended);
        }
        ComputeBounds(positions);
    }

    public void Draw(GraphicsDevice device, BasicEffect effect)
    {
        if (VertexBuffer == null || IndexBuffer == null || PrimitiveCount == 0)
            return;

        device.SetVertexBuffer(VertexBuffer);
        device.Indices = IndexBuffer;

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount);
        }
    }

    public void Dispose()
    {
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
        Texture?.Dispose();
        VertexBuffer = null;
        IndexBuffer = null;
        Texture = null;
    }

    private void ComputeBounds(Vector3[] positions)
    {
        if (positions.Length == 0) return;
        var min = positions[0];
        var max = positions[0];
        for (int i = 1; i < positions.Length; i++)
        {
            min = Vector3.Min(min, positions[i]);
            max = Vector3.Max(max, positions[i]);
        }
        BoundsMin = min;
        BoundsMax = max;
    }

    private static Texture2D LoadTexture(GraphicsDevice device, string path)
    {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(device, stream);
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
