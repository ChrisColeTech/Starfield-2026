using System;
using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Systems;

public enum ProjectileType
{
    Gold,
    Red,
}

public enum CoinType
{
    Gold,
    Red,
    Blue,
    Green,
}

public static class AmmoConfig
{
    public const int GoldCoinAmmo = 10;
    public const int RedCoinAmmo = 10;
    public const int RedDamageMultiplier = 2;
    
    public static readonly Color GoldColor = Color.Gold;
    public static readonly Color RedColor = Color.Red;
    public static readonly Color BlueColor = Color.DodgerBlue;
    public static readonly Color GreenColor = Color.LimeGreen;
    
    public static Color GetProjectileColor(ProjectileType type) => type switch
    {
        ProjectileType.Gold => GoldColor,
        ProjectileType.Red => RedColor,
        _ => GoldColor,
    };
    
    public static int GetDamageMultiplier(ProjectileType type) => type switch
    {
        ProjectileType.Gold => 1,
        ProjectileType.Red => RedDamageMultiplier,
        _ => 1,
    };
    
    public static Color GetCoinColor(CoinType type) => type switch
    {
        CoinType.Gold => GoldColor,
        CoinType.Red => RedColor,
        CoinType.Blue => BlueColor,
        CoinType.Green => GreenColor,
        _ => GoldColor,
    };
    
    public static int GetAmmoFromCoin(CoinType type) => type switch
    {
        CoinType.Gold => GoldCoinAmmo,
        CoinType.Red => RedCoinAmmo,
        _ => 0,
    };
    
    public static ProjectileType GetProjectileTypeFromCoin(CoinType type) => type switch
    {
        CoinType.Gold => ProjectileType.Gold,
        CoinType.Red => ProjectileType.Red,
        _ => ProjectileType.Gold,
    };
}

public class AmmoSystem
{
    public int GoldAmmo { get; private set; }
    public int RedAmmo { get; private set; }
    public ProjectileType SelectedType { get; private set; }
    
    public event Action<AmmoSystem>? Changed;
    
    public void Initialize(int goldAmmo = 0, int redAmmo = 0)
    {
        GoldAmmo = goldAmmo;
        RedAmmo = redAmmo;
        SelectedType = ProjectileType.Gold;
    }
    
    public void ToggleProjectileType()
    {
        SelectedType = SelectedType == ProjectileType.Gold ? ProjectileType.Red : ProjectileType.Gold;
        Changed?.Invoke(this);
    }
    
    public void AddAmmoFromCoin(CoinType coinType)
    {
        var projectileType = AmmoConfig.GetProjectileTypeFromCoin(coinType);
        int ammo = AmmoConfig.GetAmmoFromCoin(coinType);
        
        if (projectileType == ProjectileType.Gold)
            GoldAmmo += ammo;
        else
            RedAmmo += ammo;
        
        Changed?.Invoke(this);
    }
    
    public bool CanFire(ProjectileType type)
    {
        return type switch
        {
            ProjectileType.Gold => GoldAmmo > 0,
            ProjectileType.Red => RedAmmo > 0,
            _ => false,
        };
    }
    
    public bool TryConsumeAmmo(ProjectileType type)
    {
        if (!CanFire(type)) return false;
        
        int cost = AmmoConfig.GetDamageMultiplier(type);
        switch (type)
        {
            case ProjectileType.Gold:
                GoldAmmo = Math.Max(0, GoldAmmo - cost);
                break;
            case ProjectileType.Red:
                RedAmmo = Math.Max(0, RedAmmo - cost);
                break;
        }
        
        Changed?.Invoke(this);
        return true;
    }
    
    public bool TryConsumeSelectedAmmo()
    {
        return TryConsumeAmmo(SelectedType);
    }
    
    public int GetSelectedAmmoCount()
    {
        return SelectedType switch
        {
            ProjectileType.Gold => GoldAmmo,
            ProjectileType.Red => RedAmmo,
            _ => 0,
        };
    }
    
    public void SetAmmo(int gold, int red)
    {
        GoldAmmo = gold;
        RedAmmo = red;
        Changed?.Invoke(this);
    }
}
