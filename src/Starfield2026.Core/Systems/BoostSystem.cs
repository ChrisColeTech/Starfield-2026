using System;

namespace Starfield2026.Core.Systems;

public class BoostSystem
{
    public const int MaxBoosts = 5;
    public const float BoostDuration = 10f;

    public int BoostCount { get; private set; }
    public bool IsActive { get; private set; }
    public float ActivePercent => IsActive ? _activeTimer / BoostDuration : 0f;

    private float _activeTimer;

    public event Action<BoostSystem>? Changed;

    public void Update(float dt)
    {
        if (!IsActive) return;
        _activeTimer -= dt;
        if (_activeTimer <= 0f)
        {
            IsActive = false;
            _activeTimer = 0f;
            Changed?.Invoke(this);
        }
    }

    public void AddBoost()
    {
        if (BoostCount < MaxBoosts)
        {
            BoostCount++;
            Changed?.Invoke(this);
        }
    }

    public void AddBoost(int count)
    {
        BoostCount = Math.Clamp(BoostCount + count, 0, MaxBoosts);
        Changed?.Invoke(this);
    }

    public void UseBoost(int count = 1)
    {
        BoostCount = Math.Max(0, BoostCount - count);
        Changed?.Invoke(this);
    }

    public bool TryActivate()
    {
        return ActivateBoost();
    }

    public bool ActivateBoost()
    {
        if (BoostCount <= 0) return false;
        BoostCount--;
        IsActive = true;
        _activeTimer = BoostDuration;
        Changed?.Invoke(this);
        return true;
    }

    public void SetBoosts(int count)
    {
        BoostCount = Math.Clamp(count, 0, MaxBoosts);
        Changed?.Invoke(this);
    }
}
