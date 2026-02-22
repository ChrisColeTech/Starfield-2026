namespace Starfield2026.Core.Save;

public class GameState
{
    public int TotalCoins { get; private set; }
    public int GoldAmmo { get; private set; }
    public int RedAmmo { get; private set; }
    public int MaxHealth { get; private set; } = 100;
    public int CurrentHealth { get; private set; } = 100;
    public int BoostCount { get; private set; }
    public string CurrentScreen { get; private set; } = "space";
    public string? CurrentMapId { get; private set; }
    public int? CharacterId { get; private set; }
    public Microsoft.Xna.Framework.Vector3 PlayerPosition { get; set; } = new Microsoft.Xna.Framework.Vector3(80, 0.825f, 80);

    private GameDatabase _database = null!;

    public void Initialize(GameDatabase database, PlayerProfile? profile)
    {
        _database = database;

        if (profile != null)
        {
            TotalCoins = profile.CoinCount;
            GoldAmmo = profile.GoldAmmo;
            RedAmmo = profile.RedAmmo;
            BoostCount = profile.BoostCount;
            CurrentScreen = profile.CurrentScreen ?? "space";
            CurrentMapId = profile.CurrentMapId;
            CharacterId = profile.CharacterId;
            PlayerPosition = profile.Position;
        }

        CurrentHealth = MaxHealth;
    }

    public void AddCoins(int gold, int red, int blue = 0, int green = 0)
    {
        TotalCoins += gold + (red * 3) + (blue * 5) + (green * 5);
        _database.SaveCoinCount(TotalCoins);
    }

    public void AddAmmo(int gold, int red)
    {
        GoldAmmo += gold;
        RedAmmo += red;
        _database.SaveAmmo(GoldAmmo, RedAmmo);
    }

    public void SetAmmo(int gold, int red)
    {
        GoldAmmo = gold;
        RedAmmo = red;
        _database.SaveAmmo(GoldAmmo, RedAmmo);
    }

    public void TakeDamage(int amount)
    {
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
    }

    public void Heal(int amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
    }

    public float HealthPercent => (float)CurrentHealth / MaxHealth;

    public void SetScreen(string screen)
    {
        CurrentScreen = screen;
        Save();
    }

    public void SetMap(string mapId)
    {
        CurrentMapId = mapId;
        Save();
    }

    public void SetCharacterId(int? id)
    {
        CharacterId = id;
        Save();
    }

    public void SetBoostCount(int count)
    {
        BoostCount = count;
        Save();
    }

    public void Save()
    {
        _database?.SaveProfile(new PlayerProfile
        {
            CoinCount = TotalCoins,
            GoldAmmo = GoldAmmo,
            RedAmmo = RedAmmo,
            BoostCount = BoostCount,
            CurrentScreen = CurrentScreen,
            CurrentMapId = CurrentMapId ?? "overworld_grid",
            Position = PlayerPosition,
            CharacterId = CharacterId,
        });
    }
}
