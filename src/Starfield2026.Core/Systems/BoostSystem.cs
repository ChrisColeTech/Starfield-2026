using System;

namespace Starfield2026.Core.Systems;

public class BoostSystem
{
    public const int MaxBoosts = 5;
    public const float BoostDuration = 10f;
    
    public int BoostCount { get; private set; }
    
    public event Action<BoostSystem>? Changed;
    
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
        if (BoostCount <= 0) return false;
        
        BoostCount--;
        Changed?.Invoke(this);
        return true;
    }
    
    public void SetBoosts(int count)
    {
        BoostCount = Math.Clamp(count, 0, MaxBoosts);
        Changed?.Invoke(this);
    }
}
