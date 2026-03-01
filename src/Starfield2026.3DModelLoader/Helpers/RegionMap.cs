#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Helpers;

public enum RegionType
{
    Grass,
    Dirt,
    Rock,
    Water,
}

public static class RegionTextures
{
    public static string GetTextureName(RegionType type) => type switch
    {
        RegionType.Grass => "Terrain Grass.png",
        RegionType.Dirt => "Terrain Dirt.png",
        RegionType.Rock => "Terrain Rock.png",
        RegionType.Water => "Water.png",
        _ => "Terrain Grass.png",
    };

    public static Color GetDebugColor(RegionType type) => type switch
    {
        RegionType.Grass => new Color(80, 160, 60),
        RegionType.Dirt => new Color(160, 130, 80),
        RegionType.Rock => new Color(130, 130, 130),
        RegionType.Water => new Color(60, 120, 200),
        _ => Color.Magenta,
    };
}

public class RegionCell
{
    public RegionType Type { get; set; } = RegionType.Grass;
}

public class RegionMap
{
    public int Width { get; }
    public int Depth { get; }
    public float CellSize { get; }
    public Vector3 Origin { get; }

    private readonly RegionCell[,] _cells;

    public RegionMap(int width, int depth, float cellSize = 2f)
    {
        Width = width;
        Depth = depth;
        CellSize = cellSize;
        Origin = new Vector3(-width * cellSize * 0.5f, 0, -depth * cellSize * 0.5f);
        _cells = new RegionCell[width, depth];

        for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
                _cells[x, z] = new RegionCell();
    }

    public RegionCell this[int x, int z]
    {
        get => _cells[x, z];
        set => _cells[x, z] = value;
    }

    public bool InBounds(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Depth;

    public (int x, int z) WorldToCell(Vector3 worldPos)
    {
        float localX = worldPos.X - Origin.X;
        float localZ = worldPos.Z - Origin.Z;
        int cellX = (int)MathF.Floor(localX / CellSize);
        int cellZ = (int)MathF.Floor(localZ / CellSize);
        return (cellX, cellZ);
    }

    public Vector3 CellToWorld(int x, int z)
    {
        return new Vector3(
            Origin.X + (x + 0.5f) * CellSize,
            0,
            Origin.Z + (z + 0.5f) * CellSize);
    }

    public RegionType GetRegionAt(Vector3 worldPos)
    {
        var (x, z) = WorldToCell(worldPos);
        if (InBounds(x, z))
            return _cells[x, z].Type;
        return RegionType.Grass;
    }

    public void GenerateProcedural(int seed = 12345)
    {
        var rng = new Random(seed);

        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Depth; z++)
            {
                float nx = x * 0.08f;
                float nz = z * 0.08f;

                float n = Noise2D(nx, nz, seed);
                float n2 = Noise2D(nx * 2f, nz * 2f, seed + 1000) * 0.5f;
                float value = (n + n2) * 0.5f;

                RegionType type = value switch
                {
                    < 0.25f => RegionType.Water,
                    < 0.55f => RegionType.Grass,
                    < 0.75f => RegionType.Dirt,
                    _ => RegionType.Rock,
                };

                _cells[x, z].Type = type;
            }
        }
    }

    private static float Noise2D(float x, float y, int seed)
    {
        int ix = (int)MathF.Floor(x);
        int iy = (int)MathF.Floor(y);
        float fx = x - ix;
        float fy = y - iy;

        float a = PseudoRandom(ix, iy, seed);
        float b = PseudoRandom(ix + 1, iy, seed);
        float c = PseudoRandom(ix, iy + 1, seed);
        float d = PseudoRandom(ix + 1, iy + 1, seed);

        float ux = Smoothstep(fx);
        float uy = Smoothstep(fy);

        return Lerp(Lerp(a, b, ux), Lerp(c, d, ux), uy);
    }

    private static float PseudoRandom(int x, int y, int seed)
    {
        int n = x + y * 57427 + seed * 15485863;
        n = (n << 13) ^ n;
        return (n * (n * n * 15731 + 789221) + 1376312589) / 2147483648f;
    }

    private static float Smoothstep(float t) => t * t * (3 - 2 * t);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
