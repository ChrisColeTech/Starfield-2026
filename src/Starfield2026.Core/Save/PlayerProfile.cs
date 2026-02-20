using System;
using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Save;

public class PlayerProfile
{
    public int CoinCount { get; set; }
    public Vector3 Position { get; set; }
    public string CurrentScreen { get; set; } = "space";
    public string CurrentMapId { get; set; } = "overworld_grid";
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    
    public int GoldAmmo { get; set; }
    public int RedAmmo { get; set; }
    public int BoostCount { get; set; }
}
