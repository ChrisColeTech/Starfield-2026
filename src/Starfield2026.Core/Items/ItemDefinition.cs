namespace Starfield2026.Core.Items;

public record ItemDefinition(
    int Id,
    string Name,
    string SpriteName,
    ItemCategory Category,
    int BuyPrice,
    int SellPrice,
    bool UsableInBattle,
    bool UsableOverworld,
    string? Effect = null
)
{
    public ItemEffect? ParsedEffect { get; } = ItemEffectParser.Parse(Effect);
}
