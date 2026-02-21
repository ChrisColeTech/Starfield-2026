using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;

using Starfield2026.Core.Systems.Coins;

namespace Starfield2026.Core.Systems;

public class CoinCollectibleSystem
{
    public struct CoinInstance
    {
        public Vector3 Position;
        public float Rotation;
        public bool Collected;
        public CoinType Type;
    }

    private CoinRenderer _renderer = null!;
    private readonly List<CoinInstance> _coins = new();
    public List<CoinInstance> Coins => _coins;
    
    private readonly Random _random = new();
    private ICoinSpawner _spawnerStrategy = null!;

    private float _driftSpeed;
    public float DriftSpeed
    {
        get => _driftSpeed;
        set => _driftSpeed = value;
    }

    // Timers moved to InfiniteRunnerCoinSpawner

    public int ActiveCount
    {
        get
        {
            int count = 0;
            foreach (var c in _coins)
                if (!c.Collected) count++;
            return count;
        }
    }

    private int _newlyGoldCollected;
    private int _newlyRedCollected;
    private int _newlyBlueCollected;
    private int _newlyGreenCollected;

    public void Initialize(GraphicsDevice device, ICoinSpawner spawnerStrategy)
    {
        _renderer = new CoinRenderer();
        _renderer.Initialize(device);
        _spawnerStrategy = spawnerStrategy;
        _spawnerStrategy.Initialize(this);
    }

    public void SpawnCoin(Vector3 position, CoinType type = CoinType.Gold)
    {
        _coins.Add(new CoinInstance
        {
            Position = position,
            Rotation = (float)(_random.NextDouble() * MathHelper.TwoPi),
            Collected = false,
            Type = type,
        });
    }

    public void SpawnRandomNearby(Vector3 playerPos, float radius, float groundY, float redChance = 0.1f, float blueChance = 0.2f, float greenChance = 0.2f)
    {
        float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
        float dist = (float)(_random.NextDouble() * radius);
        float x = playerPos.X + (float)Math.Cos(angle) * dist;
        float z = playerPos.Z + (float)Math.Sin(angle) * dist;

        var type = PickCoinType(redChance, blueChance, greenChance);
        SpawnCoin(new Vector3(x, groundY + 1f, z), type);
    }

    private CoinType PickCoinType(float redChance, float blueChance, float greenChance)
    {
        double roll = _random.NextDouble();
        if (roll < greenChance) return CoinType.Green;
        if (roll < greenChance + blueChance) return CoinType.Blue;
        if (roll < greenChance + blueChance + redChance) return CoinType.Red;
        return CoinType.Gold;
    }
    public void Update(float dt, Vector3 playerPos, float collectRadius, float playerSpeed, Starfield2026.Core.Maps.MapDefinition? map = null)
    {
        UpdateCollectionLogic(dt, playerPos, collectRadius);
        _spawnerStrategy.Update(dt, playerPos, playerSpeed, this, map);
    }
    
    public void OnMapLoaded(Starfield2026.Core.Maps.MapDefinition map)
    {
        _spawnerStrategy.OnMapLoaded(map, this);
    }

    private void UpdateCollectionLogic(float dt, Vector3 playerPos, float collectRadius)
    {
        float radiusSq = collectRadius * collectRadius;

        for (int i = 0; i < _coins.Count; i++)
        {
            var coin = _coins[i];
            if (coin.Collected) continue;

            coin.Rotation += 3f * dt;

            if (_driftSpeed > 0)
                coin.Position.Z += _driftSpeed * dt;

            _coins[i] = coin;

            float distSq = Vector3.DistanceSquared(coin.Position, playerPos);
            if (distSq <= radiusSq)
            {
                coin.Collected = true;
                _coins[i] = coin;

            if (coin.Type == CoinType.Gold)
                    _newlyGoldCollected++;
                else if (coin.Type == CoinType.Red)
                    _newlyRedCollected++;
                else if (coin.Type == CoinType.Blue)
                    _newlyBlueCollected++;
                else if (coin.Type == CoinType.Green)
                    _newlyGreenCollected++;
            }
        }
    }

    // Removed ResetSpawnTimer

    public (int Gold, int Red, int Blue, int Green) GetAndResetNewlyCollected()
    {
        var result = (_newlyGoldCollected, _newlyRedCollected, _newlyBlueCollected, _newlyGreenCollected);
        _newlyGoldCollected = 0;
        _newlyRedCollected = 0;
        _newlyBlueCollected = 0;
        _newlyGreenCollected = 0;
        return result;
    }

    public void ClearCoins()
    {
        _coins.Clear();
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        foreach (var coin in _coins)
        {
            if (coin.Collected) continue;
            var color = AmmoConfig.GetCoinColor(coin.Type);
            _renderer.Draw(device, view, projection, coin.Position, coin.Rotation, 2.5f, color);
        }
    }
}
