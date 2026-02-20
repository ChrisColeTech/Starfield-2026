using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.Systems;

public class BossSystem
{
    public Vector3 Position;
    public float Rotation;
    public float Scale = 4f;
    public float HitRadius = 6f;
    public int HP = 20;
    public int Phase;
    public bool Active;

    private float _moveTimer;
    private float _flashTimer;
    private CubeRenderer _renderer = null!;

    public void Initialize(GraphicsDevice device)
    {
        _renderer = new CubeRenderer();
        _renderer.Initialize(device);
    }

    public void Spawn(Vector3 playerPosition, int hp = 20)
    {
        Active = true;
        HP = hp;
        Position = new Vector3(playerPosition.X, playerPosition.Y + 5f, playerPosition.Z - 60f);
        Scale = 4f;
        HitRadius = 6f;
        Phase = 0;
        _moveTimer = 0;
        _flashTimer = 0;
    }

    public void Update(float dt)
    {
        if (!Active) return;

        _moveTimer += dt;
        Rotation += dt * 0.5f;

        if (_flashTimer > 0)
            _flashTimer -= dt;
    }

    public void TakeDamage(int damage)
    {
        HP -= damage;
        _flashTimer = 0.15f; // Flash for 150ms
        if (HP <= 0)
        {
            Active = false;
        }
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        if (!Active) return;

        if (_flashTimer > 0)
        {
            // Flash red when hit
            _renderer.Draw(device, view, projection, Position, Rotation, Scale, new Color(255, 30, 30));
        }
        else
        {
            // Normal appearance — per-face colored cube
            _renderer.Draw(device, view, projection, Position, Rotation, Scale);
        }
    }
}
