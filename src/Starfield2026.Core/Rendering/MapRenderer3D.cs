using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Rendering;

/// <summary>
/// Renders a MapDefinition as 3D colored geometry.
/// Walkable tiles → flat colored quads at y=0.
/// Non-walkable tiles with Height → extruded colored cubes.
/// </summary>
public class MapRenderer3D
{
    private BasicEffect _effect = null!;
    private readonly int _viewRadius;

    public MapRenderer3D(int viewRadius = 24)
    {
        _viewRadius = viewRadius;
    }

    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MapDefinition? map, Vector3 cameraCenter, int skipTileId = -1)
    {
        if (map == null) return;

        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;

        int tileSize = map.TileSize > 0 ? map.TileSize : 1;
        float scale = 2f; // world units per tile

        int centerTileX = (int)(cameraCenter.X / scale + map.Width / 2f);
        int centerTileZ = (int)(cameraCenter.Z / scale + map.Height / 2f);

        var floorVerts = new List<VertexPositionColor>();
        var wallVerts = new List<VertexPositionColor>();

        for (int ty = 0; ty < map.Height; ty++)
        {
            for (int tx = 0; tx < map.Width; tx++)
            {
                // Frustum cull by distance
                int dx = tx - centerTileX;
                int dz = ty - centerTileZ;
                if (dx * dx + dz * dz > _viewRadius * _viewRadius)
                    continue;

                int tileId = map.GetBaseTile(tx, ty);
                if (tileId == skipTileId) continue; // Skip background tile (e.g. wireframe grid floor)
                var tileDef = TileRegistry.GetTile(tileId);
                if (tileDef == null) continue;

                Color color = ParseHexColor(tileDef.Color);

                // World position: center the map at origin
                float wx = (tx - map.Width / 2f) * scale;
                float wz = (ty - map.Height / 2f) * scale;

                if (tileDef.Height > 0.01f && !tileDef.Walkable)
                {
                    // Extruded cube (wall/obstacle)
                    AddCube(wallVerts, wx, 0f, wz, scale, tileDef.Height, color);
                }
                else
                {
                    // Flat floor quad
                    AddFloorQuad(floorVerts, wx, 0f, wz, scale, color);
                }

                // Overlay tile
                int? overlayId = map.GetOverlayTile(tx, ty);
                if (overlayId is int oid)
                {
                    var overlayDef = TileRegistry.GetTile(oid);
                    if (overlayDef != null)
                    {
                        Color overlayColor = ParseHexColor(overlayDef.Color);
                        if (overlayDef.Height > 0.01f)
                            AddCube(wallVerts, wx, 0f, wz, scale, overlayDef.Height, overlayColor);
                        else
                            AddFloorQuad(floorVerts, wx, 0.01f, wz, scale, overlayColor);
                    }
                }
            }
        }

        // Draw floor tiles
        if (floorVerts.Count >= 3)
        {
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList,
                    floorVerts.ToArray(), 0, floorVerts.Count / 3);
            }
        }

        // Draw wall cubes
        if (wallVerts.Count >= 3)
        {
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList,
                    wallVerts.ToArray(), 0, wallVerts.Count / 3);
            }
        }
    }

    private static void AddFloorQuad(List<VertexPositionColor> verts, float x, float y, float z, float size, Color color)
    {
        // Two triangles forming a flat quad on the XZ plane
        var a = new Vector3(x, y, z);
        var b = new Vector3(x + size, y, z);
        var c = new Vector3(x + size, y, z + size);
        var d = new Vector3(x, y, z + size);

        verts.Add(new VertexPositionColor(a, color));
        verts.Add(new VertexPositionColor(b, color));
        verts.Add(new VertexPositionColor(c, color));

        verts.Add(new VertexPositionColor(a, color));
        verts.Add(new VertexPositionColor(c, color));
        verts.Add(new VertexPositionColor(d, color));
    }

    private static void AddCube(List<VertexPositionColor> verts, float x, float y, float z, float size, float height, Color color)
    {
        // 6 faces, 2 triangles each = 36 vertices
        float x1 = x, x2 = x + size;
        float y1 = y, y2 = y + height;
        float z1 = z, z2 = z + size;

        Color top = color;
        Color side = new Color((int)(color.R * 0.7f), (int)(color.G * 0.7f), (int)(color.B * 0.7f));
        Color dark = new Color((int)(color.R * 0.5f), (int)(color.G * 0.5f), (int)(color.B * 0.5f));

        // Top face
        AddQuad(verts, new Vector3(x1, y2, z1), new Vector3(x2, y2, z1), new Vector3(x2, y2, z2), new Vector3(x1, y2, z2), top);
        // Front face (z2)
        AddQuad(verts, new Vector3(x1, y1, z2), new Vector3(x2, y1, z2), new Vector3(x2, y2, z2), new Vector3(x1, y2, z2), side);
        // Back face (z1)
        AddQuad(verts, new Vector3(x2, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y2, z1), new Vector3(x2, y2, z1), side);
        // Left face (x1)
        AddQuad(verts, new Vector3(x1, y1, z1), new Vector3(x1, y1, z2), new Vector3(x1, y2, z2), new Vector3(x1, y2, z1), dark);
        // Right face (x2)
        AddQuad(verts, new Vector3(x2, y1, z2), new Vector3(x2, y1, z1), new Vector3(x2, y2, z1), new Vector3(x2, y2, z2), dark);
        // Bottom face
        AddQuad(verts, new Vector3(x1, y1, z2), new Vector3(x2, y1, z2), new Vector3(x2, y1, z1), new Vector3(x1, y1, z1), dark);
    }

    private static void AddQuad(List<VertexPositionColor> verts, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        verts.Add(new VertexPositionColor(a, color));
        verts.Add(new VertexPositionColor(b, color));
        verts.Add(new VertexPositionColor(c, color));

        verts.Add(new VertexPositionColor(a, color));
        verts.Add(new VertexPositionColor(c, color));
        verts.Add(new VertexPositionColor(d, color));
    }

    private static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Color.Magenta;

        hex = hex.TrimStart('#');
        if (hex.Length == 8)
        {
            int a = Convert.ToInt32(hex[..2], 16);
            int r = Convert.ToInt32(hex[2..4], 16);
            int g = Convert.ToInt32(hex[4..6], 16);
            int b = Convert.ToInt32(hex[6..8], 16);
            return new Color(r, g, b, a);
        }
        if (hex.Length == 6)
        {
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            return new Color(r, g, b);
        }
        return Color.Magenta;
    }
}
