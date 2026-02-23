using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Maps;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.Systems;

public enum EnemyType
{
    Scout = 1,
    Fighter = 2,
    Bomber = 3,
    Interceptor = 4,
    Cruiser = 5,
    Destroyer = 6,
    Dreadnought = 7,
    Carrier = 8,
    Boss = 9,
}

public enum EnemySpawnMode
{
    /// <summary>Enemies spawn in a ring ahead of the player's forward direction.</summary>
    Ahead,
    /// <summary>Enemies spawn in a ring around the player and chase.</summary>
    Ring,
    /// <summary>No auto-spawning — use SpawnAtPosition for tile-based placement.</summary>
    Manual,
}

public class EnemySystem
{
    private class Enemy
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public EnemyType Type;
        public int HP;
        public int MaxHP;
        public float Scale;
        public Color Color;
        public float Rotation;
        public float RotationSpeed;
        public float FireRate;
        public float FireCooldown;
        public float Speed;
        public float HitRadius;
        public bool Active;
        public int Behavior; // 0 = pursue, 1 = strafe, 2 = orbit
    }

    private readonly List<Enemy> _enemies = new();
    private readonly List<ProjectileInstance> _enemyProjectiles = new();
    private readonly Random _random = new();
    private CubeRenderer _renderer = null!;

    private class ProjectileInstance
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Life;
    }

    // Auto-spawn configuration
    public float SpawnInterval { get; set; } = 8f;
    public float SpawnRadius { get; set; } = 120f;
    public int MaxActive { get; set; } = 5;
    public float PlayerHitRadius { get; set; } = 3f;
    public EnemySpawnMode SpawnMode { get; set; } = EnemySpawnMode.Ring;
    public Vector3 PlayerForward { get; set; } = -Vector3.UnitZ;
    public EnemyType[] AllowedTypes { get; set; } = { EnemyType.Scout, EnemyType.Fighter, EnemyType.Bomber, EnemyType.Interceptor };

    /// <summary>Multiplier applied to enemy movement speed. Set higher for fast screens (e.g. space flight).</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>When set, enemies match at least this speed so they don't fall behind the player.</summary>
    public float MinimumSpeed { get; set; } = 0f;

    private float _spawnTimer;
    private float _collisionImmunity;

    /// <summary>Optional coin system — if set, enemies burst coins on death.</summary>
    public CoinCollectibleSystem? CoinSystem { get; set; }

    // Events
    public event Action<Vector3, EnemyType>? OnEnemyDefeated;
    public event Action<Vector3, EnemyType>? OnPlayerCollision;

    public int ActiveCount => _enemies.Count;

    /// <summary>Returns world positions of all active enemies for minimap rendering.</summary>
    public List<Vector3> GetPositions()
    {
        var positions = new List<Vector3>(_enemies.Count);
        foreach (var e in _enemies)
            if (e.Active) positions.Add(e.Position);
        return positions;
    }

    public void Initialize(GraphicsDevice device)
    {
        _renderer = new CubeRenderer();
        _renderer.Initialize(device);
        _spawnTimer = SpawnInterval;
    }

    /// <summary>
    /// Spawn an enemy at an exact world position (for tile-based spawning).
    /// </summary>
    public void SpawnAtPosition(EnemyType type, Vector3 position)
    {
        var enemy = CreateEnemyAtPosition(type, position);
        _enemies.Add(enemy);
    }

    public void SpawnEnemies(EnemyType type, Vector3 playerPosition, int count = 2)
    {
        for (int i = 0; i < count; i++)
        {
            var enemy = CreateEnemy(type, playerPosition);
            _enemies.Add(enemy);
        }
    }

    private Enemy CreateEnemyAtPosition(EnemyType type, Vector3 position)
    {
        var enemy = new Enemy
        {
            Type = type,
            Active = true,
            Behavior = _random.Next(3),
            Position = position,
        };
        ApplyTypeStats(enemy, type);
        return enemy;
    }

    private Enemy CreateEnemy(EnemyType type, Vector3 playerPosition)
    {
        var enemy = new Enemy
        {
            Type = type,
            Active = true,
            Behavior = _random.Next(3),
        };

        float dist = SpawnRadius * 0.8f + (float)(_random.NextDouble() * SpawnRadius * 0.2f);

        if (SpawnMode == EnemySpawnMode.Ahead)
        {
            // Spawn ahead of the player's travel direction with some lateral spread
            float spread = (float)(_random.NextDouble() * 2 - 1) * SpawnRadius * 0.5f;
            float ySpread = (float)(_random.NextDouble() * 2 - 1) * 10f;
            Vector3 forward = PlayerForward;
            if (forward.LengthSquared() < 0.01f) forward = -Vector3.UnitZ;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.Up, forward);
            if (right.LengthSquared() < 0.01f) right = Vector3.UnitX;
            right.Normalize();
            enemy.Position = playerPosition + forward * dist + right * spread + Vector3.Up * ySpread;
        }
        else // Ring
        {
            float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
            float xOffset = (float)Math.Cos(angle) * dist;
            float zOffset = (float)Math.Sin(angle) * dist;
            float yOffset = (float)(_random.NextDouble() * 2 - 1) * 5f;
            enemy.Position = playerPosition + new Vector3(xOffset, yOffset, zOffset);
        }

        ApplyTypeStats(enemy, type);
        return enemy;
    }

    private void ApplyTypeStats(Enemy enemy, EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Scout:
                enemy.Scale = 2.0f;
                enemy.Color = Color.Lime;
                enemy.MaxHP = enemy.HP = 5;
                enemy.Speed = 4f;
                enemy.FireRate = 1.5f;
                enemy.HitRadius = 2.5f;
                enemy.RotationSpeed = 3f;
                break;
            case EnemyType.Fighter:
                enemy.Scale = 2.5f;
                enemy.Color = Color.Cyan;
                enemy.MaxHP = enemy.HP = 10;
                enemy.Speed = 3.5f;
                enemy.FireRate = 1f;
                enemy.HitRadius = 3f;
                enemy.RotationSpeed = 2.5f;
                break;
            case EnemyType.Bomber:
                enemy.Scale = 3.5f;
                enemy.Color = Color.Orange;
                enemy.MaxHP = enemy.HP = 25;
                enemy.Speed = 2f;
                enemy.FireRate = 2f;
                enemy.HitRadius = 4f;
                enemy.RotationSpeed = 1f;
                break;
            case EnemyType.Interceptor:
                enemy.Scale = 2.2f;
                enemy.Color = Color.Magenta;
                enemy.MaxHP = enemy.HP = 8;
                enemy.Speed = 5f;
                enemy.FireRate = 0.8f;
                enemy.HitRadius = 2.8f;
                enemy.RotationSpeed = 4f;
                break;
            case EnemyType.Cruiser:
                enemy.Scale = 4.0f;
                enemy.Color = Color.Yellow;
                enemy.MaxHP = enemy.HP = 40;
                enemy.Speed = 2.5f;
                enemy.FireRate = 1.5f;
                enemy.HitRadius = 5f;
                enemy.RotationSpeed = 1.5f;
                break;
            case EnemyType.Destroyer:
                enemy.Scale = 4.5f;
                enemy.Color = Color.Red;
                enemy.MaxHP = enemy.HP = 50;
                enemy.Speed = 2f;
                enemy.FireRate = 1.2f;
                enemy.HitRadius = 5.5f;
                enemy.RotationSpeed = 1.2f;
                break;
            case EnemyType.Dreadnought:
                enemy.Scale = 5.5f;
                enemy.Color = Color.Purple;
                enemy.MaxHP = enemy.HP = 80;
                enemy.Speed = 1.5f;
                enemy.FireRate = 2f;
                enemy.HitRadius = 6.5f;
                enemy.RotationSpeed = 0.8f;
                break;
            case EnemyType.Carrier:
                enemy.Scale = 6.0f;
                enemy.Color = Color.Teal;
                enemy.MaxHP = enemy.HP = 100;
                enemy.Speed = 1f;
                enemy.FireRate = 2.5f;
                enemy.HitRadius = 7f;
                enemy.RotationSpeed = 0.5f;
                break;
            case EnemyType.Boss:
                enemy.Scale = 7.0f;
                enemy.Color = Color.Crimson;
                enemy.MaxHP = enemy.HP = 150;
                enemy.Speed = 1f;
                enemy.FireRate = 0.5f;
                enemy.HitRadius = 8f;
                enemy.RotationSpeed = 0.3f;
                break;
        }
    }

    private void TrySpawnFromTile(MapDefinition map, Vector3 playerPosition)
    {
        const float scale = 2f;
        int px = (int)(playerPosition.X / scale + map.Width / 2f);
        int pz = (int)(playerPosition.Z / scale + map.Height / 2f);

        // Scan nearby tiles for EnemySpawn (117) or BossSpawn (118)
        int scanRadius = 15;
        var candidates = new List<(int x, int z, bool isBoss)>();
        for (int dz = -scanRadius; dz <= scanRadius; dz++)
        {
            for (int dx = -scanRadius; dx <= scanRadius; dx++)
            {
                int tx = px + dx;
                int tz = pz + dz;
                if (tx < 0 || tx >= map.Width || tz < 0 || tz >= map.Height) continue;

                int tileId = map.GetBaseTile(tx, tz);
                if (tileId == 117) candidates.Add((tx, tz, false));
                else if (tileId == 118) candidates.Add((tx, tz, true));

                int? overlay = map.GetOverlayTile(tx, tz);
                if (overlay == 117) candidates.Add((tx, tz, false));
                else if (overlay == 118) candidates.Add((tx, tz, true));
            }
        }

        if (candidates.Count == 0) return;

        var (tileX, tileZ, boss) = candidates[_random.Next(candidates.Count)];
        float worldX = (tileX - map.Width / 2f) * scale;
        float worldZ = (tileZ - map.Height / 2f) * scale;

        EnemyType type;
        if (boss)
            type = EnemyType.Boss;
        else
            type = AllowedTypes[_random.Next(AllowedTypes.Length)];

        SpawnAtPosition(type, new Vector3(worldX, 0.825f, worldZ));
    }

    /// <summary>
    /// Full update: auto-spawns enemies, runs AI, checks projectile hits, checks player collision.
    /// Pass a map to use tile-based spawning (EnemySpawn/BossSpawn tiles).
    /// </summary>
    public void Update(float dt, Vector3 playerPosition, ProjectileSystem? projectiles = null, MapDefinition? map = null)
    {
        if (_collisionImmunity > 0) _collisionImmunity -= dt;

        _spawnTimer -= dt;
        if (_spawnTimer <= 0 && _enemies.Count < MaxActive && AllowedTypes.Length > 0)
        {
            _spawnTimer = SpawnInterval;

            if (map != null)
            {
                // Tile-based: find a spawn tile near the player
                TrySpawnFromTile(map, playerPosition);
            }
            else if (SpawnMode != EnemySpawnMode.Manual)
            {
                // Procedural: ring or ahead
                var type = AllowedTypes[_random.Next(AllowedTypes.Length)];
                SpawnEnemies(type, playerPosition, 1);
            }
        }

        // Update enemies
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.Active) continue;

            // AI behavior
            UpdateAI(enemy, dt, playerPosition);

            // Rotation
            enemy.Rotation += enemy.RotationSpeed * dt;

            // Firing
            enemy.FireCooldown -= dt;
            if (enemy.FireCooldown <= 0)
            {
                enemy.FireCooldown = enemy.FireRate;
                FireAtPlayer(enemy, playerPosition);
            }

            // Remove if too far from player
            float distToPlayer = Vector3.Distance(enemy.Position, playerPosition);
            if (distToPlayer > SpawnRadius * 3f)
            {
                _enemies.RemoveAt(i);
            }
        }

        // Update enemy projectiles
        for (int i = _enemyProjectiles.Count - 1; i >= 0; i--)
        {
            var proj = _enemyProjectiles[i];
            proj.Position += proj.Velocity * dt;
            proj.Life -= dt;
            _enemyProjectiles[i] = proj;
            if (proj.Life <= 0)
                _enemyProjectiles.RemoveAt(i);
        }

        // Check player projectile hits on enemies
        if (projectiles != null)
            CheckProjectileHitsInternal(projectiles);

        // Check player body collision with enemies
        CheckPlayerBodyCollision(playerPosition);
    }

    private void UpdateAI(Enemy enemy, float dt, Vector3 playerPosition)
    {
        Vector3 toPlayer = playerPosition - enemy.Position;
        float dist = toPlayer.Length();

        if (dist > 0.1f)
            toPlayer /= dist; // Normalize

        float speed = Math.Max(enemy.Speed * SpeedMultiplier, MinimumSpeed);

        if (SpawnMode == EnemySpawnMode.Ahead)
        {
            // Ahead mode: fly in front of the player at a set distance,
            // occasionally close in to try to make contact, then pull back out.

            float time = enemy.Rotation * 2f;

            Vector3 forward = PlayerForward;
            if (forward.LengthSquared() < 0.01f) forward = -Vector3.UnitZ;
            forward.Normalize();

            // How far ahead the enemy currently is
            float dotAhead = Vector3.Dot(enemy.Position - playerPosition, forward);

            // Approach cycle: ~6 second period, offset per enemy
            float cycle = (float)Math.Sin(time * 0.3f + enemy.Behavior * 2.1f);

            // Target distance: normally 30 ahead, but during attack dips to 0 (player contact)
            float holdDist = 30f;
            float targetDist = cycle > 0.6f ? 0f : holdDist;

            // Steer toward target distance — always flying forward
            float correction = (targetDist - dotAhead) * 2f;
            enemy.Velocity = forward * (speed + correction);
        }
        else
        {
            switch (enemy.Behavior)
            {
                case 0: // Pursue - move toward player
                    enemy.Velocity = toPlayer * speed;
                    break;
                case 1: // Strafe - move sideways relative to player
                    Vector3 strafeDir = new Vector3(-toPlayer.Z, 0, toPlayer.X);
                    enemy.Velocity = strafeDir * speed * 0.7f + toPlayer * speed * 0.3f;
                    break;
                case 2: // Orbit - circle around player
                    float orbitAngle = (float)_random.NextDouble() * MathHelper.TwoPi;
                    Vector3 orbitDir = new Vector3((float)Math.Cos(orbitAngle), 0, (float)Math.Sin(orbitAngle));
                    enemy.Velocity = orbitDir * speed * 0.5f + toPlayer * speed * 0.5f;
                    break;
            }
        }

        enemy.Position += enemy.Velocity * dt;
    }

    private void FireAtPlayer(Enemy enemy, Vector3 playerPosition)
    {
        Vector3 toPlayer = playerPosition - enemy.Position;
        if (toPlayer.LengthSquared() > 0)
            toPlayer.Normalize();

        _enemyProjectiles.Add(new ProjectileInstance
        {
            Position = enemy.Position,
            Velocity = toPlayer * 80f,
            Life = 4f,
        });
    }

    /// <summary>
    /// Check enemy projectiles hitting the player. Returns number of hits.
    /// </summary>
    public int CheckPlayerProjectileHits(Vector3 playerPosition)
    {
        int hits = 0;
        float radiusSq = PlayerHitRadius * PlayerHitRadius;

        for (int i = _enemyProjectiles.Count - 1; i >= 0; i--)
        {
            var proj = _enemyProjectiles[i];
            float distSq = Vector3.DistanceSquared(proj.Position, playerPosition);
            if (distSq <= radiusSq)
            {
                _enemyProjectiles.RemoveAt(i);
                hits++;
            }
        }

        return hits;
    }

    private int _collidedEnemyIndex = -1;

    private void CheckPlayerBodyCollision(Vector3 playerPosition)
    {
        if (_collidedEnemyIndex >= 0) return; // Already in a collision/battle
        if (_collisionImmunity > 0) return; // Immune after running from battle

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.Active) continue;

            float distSq = Vector3.DistanceSquared(enemy.Position, playerPosition);
            float combinedRadius = enemy.HitRadius + PlayerHitRadius;
            if (distSq <= combinedRadius * combinedRadius)
            {
                _collidedEnemyIndex = i;
                enemy.Active = false; // Freeze during battle
                OnPlayerCollision?.Invoke(enemy.Position, enemy.Type);
                return;
            }
        }
    }

    /// <summary>Call after battle victory — removes the enemy that triggered the battle and bursts coins.</summary>
    public void DefeatCollidedEnemy()
    {
        if (_collidedEnemyIndex >= 0 && _collidedEnemyIndex < _enemies.Count)
        {
            var enemy = _enemies[_collidedEnemyIndex];
            var pos = enemy.Position;
            var type = enemy.Type;
            _enemies.RemoveAt(_collidedEnemyIndex);
            int coins = type >= EnemyType.Cruiser ? 8 : type >= EnemyType.Bomber ? 5 : 3;
            CoinSystem?.SpawnBurst(pos, coins);
            OnEnemyDefeated?.Invoke(pos, type);
        }
        _collidedEnemyIndex = -1;
    }

    /// <summary>Call after running from battle — re-activates the enemy and pushes it away.</summary>
    public void ReleaseCollidedEnemy(Vector3 playerPosition)
    {
        if (_collidedEnemyIndex >= 0 && _collidedEnemyIndex < _enemies.Count)
        {
            var enemy = _enemies[_collidedEnemyIndex];
            // Push the enemy away so it doesn't immediately re-trigger
            Vector3 away = enemy.Position - playerPosition;
            if (away.LengthSquared() < 0.01f)
                away = Vector3.UnitX; // fallback direction
            away.Normalize();
            enemy.Position = playerPosition + away * (enemy.HitRadius + PlayerHitRadius + 20f);
            enemy.Active = true;
        }
        _collidedEnemyIndex = -1;
        _collisionImmunity = 3f; // 3 seconds grace period
    }

    private void CheckProjectileHitsInternal(ProjectileSystem projectiles)
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.Active) continue;

            int hits = projectiles.CheckCollisionsWithDamage(enemy.Position, enemy.HitRadius, out int damage);
            if (hits > 0)
            {
                enemy.HP -= damage;
                if (enemy.HP <= 0)
                {
                    var pos = enemy.Position;
                    var type = enemy.Type;
                    enemy.Active = false;
                    _enemies.RemoveAt(i);
                    int coins = type >= EnemyType.Cruiser ? 8 : type >= EnemyType.Bomber ? 5 : 3;
                    CoinSystem?.SpawnBurst(pos, coins);
                    OnEnemyDefeated?.Invoke(pos, type);
                }
            }
        }
    }

    public void Clear()
    {
        _enemies.Clear();
        _enemyProjectiles.Clear();
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        // Draw enemies
        foreach (var enemy in _enemies)
        {
            if (!enemy.Active) continue;

            // Flash white when damaged (HP < 80% of max)
            var color = enemy.HP < enemy.MaxHP * 0.8f && (DateTime.Now.Millisecond % 200 < 100)
                ? Color.White
                : enemy.Color;

            _renderer.Draw(device, view, projection, enemy.Position, enemy.Rotation, enemy.Scale, color);
        }

        // Draw enemy projectiles (red)
        foreach (var proj in _enemyProjectiles)
        {
            _renderer.Draw(device, view, projection, proj.Position, 0f, 0.3f, Color.Red);
        }
    }
}
