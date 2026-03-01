#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Helpers;

namespace Starfield2026.ModelLoader.Rendering;

public class TexturedTileRenderer
{
    private GraphicsDevice _device = null!;
    private BasicEffect _effect = null!;
    private Dictionary<RegionType, Texture2D> _textures = new();
    private VertexPositionTexture[] _quadVertices = new VertexPositionTexture[4];
    private short[] _quadIndices = { 0, 1, 2, 0, 2, 3 };

    public void Initialize(GraphicsDevice device, string textureFolder)
    {
        _device = device;

        _effect = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = false,
            LightingEnabled = false,
        };

        LoadTexture(device, textureFolder, RegionType.Grass, "Terrain Grass.png");
        LoadTexture(device, textureFolder, RegionType.Dirt, "Terrain Dirt.png");
        LoadTexture(device, textureFolder, RegionType.Rock, "Terrain Rock.png");

        // Fallback water texture - use solid color
        CreateSolidTexture(device, RegionType.Water, new Color(60, 120, 200));

        // Setup quad vertices (will be transformed per tile)
        _quadVertices[0] = new VertexPositionTexture(new Vector3(0, 0, 0), new Vector2(0, 1));
        _quadVertices[1] = new VertexPositionTexture(new Vector3(1, 0, 0), new Vector2(1, 1));
        _quadVertices[2] = new VertexPositionTexture(new Vector3(1, 0, 1), new Vector2(1, 0));
        _quadVertices[3] = new VertexPositionTexture(new Vector3(0, 0, 1), new Vector2(0, 0));
    }

    private void LoadTexture(GraphicsDevice device, string folder, RegionType type, string filename)
    {
        string path = System.IO.Path.Combine(folder, filename);
        if (!System.IO.File.Exists(path))
        {
            ModelLoaderLog.Info($"[TexturedTileRenderer] Texture not found: {path}");
            CreateSolidTexture(device, type, RegionTextures.GetDebugColor(type));
            return;
        }

        try
        {
            using var stream = System.IO.File.OpenRead(path);
            var texture = Texture2D.FromStream(device, stream);
            _textures[type] = texture;
            ModelLoaderLog.Info($"[TexturedTileRenderer] Loaded texture: {filename}");
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[TexturedTileRenderer] Failed to load {path}: {ex.Message}");
            CreateSolidTexture(device, type, RegionTextures.GetDebugColor(type));
        }
    }

    private void CreateSolidTexture(GraphicsDevice device, RegionType type, Color color)
    {
        var texture = new Texture2D(device, 1, 1);
        texture.SetData(new[] { color });
        _textures[type] = texture;
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection, RegionMap map, Vector3? cameraPos = null)
    {
        float cullDist = 120f;
        var camPos = cameraPos ?? Vector3.Zero;

        _effect.View = view;
        _effect.Projection = projection;

        device.RasterizerState = RasterizerState.CullNone;
        device.DepthStencilState = DepthStencilState.Default;

        foreach (var kvp in _textures)
        {
            var type = kvp.Key;
            var texture = kvp.Value;

            _effect.Texture = texture;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                float cellSize = map.CellSize;
                var origin = map.Origin;

                for (int x = 0; x < map.Width; x++)
                {
                    for (int z = 0; z < map.Depth; z++)
                    {
                        var cell = map[x, z];
                        if (cell.Type != type) continue;

                        float worldX = origin.X + x * cellSize;
                        float worldZ = origin.Z + z * cellSize;

                        // Distance culling
                        float dx = worldX + cellSize * 0.5f - camPos.X;
                        float dz = worldZ + cellSize * 0.5f - camPos.Z;
                        if (dx * dx + dz * dz > cullDist * cullDist) continue;

                        // Update vertices for this tile
                        _quadVertices[0].Position = new Vector3(worldX, 0, worldZ);
                        _quadVertices[1].Position = new Vector3(worldX + cellSize, 0, worldZ);
                        _quadVertices[2].Position = new Vector3(worldX + cellSize, 0, worldZ + cellSize);
                        _quadVertices[3].Position = new Vector3(worldX, 0, worldZ + cellSize);

                        _effect.World = Matrix.Identity;

                        device.DrawUserIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            _quadVertices, 0, 4,
                            _quadIndices, 0, 2);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
            texture.Dispose();
        _textures.Clear();
        _effect?.Dispose();
    }
}
