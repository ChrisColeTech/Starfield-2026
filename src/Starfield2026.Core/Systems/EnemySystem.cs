using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    public void Initialize(GraphicsDevice device)
    {
        _renderer = new CubeRenderer();
        _renderer.Initialize(device);
    }

    public void SpawnEnemies(EnemyType type, Vector3 playerPosition, int count = 2)
    {
        for (int i = 0; i < count; i++)
        {
            var enemy = CreateEnemy(type, playerPosition);
            _enemies.Add(enemy);
        }
    }

    private Enemy CreateEnemy(EnemyType type, Vector3 playerPosition)
    {
        var enemy = new Enemy
        {
            Type = type,
            Active = true,
            Behavior = _random.Next(3),
        };

        // Random spawn position ahead and to the side of player
        float xOffset = (float)(_random.NextDouble() * 2 - 1) * 40f;
        float yOffset = (float)(_random.NextDouble() * 2 - 1) * 20f;
        float zOffset = -50f - (float)_random.NextDouble() * 30f;

        enemy.Position = playerPosition + new Vector3(xOffset, yOffset, zOffset);

        // Configure based on type
        switch (type)
        {
            case EnemyType.Scout:
                enemy.Scale = 1f;
                enemy.Color = Color.Lime;
                enemy.MaxHP = enemy.HP = 5;
                enemy.Speed = 35f;
                enemy.FireRate = 1.5f;
                enemy.HitRadius = 1.5f;
                enemy.RotationSpeed = 3f;
                break;
            case EnemyType.Fighter:
                enemy.Scale = 1.5f;
                enemy.Color = Color.Cyan;
                enemy.MaxHP = enemy.HP = 10;
                enemy.Speed = 30f;
                enemy.FireRate = 1f;
                enemy.HitRadius = 2f;
                enemy.RotationSpeed = 2.5f;
                break;
            case EnemyType.Bomber:
                enemy.Scale = 2.5f;
                enemy.Color = Color.Orange;
                enemy.MaxHP = enemy.HP = 25;
                enemy.Speed = 15f;
                enemy.FireRate = 2f;
                enemy.HitRadius = 3f;
                enemy.RotationSpeed = 1f;
                break;
            case EnemyType.Interceptor:
                enemy.Scale = 1.2f;
                enemy.Color = Color.Magenta;
                enemy.MaxHP = enemy.HP = 8;
                enemy.Speed = 45f;
                enemy.FireRate = 0.8f;
                enemy.HitRadius = 1.8f;
                enemy.RotationSpeed = 4f;
                break;
            case EnemyType.Cruiser:
                enemy.Scale = 3f;
                enemy.Color = Color.Yellow;
                enemy.MaxHP = enemy.HP = 40;
                enemy.Speed = 20f;
                enemy.FireRate = 1.5f;
                enemy.HitRadius = 4f;
                enemy.RotationSpeed = 1.5f;
                break;
            case EnemyType.Destroyer:
                enemy.Scale = 3.5f;
                enemy.Color = Color.Red;
                enemy.MaxHP = enemy.HP = 50;
                enemy.Speed = 18f;
                enemy.FireRate = 1.2f;
                enemy.HitRadius = 4.5f;
                enemy.RotationSpeed = 1.2f;
                break;
            case EnemyType.Dreadnought:
                enemy.Scale = 4.5f;
                enemy.Color = Color.Purple;
                enemy.MaxHP = enemy.HP = 80;
                enemy.Speed = 12f;
                enemy.FireRate = 2f;
                enemy.HitRadius = 5.5f;
                enemy.RotationSpeed = 0.8f;
                break;
            case EnemyType.Carrier:
                enemy.Scale = 5f;
                enemy.Color = Color.Teal;
                enemy.MaxHP = enemy.HP = 100;
                enemy.Speed = 10f;
                enemy.FireRate = 2.5f;
                enemy.HitRadius = 6f;
                enemy.RotationSpeed = 0.5f;
                break;
            case EnemyType.Boss:
                enemy.Scale = 6f;
                enemy.Color = Color.Crimson;
                enemy.MaxHP = enemy.HP = 150;
                enemy.Speed = 8f;
                enemy.FireRate = 0.5f;
                enemy.HitRadius = 7f;
                enemy.RotationSpeed = 0.3f;
                break;
        }

        return enemy;
    }

    public void Update(float dt, Vector3 playerPosition)
    {
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

            // Remove if too far behind
            if (enemy.Position.Z > playerPosition.Z + 100f)
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
    }

    private void UpdateAI(Enemy enemy, float dt, Vector3 playerPosition)
    {
        Vector3 toPlayer = playerPosition - enemy.Position;
        float dist = toPlayer.Length();
        
        if (dist > 0.1f)
            toPlayer /= dist; // Normalize

        switch (enemy.Behavior)
        {
            case 0: // Pursue - move toward player
                enemy.Velocity = toPlayer * enemy.Speed;
                break;
            case 1: // Strafe - move sideways relative to player
                Vector3 strafeDir = new Vector3(-toPlayer.Z, 0, toPlayer.X);
                enemy.Velocity = strafeDir * enemy.Speed * 0.7f + toPlayer * enemy.Speed * 0.3f;
                break;
            case 2: // Orbit - circle around player
                float orbitAngle = (float)_random.NextDouble() * MathHelper.TwoPi;
                Vector3 orbitDir = new Vector3((float)Math.Cos(orbitAngle), 0, (float)Math.Sin(orbitAngle));
                enemy.Velocity = orbitDir * enemy.Speed * 0.5f + toPlayer * enemy.Speed * 0.5f;
                break;
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

    public int CheckPlayerCollisions(Vector3 playerPosition, float playerRadius)
    {
        int hits = 0;
        float radiusSq = playerRadius * playerRadius;

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

        // Also check collision with enemies themselves
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.Active) continue;
            
            float distSq = Vector3.DistanceSquared(enemy.Position, playerPosition);
            float combinedRadius = enemy.HitRadius + playerRadius;
            if (distSq <= combinedRadius * combinedRadius)
            {
                enemy.HP -= 10; // Collision damage
                if (enemy.HP <= 0)
                {
                    enemy.Active = false;
                    _enemies.RemoveAt(i);
                }
                hits++;
            }
        }

        return hits;
    }

    public int CheckProjectileHits(List<(Vector3 Pos, bool Hit)> projectilePositions, float projectileRadius)
    {
        int totalHits = 0;

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.Active) continue;

            for (int j = projectilePositions.Count - 1; j >= 0; j--)
            {
                float distSq = Vector3.DistanceSquared(projectilePositions[j].Pos, enemy.Position);
                float combinedRadius = enemy.HitRadius + projectileRadius;
                if (distSq <= combinedRadius * combinedRadius)
                {
                    enemy.HP--;
                    projectilePositions[j] = (projectilePositions[j].Pos, true);
                    totalHits++;

                    if (enemy.HP <= 0)
                    {
                        enemy.Active = false;
                        _enemies.RemoveAt(i);
                    }
                    break;
                }
            }
        }

        return totalHits;
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
