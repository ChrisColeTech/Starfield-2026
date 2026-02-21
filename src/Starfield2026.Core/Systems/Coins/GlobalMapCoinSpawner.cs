using System;
using Microsoft.Xna.Framework;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Systems.Coins;

public class GlobalMapCoinSpawner : ICoinSpawner
{
    private readonly int _densityCount;
    private readonly Random _random = new();
    
    public GlobalMapCoinSpawner(int densityCount = 75)
    {
        _densityCount = densityCount;
    }

    public void Initialize(CoinCollectibleSystem system)
    {
        // No per-frame initialization needed for global spawn
    }

    public void Update(float dt, Vector3 playerPos, float playerSpeed, CoinCollectibleSystem system, MapDefinition? map = null)
    {
        // Global Map mode never despawns based on distance, and never continuously spawns on a timer.
        // It relies purely on the OnMapLoaded hook to seed the map once.
    }

    public void OnMapLoaded(MapDefinition map, CoinCollectibleSystem system)
    {
        system.ClearCoins();
        
        for (int i = 0; i < _densityCount; i++)
        {
            float x = ((float)_random.NextDouble() * 2f - 1f) * map.Width;
            float z = ((float)_random.NextDouble() * 2f - 1f) * map.Height;
            
            int nxX = (int)Math.Floor(x / 2f + map.Width / 2f);
            int nzZ = (int)Math.Floor(z / 2f + map.Height / 2f);
            
            float y = 1.6f;
            if (nxX >= 0 && nxX < map.Width && nzZ >= 0 && nzZ < map.Height)
            {
                y += map.GetTileHeight(nxX, nzZ);
            }
            
            var type = PickCoinType(0.1f, 0.2f, 0.2f);
            system.SpawnCoin(new Vector3(x, y, z), type);
        }
    }
    
    private CoinType PickCoinType(float redChance, float blueChance, float greenChance)
    {
        double roll = _random.NextDouble();
        if (roll < greenChance) return CoinType.Green;
        if (roll < greenChance + blueChance) return CoinType.Blue;
        if (roll < greenChance + blueChance + redChance) return CoinType.Red;
        return CoinType.Gold;
    }
}
