using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.Systems;

public class CoinCollectibleSystem
{
    private struct CoinInstance
    {
        public Vector3 Position;
        public float Rotation;
        public bool Collected;
        public CoinType Type;
    }

    private CoinRenderer _renderer = null!;
    private readonly List<CoinInstance> _coins = new();
    private readonly Random _random = new();

    private float _driftSpeed;
    public float DriftSpeed
    {
        get => _driftSpeed;
        set => _driftSpeed = value;
    }

    private float _spawnTimer;
    private float _spawnInterval = 1.5f;

    public float SpawnInterval
    {
        get => _spawnInterval;
        set => _spawnInterval = value;
    }

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

    public void Initialize(GraphicsDevice device)
    {
        _renderer = new CoinRenderer();
        _renderer.Initialize(device);
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

    public void SpawnRandomAhead(Vector3 playerPos, float aheadDistance, float corridorWidth, float corridorHeight, float maxBound = float.MaxValue, Starfield2026.Core.Maps.MapDefinition? map = null, float redChance = 0.1f, float blueChance = 0.2f, float greenChance = 0.2f)
    {
        float x = playerPos.X + ((float)_random.NextDouble() * 2f - 1f) * corridorWidth;
        float y = Math.Max(1.5f, playerPos.Y + ((float)_random.NextDouble() * 2f - 1f) * corridorHeight);
        float z = playerPos.Z - aheadDistance;

        if (map != null)
        {
            x = MathHelper.Clamp(x, -map.Width, map.Width);
            z = MathHelper.Clamp(z, -map.Height, map.Height);
            y = MathHelper.Clamp(y, 1f, 25f);
        }

        x = MathHelper.Clamp(x, -maxBound, maxBound);
        z = MathHelper.Clamp(z, -maxBound, maxBound);

        var type = PickCoinType(redChance, blueChance, greenChance);
        SpawnCoin(new Vector3(x, y, z), type);
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

public void Update(float dt, Vector3 playerPos, float collectRadius, float speed = 0f,
float aheadDistance = 60f, float corridorWidth = 20f, float corridorHeight = 5f, float maxBound = float.MaxValue, Starfield2026.Core.Maps.MapDefinition? map = null)
    {
        if (speed > 2f)
        {
            _spawnTimer += dt;
            if (_spawnTimer >= _spawnInterval)
            {
                _spawnTimer = 0;
                SpawnRandomAhead(playerPos, aheadDistance, corridorWidth, corridorHeight, maxBound);
            }
        }

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

        _coins.RemoveAll(c =>
            c.Collected ||
            (_driftSpeed > 0 && c.Position.Z > playerPos.Z + 20f));
    }

    public void ResetSpawnTimer()
    {
        _spawnTimer = 0;
    }

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
