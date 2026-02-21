using System;
using System.Collections.Generic;
using System.IO;
using Assimp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering.Battle;



public static class BattleModelLoader
{
    public static BattleModelData Load(string daeFilePath, GraphicsDevice graphicsDevice)
    {
        var result = new BattleModelData();
        string directory = Path.GetDirectoryName(daeFilePath) ?? "";

        string folderPrefix = Path.GetFileName(directory) ?? "";
        var folderTextures = new List<string>();
        if (!string.IsNullOrEmpty(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, $"{folderPrefix}_*.png"))
            {
                string name = Path.GetFileName(file);
                if (name.Contains("Nor", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Dummy", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Inc", StringComparison.OrdinalIgnoreCase))
                    continue;
                folderTextures.Add(file);
            }
            folderTextures.Sort(StringComparer.OrdinalIgnoreCase);
        }

        using var importer = new AssimpContext();
        var scene = importer.ImportFile(daeFilePath,
            PostProcessSteps.Triangulate |
            PostProcessSteps.GenerateSmoothNormals |
            PostProcessSteps.FlipUVs |
            PostProcessSteps.PreTransformVertices);

        if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete))
            throw new InvalidOperationException($"Failed to load model: {daeFilePath}");

        for (int m = 0; m < scene.MeshCount; m++)
        {
            var mesh = scene.Meshes[m];

            bool hasUVs = mesh.HasTextureCoords(0);
            var vertices = new VertexPositionNormalTexture[mesh.VertexCount];
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var pos = mesh.Vertices[i];
                var normal = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 1, 0);
                var uv = hasUVs ? mesh.TextureCoordinateChannels[0][i] : new Vector3D(0, 0, 0);

                vertices[i] = new VertexPositionNormalTexture(
                    new Vector3(pos.X, pos.Y, pos.Z),
                    new Vector3(normal.X, normal.Y, normal.Z),
                    new Vector2(uv.X, uv.Y));
            }

            var indices = new List<int>();
            for (int f = 0; f < mesh.FaceCount; f++)
            {
                var face = mesh.Faces[f];
                for (int fi = 0; fi < face.IndexCount; fi++)
                    indices.Add(face.Indices[fi]);
            }

            Texture2D? texture = null;
            if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
            {
                var material = scene.Materials[mesh.MaterialIndex];
                string? texturePath = null;

                if (material.HasTextureDiffuse && !string.IsNullOrEmpty(material.TextureDiffuse.FilePath))
                {
                    texturePath = ResolveDiffusePath(directory, material.TextureDiffuse.FilePath);
                }

                if (texturePath == null && !string.IsNullOrEmpty(material.Name))
                {
                    string matName = material.Name.Replace("_mat", "");
                    string folderName = Path.GetFileName(directory) ?? "";
                    texturePath = FindTextureForMaterial(directory, folderName, matName);
                }

                if (texturePath == null && m < folderTextures.Count)
                    texturePath = folderTextures[m];

                if (texturePath != null)
                {
                    using var stream = File.OpenRead(texturePath);
                    texture = Texture2D.FromStream(graphicsDevice, stream);
                }
            }

            foreach (var v in vertices)
            {
                if (v.Position.X < result.BoundsMin.X) result.BoundsMin = new Vector3(v.Position.X, result.BoundsMin.Y, result.BoundsMin.Z);
                if (v.Position.Y < result.BoundsMin.Y) result.BoundsMin = new Vector3(result.BoundsMin.X, v.Position.Y, result.BoundsMin.Z);
                if (v.Position.Z < result.BoundsMin.Z) result.BoundsMin = new Vector3(result.BoundsMin.X, result.BoundsMin.Y, v.Position.Z);
                if (v.Position.X > result.BoundsMax.X) result.BoundsMax = new Vector3(v.Position.X, result.BoundsMax.Y, result.BoundsMax.Z);
                if (v.Position.Y > result.BoundsMax.Y) result.BoundsMax = new Vector3(result.BoundsMax.X, v.Position.Y, result.BoundsMax.Z);
                if (v.Position.Z > result.BoundsMax.Z) result.BoundsMax = new Vector3(result.BoundsMax.X, result.BoundsMax.Y, v.Position.Z);
            }

            var vb = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.WriteOnly);
            vb.SetData(vertices);
            
            var ib = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
            ib.SetData(indices.ToArray());

            var meshData = new BattleMeshData
            {
                VertexBuffer = vb,
                IndexBuffer = ib,
                Texture = texture,
                PrimitiveCount = indices.Count / 3
            };
            
            result.Meshes.Add(meshData);
        }

        return result;
    }

    internal static string? ResolveDiffusePath(string directory, string diffusePath)
    {
        var candidate = Path.Combine(directory, diffusePath);
        if (File.Exists(candidate))
            return candidate;

        string cleaned = diffusePath;
        if (cleaned.EndsWith("_id", StringComparison.Ordinal))
            cleaned = cleaned[..^3];

        candidate = Path.Combine(directory, cleaned);
        if (File.Exists(candidate))
            return candidate;

        candidate = Path.Combine(directory, cleaned + ".png");
        if (File.Exists(candidate))
            return candidate;

        string noExt = Path.GetFileNameWithoutExtension(cleaned);
        candidate = Path.Combine(directory, noExt + ".png");
        if (File.Exists(candidate))
            return candidate;

        return null;
    }

    internal static string? FindTextureForMaterial(string directory, string folderName, string matName)
    {
        string baseName = matName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        string digitStr = matName.Substring(baseName.Length);
        if (string.IsNullOrEmpty(baseName))
            baseName = matName;

        int texSuffix = 1;
        if (digitStr.Length > 0 && int.TryParse(digitStr, out int matIndex))
            texSuffix = matIndex + 1;

        var match = SearchTexture(directory, $"{folderName}_{baseName}{texSuffix}");
        if (match != null) return match;

        match = SearchTexture(directory, $"{folderName}_{baseName}");
        if (match != null) return match;

        match = SearchTexture(directory, $"{baseName}{texSuffix}");
        if (match != null) return match;

        match = SearchTexture(directory, $"{baseName}");
        if (match != null) return match;

        if (baseName.Length > 1 && (baseName[0] == 'L' || baseName[0] == 'R') && char.IsUpper(baseName[1]))
        {
            string stripped = baseName.Substring(1);
            match = SearchTexture(directory, $"{folderName}_{stripped}{texSuffix}");
            if (match != null) return match;
            match = SearchTexture(directory, $"{folderName}_{stripped}");
            if (match != null) return match;
            match = SearchTexture(directory, $"{stripped}");
            if (match != null) return match;
        }

        match = SearchTexture(directory, matName);
        return match;
    }

    private static string? SearchTexture(string directory, string prefix)
    {
        foreach (var file in Directory.EnumerateFiles(directory, $"{prefix}*.png"))
        {
            string name = Path.GetFileName(file);
            if (name.Contains("Nor", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Dummy", StringComparison.OrdinalIgnoreCase))
                continue;
            return file;
        }
        return null;
    }
}
