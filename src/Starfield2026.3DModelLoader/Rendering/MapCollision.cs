#nullable enable
using System;
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.Maps;

namespace Starfield2026.ModelLoader.Rendering;

/// <summary>
/// Handles terrain height sampling and obstacle passability checks for the map.
/// </summary>
public static class MapCollision
{
    public static float? SampleHeight(Vector3 worldPos, MapDefinition map, TileModelCache cache)
    {
        int x = Math.Clamp((int)MathF.Floor(worldPos.X + 0.5f), 0, map.Width - 1);
        int z = Math.Clamp((int)MathF.Floor(worldPos.Z + 0.5f), 0, map.Height - 1);

        float? baseHit = SampleTileHeight(map.GetBaseTile(x, z));
        int? overlayId = map.GetOverlayTile(x, z);
        float? overlayHit = overlayId.HasValue ? SampleTileHeight(overlayId.Value) : null;

        if (baseHit.HasValue && overlayHit.HasValue)
            return Math.Max(baseHit.Value, overlayHit.Value);

        return overlayHit ?? baseHit;
    }

    private static float? SampleTileHeight(int tileId)
    {
        var tileDef = TileRegistry.GetTile(tileId);
        if (tileDef == null) return null;

        if (tileDef.Category != TileCategory.Structure && tileDef.Category != TileCategory.Terrain)
            return null;

        return tileDef.Height;
    }

    public static bool IsPassable(Vector3 worldPos, float radius, MapDefinition map)
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
                if (overlayId.HasValue && IsObstacleOverlap(worldPos, radius, gx, gz, overlayId.Value))
                    return false;

                int baseTileId = map.GetBaseTile(gx, gz);
                if (IsObstacleOverlap(worldPos, radius, gx, gz, baseTileId))
                    return false;
            }
        }

        return true;
    }

    private static bool IsObstacleOverlap(Vector3 worldPos, float playerRadius, int x, int y, int tileId)
    {
        var tileDef = TileRegistry.GetTile(tileId);
        if (tileDef == null) return false;
        if (tileDef.Category != TileCategory.Decoration) return false;
        if (tileDef.Walkable) return false;

        float obstacleRadius = MathHelper.Clamp(tileDef.BaselineSize * tileDef.Scale * 0.22f, 0.2f, 0.55f);
        float combined = playerRadius + obstacleRadius;

        float dx = worldPos.X - x;
        float dz = worldPos.Z - y;
        return dx * dx + dz * dz <= combined * combined;
    }
}
