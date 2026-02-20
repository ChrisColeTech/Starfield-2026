using System;

namespace Starfield2026.Core.Systems;

/// <summary>
/// Tracks player health with max HP, damage, healing, and death detection.
/// </summary>
public class PlayerHealthSystem
{
    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;
    public float HealthPercent => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

    public event Action? OnDeath;
    public event Action<int>? OnDamaged;

    public void Initialize(int maxHealth = 100)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth = Math.Max(0, CurrentHealth - amount);
        OnDamaged?.Invoke(amount);

        if (IsDead)
            OnDeath?.Invoke();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
    }

    public void Reset()
    {
        CurrentHealth = MaxHealth;
    }
}
