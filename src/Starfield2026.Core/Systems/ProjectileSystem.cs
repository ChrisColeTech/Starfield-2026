using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.Systems;

public class ProjectileSystem
{
    private struct ProjectileInstance
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Life;
        public ProjectileType Type;
    }

    private readonly List<ProjectileInstance> _projectiles = new();
    private CubeRenderer _renderer = null!;
    private float _fireCooldown;
    private float _fireRate = 0.12f;

    public float FireRate
    {
        get => _fireRate;
        set => _fireRate = value;
    }

    public void Initialize(GraphicsDevice device)
    {
        _renderer = new CubeRenderer();
        _renderer.Initialize(device);
    }

    public void Spawn(Vector3 position, Vector3 velocity, ProjectileType type = ProjectileType.Gold, float life = 2f)
    {
        _projectiles.Add(new ProjectileInstance
        {
            Position = position,
            Velocity = velocity,
            Life = life,
            Type = type,
        });
    }

    public bool TryFire(Vector3 position, Vector3 velocity, ProjectileType type, float life = 2f)
    {
        if (_fireCooldown <= 0)
        {
            _fireCooldown = _fireRate;
            Spawn(position, velocity, type, life);
            return true;
        }
        return false;
    }

    public void Update(float dt)
    {
        _fireCooldown -= dt;

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Position += p.Velocity * dt;
            p.Life -= dt;
            _projectiles[i] = p;
            if (p.Life <= 0)
                _projectiles.RemoveAt(i);
        }
    }

    public int CheckCollisions(Vector3 targetPosition, float hitRadius)
    {
        int hits = 0;
        float radiusSq = hitRadius * hitRadius;

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            float distSq = Vector3.DistanceSquared(p.Position, targetPosition);
            if (distSq <= radiusSq)
            {
                _projectiles.RemoveAt(i);
                hits++;
            }
        }
        return hits;
    }

    public int CheckCollisionsWithDamage(Vector3 targetPosition, float hitRadius, out int totalDamage)
    {
        int hits = 0;
        totalDamage = 0;
        float radiusSq = hitRadius * hitRadius;

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            float distSq = Vector3.DistanceSquared(p.Position, targetPosition);
            if (distSq <= radiusSq)
            {
                totalDamage += AmmoConfig.GetDamageMultiplier(p.Type);
                _projectiles.RemoveAt(i);
                hits++;
            }
        }
        return hits;
    }

    public void Clear()
    {
        _projectiles.Clear();
        _fireCooldown = 0;
    }

    public int Count => _projectiles.Count;

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        foreach (var p in _projectiles)
        {
            var color = AmmoConfig.GetProjectileColor(p.Type);
            float size = p.Type == ProjectileType.Red ? 0.8f : 0.4f;
            _renderer.Draw(device, view, projection, p.Position, 0f, size, color);
        }
    }
}
