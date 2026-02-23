using Starfield2026.Core.Save;
using Starfield2026.Core.Screens;
using Starfield2026.Core.Systems;

namespace Starfield2026.Core.Managers;

public class CoinCollector
{
    private readonly GameState _state;
    private readonly AmmoSystem _ammo;
    private readonly BoostSystem _boosts;
    
    public CoinCollector(GameState state, AmmoSystem ammo, BoostSystem boosts)
    {
        _state = state;
        _ammo = ammo;
        _boosts = boosts;
    }
    
    public void CollectFromScreen(IGameScreen screen)
    {
        var coins = screen.CoinSystem;
        if (coins == null) return;
        var (gold, red, blue, green) = coins.GetAndResetNewlyCollected();
        
        if (gold > 0 || red > 0 || blue > 0 || green > 0)
        {
            _state.AddCoins(gold, red, blue, green);
            
            for (int i = 0; i < gold; i++)
                _ammo.AddAmmoFromCoin(CoinType.Gold);
            for (int i = 0; i < red; i++)
                _ammo.AddAmmoFromCoin(CoinType.Red);
            
            for (int i = 0; i < blue; i++)
                _boosts.AddBoost();
            
            if (green > 0 && _state.HealthPercent < 1f)
            {
                int healAmount = (int)(green * _state.MaxHealth * 0.25f);
                _state.Heal(healAmount);
            }
        }
    }
}
