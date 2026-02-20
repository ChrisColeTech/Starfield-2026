using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starfield2026.Core.Items;

public class ItemData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = "";
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
    
    [JsonPropertyName("buyPrice")]
    public int BuyPrice { get; set; }
    
    [JsonPropertyName("sellPrice")]
    public int SellPrice { get; set; }
    
    [JsonPropertyName("usableInBattle")]
    public bool UsableInBattle { get; set; }
    
    [JsonPropertyName("usableOverworld")]
    public bool UsableOverworld { get; set; }
    
    [JsonPropertyName("effect")]
    public string? Effect { get; set; }
}
