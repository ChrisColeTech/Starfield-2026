using System;
using Microsoft.Xna.Framework;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Systems.Coins;

public class InfiniteRunnerCoinSpawner : ICoinSpawner
{
    private readonly Random _random = new();
    
    private float _spawnTimer;
    public float SpawnInterval { get; set; } = 2.5f;
    public float AheadDistance { get; set; } = 60f;
    public float CorridorWidth { get; set; } = 20f;
    public float CorridorHeight { get; set; } = 5f;
    public float MaxBound { get; set; } = float.MaxValue;
    
    // Space and Driving modes might want different drop rates
    public float RedChance { get; set; } = 0.1f;
    public float BlueChance { get; set; } = 0.2f;
    public float GreenChance { get; set; } = 0.2f;

    public void Initialize(CoinCollectibleSystem system)
    {
        _spawnTimer = 0f;
    }

    public void OnMapLoaded(MapDefinition map, CoinCollectibleSystem system)
    {
        // Infinite Runners do not pre-populate the world map, they rely strictly on the Update loop over time.
    }

    public void Update(float dt, Vector3 playerPos, float playerSpeed, CoinCollectibleSystem system, MapDefinition? map = null)
    {
        if (Math.Abs(playerSpeed) > 2f)
        {
            _spawnTimer += dt;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0;
                SpawnRandomAhead(playerPos, system, map);
            }
        }
        
        // Critically, Infinite Runners must despawn coins that fall far behind the player's camera
        // so that memory usage remains stable during endless play
        system.Coins.RemoveAll(c => c.Collected || (system.DriftSpeed > 0 && c.Position.Z > playerPos.Z + 20f));
    }
    
    private void SpawnRandomAhead(Vector3 playerPos, CoinCollectibleSystem system, MapDefinition? map)
    {
        float x = playerPos.X + ((float)_random.NextDouble() * 2f - 1f) * CorridorWidth;
        float y = Math.Max(1.5f, playerPos.Y + ((float)_random.NextDouble() * 2f - 1f) * CorridorHeight);
        float z = playerPos.Z - AheadDistance;

        if (map != null)
        {
            x = MathHelper.Clamp(x, -map.Width, map.Width);
            z = MathHelper.Clamp(z, -map.Height, map.Height);
            y = MathHelper.Clamp(y, 1f, 25f);
        }

        x = MathHelper.Clamp(x, -MaxBound, MaxBound);
        z = MathHelper.Clamp(z, -MaxBound, MaxBound);

        var type = PickCoinType(RedChance, BlueChance, GreenChance);
        system.SpawnCoin(new Vector3(x, y, z), type);
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
