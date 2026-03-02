#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Loaders;
using Starfield2026.ModelLoader.Maps;

namespace Starfield2026.ModelLoader.Rendering;

/// <summary>
/// Renders a tile map: 3D models, textured quads, or colored cube fallbacks.
/// One model per tile — no scatter or special-casing.
/// </summary>
public sealed class MapRenderer
{
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

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        MapDefinition map, TileModelCache cache, Vector3 cameraPosition = default)
    {
        device.RasterizerState = RenderStates.CullNone;

        _modelEffect.View = view;
        _modelEffect.Projection = projection;
        _alphaTestEffect.View = view;
        _alphaTestEffect.Projection = projection;

        var frustum = new BoundingFrustum(view * projection);

        // Opaque pass
        device.BlendState = BlendState.Opaque;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                if (!IsTileVisible(frustum, x, y)) continue;
                DrawTilePass(device, view, projection, x, y, map, cache, alphaPass: false);
            }

        // Alpha pass
        device.BlendState = BlendState.NonPremultiplied;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                if (!IsTileVisible(frustum, x, y)) continue;
                DrawTilePass(device, view, projection, x, y, map, cache, alphaPass: true);
            }

        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
    }

    public void DrawWithOffset(GraphicsDevice device, Matrix view, Matrix projection,
        MapDefinition map, TileModelCache cache, int offsetX, int offsetZ, bool alphaPass, Vector3 cameraPosition = default)
    {
        device.RasterizerState = RenderStates.CullNone;
        _modelEffect.View = view;
        _modelEffect.Projection = projection;
        _alphaTestEffect.View = view;
        _alphaTestEffect.Projection = projection;

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
        int x, int y, MapDefinition map, TileModelCache cache, bool alphaPass)
    {
        int baseTileId = map.GetBaseTile(x, y);
        int? overlayId = map.GetOverlayTile(x, y);

        DrawTile(device, view, projection, x, y, baseTileId, cache, alphaPass);

        if (overlayId.HasValue)
            DrawTile(device, view, projection, x, y, overlayId.Value, cache, alphaPass);
    }

    private void DrawTile(GraphicsDevice device, Matrix view, Matrix projection,
        int x, int y, int tileId, TileModelCache cache, bool alphaPass)
    {
        var tileDef = TileRegistry.GetTile(tileId);
        if (tileDef == null) return;

        // 3D model (opaque pass only)
        if (!alphaPass && !string.IsNullOrEmpty(tileDef.ModelId) &&
            cache.TryGetModel(tileDef.ModelId, out var model))
        {
            var world = BuildWorldMatrix(x, y, tileDef, model);
            if (tileDef.AlphaCutout)
                DrawModelAlphaTested(device, model, world);
            else
                DrawModelBasic(device, model, world);
            return;
        }

        if (alphaPass) return;

        // Flat textured quad
        if (!string.IsNullOrEmpty(tileDef.TexturePath) &&
            cache.TryGetTexture(tileDef.TexturePath, out var texture))
        {
            float quadHeight = Math.Max(0.01f, tileDef.Height);
            _quadRenderer.Draw(device, view, projection, texture, new Vector3(x, quadHeight, y));
            return;
        }

        // Fallback: colored cube
        float height = Math.Max(0.1f, tileDef.Height > 0 ? tileDef.Height : 0.15f);
        Color color = ParseHexColor(tileDef.Color);
        _cubeRenderer.Draw(device, view, projection,
            new Vector3(x, height / 2f, y), 0f, new Vector3(0.95f, height, 0.95f), color);
    }

    private static Matrix BuildWorldMatrix(int x, int y, TileDefinition tileDef, StaticModel model)
    {
        float extentX = model.BoundsMax.X - model.BoundsMin.X;
        float extentY = model.BoundsMax.Y - model.BoundsMin.Y;
        float extentZ = model.BoundsMax.Z - model.BoundsMin.Z;

        float modelDiameter = Math.Max(extentX, Math.Max(extentY, extentZ));
        float targetSize = tileDef.BaselineSize * tileDef.Scale;
        float scale = modelDiameter > 0.001f ? targetSize / modelDiameter : 1f;

        Vector3 center = model.Center;

        // Center horizontally, sit bottom on ground
        var baseTransform = Matrix.CreateTranslation(-center.X, -model.BoundsMin.Y, -center.Z);

        return baseTransform *
            Matrix.CreateScale(scale) *
            Matrix.CreateTranslation(x, 0f, y);
    }

    private void DrawModelAlphaTested(GraphicsDevice device, StaticModel model, Matrix world)
    {
        _modelEffect.World = world;
        _alphaTestEffect.World = world;
        model.DrawAlphaTested(device, _modelEffect, _alphaTestEffect);
    }

    private void DrawModelBasic(GraphicsDevice device, StaticModel model, Matrix world)
    {
        _modelEffect.World = world;
        model.Draw(device, _modelEffect);
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
