#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Loaders;
using Starfield2026.ModelLoader.Maps;

namespace Starfield2026.ModelLoader.Rendering;

public sealed class MapRenderer
{
    private const float TerrainSampleRadius = 0.45f;

    private CubeRenderer _cubeRenderer = null!;
    private QuadRenderer _quadRenderer = null!;
    private BasicEffect _modelEffect = null!;
    private AlphaTestEffect _alphaTestEffect = null!;

    public void Initialize(GraphicsDevice device)
    {
        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);

        _quadRenderer = new QuadRenderer();
        _quadRenderer.Initialize(device);

        _modelEffect = new BasicEffect(device)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
        };
        _modelEffect.EnableDefaultLighting();

        _alphaTestEffect = new AlphaTestEffect(device)
        {
            AlphaFunction = CompareFunction.Greater,
            ReferenceAlpha = 128,
            VertexColorEnabled = false,
        };
    }

    public float? SampleHeight(Vector3 worldPos, MapDefinition map, TileModelCache cache)
    {
        int x = Math.Clamp((int)MathF.Floor(worldPos.X + 0.5f), 0, map.Width - 1);
        int z = Math.Clamp((int)MathF.Floor(worldPos.Z + 0.5f), 0, map.Height - 1);

        float? baseHit = SampleTileHeight(worldPos, x, z, map.GetBaseTile(x, z), cache);
        int? overlayId = map.GetOverlayTile(x, z);
        float? overlayHit = overlayId.HasValue ? SampleTileHeight(worldPos, x, z, overlayId.Value, cache) : null;

        if (baseHit.HasValue && overlayHit.HasValue)
            return Math.Max(baseHit.Value, overlayHit.Value);

        return overlayHit ?? baseHit;
    }

    private static float? SampleTileHeight(Vector3 worldPos, int x, int y, int tileId, TileModelCache cache)
    {
        var tileDef = TileRegistry.GetTile(tileId);
        if (tileDef == null) return null;

        if (tileDef.Category != TileCategory.Structure && tileDef.Category != TileCategory.Terrain)
            return null;

        return tileDef.Height;
    }

    public bool IsPassable(Vector3 worldPos, float radius, MapDefinition map, TileModelCache cache)
    {
        int px = (int)MathF.Floor(worldPos.X + 0.5f);
        int pz = (int)MathF.Floor(worldPos.Z + 0.5f);

        const int searchRadius = 4;
        int minX = Math.Max(0, px - searchRadius);
        int maxX = Math.Min(map.Width - 1, px + searchRadius);
        int minZ = Math.Max(0, pz - searchRadius);
        int maxZ = Math.Min(map.Height - 1, pz + searchRadius);

        for (int gz = minZ; gz <= maxZ; gz++)
        {
            for (int gx = minX; gx <= maxX; gx++)
            {
                int? overlayId = map.GetOverlayTile(gx, gz);
                if (overlayId.HasValue && IsObstacleCircleOverlap(worldPos, radius, gx, gz, overlayId.Value, cache))
                    return false;

                int baseTileId = map.GetBaseTile(gx, gz);
                if (IsObstacleCircleOverlap(worldPos, radius, gx, gz, baseTileId, cache))
                    return false;
            }
        }

        return true;
    }

    private static bool IsObstacleCircleOverlap(Vector3 worldPos, float playerRadius, int x, int y, int tileId, TileModelCache cache)
    {
        var tileDef = TileRegistry.GetTile(tileId);
        if (tileDef == null) return false;

        if (tileDef.Category != TileCategory.Decoration) return false;

        if (tileDef.Walkable) return false;

        float obstacleRadius = MathHelper.Clamp(tileDef.BaselineSize * tileDef.Scale * 0.22f, 0.2f, 0.55f);
        float combinedRadius = playerRadius + obstacleRadius;

        float dx = worldPos.X - x;
        float dz = worldPos.Z - y;
        return dx * dx + dz * dz <= combinedRadius * combinedRadius;
    }

    private const int ScatterCount = 6;
    private const float ScatterSpread = 0.7f;

    private static uint Hash(int v)
    {
        uint h = (uint)v;
        h ^= h >> 16; h *= 0x45d9f3b; h ^= h >> 16; h *= 0x45d9f3b; h ^= h >> 16;
        return h;
    }

    private static float HashFloat(uint hash, int channel)
    {
        uint h = hash ^ (uint)(channel * 0x9E3779B9);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xFFFF) / 65535f;
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        MapDefinition map, TileModelCache cache, Vector3 cameraPosition = default)
    {
        device.RasterizerState = RenderStates.CullNone;

        _modelEffect.View = view;
        _modelEffect.Projection = projection;
        _alphaTestEffect.View = view;
        _alphaTestEffect.Projection = projection;

        var frustum = new BoundingFrustum(view * projection);

        device.BlendState = BlendState.Opaque;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                if (!IsTileVisible(frustum, x, y)) continue;
                DrawTilePass(device, view, projection, x, y, map, cache, alphaPass: false, cameraPosition);
            }

        device.BlendState = BlendState.NonPremultiplied;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                if (!IsTileVisible(frustum, x, y)) continue;
                DrawTilePass(device, view, projection, x, y, map, cache, alphaPass: true, cameraPosition);
            }

        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
    }

    public void DrawWithOffset(GraphicsDevice device, Matrix view, Matrix projection,
        MapDefinition map, TileModelCache cache, int offsetX, int offsetZ, bool alphaPass, Vector3 cameraPosition = default)
    {
        var frustum = new BoundingFrustum(view * projection);

        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int wx = x + offsetX;
                int wz = y + offsetZ;
                if (!IsTileVisible(frustum, wx, wz)) continue;

                int baseTileId = map.GetBaseTile(x, y);
                int? overlayId = map.GetOverlayTile(x, y);

                DrawTile(device, view, projection, wx, wz, baseTileId, cache, alphaPass);
                if (overlayId.HasValue)
                    DrawTile(device, view, projection, wx, wz, overlayId.Value, cache, alphaPass);
            }
    }

    private static bool IsTileVisible(BoundingFrustum frustum, int x, int y)
    {
        var tileSphere = new BoundingSphere(new Vector3(x, 2f, y), 5f);
        return frustum.Intersects(tileSphere);
    }

    private void DrawTilePass(GraphicsDevice device, Matrix view, Matrix projection,
        int x, int y, MapDefinition map, TileModelCache cache, bool alphaPass, Vector3 camPos)
    {
        int baseTileId = map.GetBaseTile(x, y);
        int? overlayId = map.GetOverlayTile(x, y);

        DrawTile(device, view, projection, x, y, baseTileId, cache, alphaPass);

        if (overlayId.HasValue)
        {
            DrawTile(device, view, projection, x, y, overlayId.Value, cache, alphaPass);
        }
    }

    private void DrawTile(GraphicsDevice device, Matrix view, Matrix projection,
        int x, int y, int tileId, TileModelCache cache, bool alphaPass)
    {
        var tileDef = TileRegistry.GetTile(tileId);
        if (tileDef == null) return;

        // 3D model path (opaque pass only)
        if (!alphaPass && !string.IsNullOrEmpty(tileDef.ModelId) &&
            cache.TryGetModel(tileDef.ModelId, out var model))
        {
            DrawTileModel(device, view, projection, x, y, tileDef, model);
            return;
        }

        // Non-model tiles only in opaque pass
        if (alphaPass) return;

        // Flat textured quad
        if (!string.IsNullOrEmpty(tileDef.TexturePath) &&
            cache.TryGetTexture(tileDef.TexturePath, out var texture))
        {
            float quadHeight = Math.Max(0.01f, tileDef.Height);
            var quadPos = new Vector3(x, quadHeight, y);
            _quadRenderer.Draw(device, view, projection, texture, quadPos);
            return;
        }

        // Fallback: colored cube
        float height = Math.Max(0.1f, tileDef.Height > 0 ? tileDef.Height : 0.15f);
        Color color = ParseHexColor(tileDef.Color);
        var cubePos = new Vector3(x, height / 2f, y);
        var cubeScale = new Vector3(0.95f, height, 0.95f);
        _cubeRenderer.Draw(device, view, projection, cubePos, 0f, cubeScale, color);
    }

    private void DrawTileModel(GraphicsDevice device, Matrix view, Matrix projection,
        int x, int y, TileDefinition tileDef, StaticModel model)
    {
        // Detect Z-up models: if Z extent is significantly taller than Y extent, rotate to Y-up
        float extentY = model.BoundsMax.Y - model.BoundsMin.Y;
        float extentZ = model.BoundsMax.Z - model.BoundsMin.Z;
        bool isZUp = extentZ > extentY * 1.5f;

        float modelDiameter = Math.Max(
            model.BoundsMax.X - model.BoundsMin.X,
            Math.Max(extentY, extentZ));

        float targetSize = tileDef.BaselineSize * tileDef.Scale;
        float scale = modelDiameter > 0.001f ? targetSize / modelDiameter : 1f;

        Vector3 modelCenter = model.Center;

        Matrix world;
        if (isZUp)
        {
            // Rotate Z-up to Y-up: -90° around X
            // Center XY only, align bottom (BoundsMin.Z) to ground before rotation
            world =
                Matrix.CreateTranslation(-modelCenter.X, -modelCenter.Y, -model.BoundsMin.Z) *
                Matrix.CreateRotationX(-MathF.PI / 2f) *
                Matrix.CreateScale(scale) *
                Matrix.CreateTranslation(x, 0f, y);
        }
        else
        {
            // Center XZ only, align bottom (BoundsMin.Y) to ground
            world =
                Matrix.CreateTranslation(-modelCenter.X, -model.BoundsMin.Y, -modelCenter.Z) *
                Matrix.CreateScale(scale) *
                Matrix.CreateTranslation(x, 0f, y);
        }

        _modelEffect.World = world;
        _modelEffect.View = view;
        _modelEffect.Projection = projection;

        _alphaTestEffect.World = world;
        _alphaTestEffect.View = view;
        _alphaTestEffect.Projection = projection;

        model.DrawAlphaTested(device, _modelEffect, _alphaTestEffect);
    }

    private static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#') return Color.Magenta;
        hex = hex.TrimStart('#');
        if (hex.Length >= 6)
        {
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            return new Color(r, g, b);
        }
        return Color.Magenta;
    }
}
