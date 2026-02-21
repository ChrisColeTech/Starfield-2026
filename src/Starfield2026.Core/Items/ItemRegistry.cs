using System.Collections.Generic;
using System.Linq;
using Starfield2026.Core.Data;

namespace Starfield2026.Core.Items;

/// <summary>
/// Item registry backed by GameDataDb (SQLite).
/// Loads all items from the database on first access and caches them.
/// </summary>
public static class ItemRegistry
{
    private static Dictionary<int, ItemDefinition>? _items;
    private static readonly object _lock = new();

    private static ItemCategory ParseCategory(string category) => category switch
    {
        "Pokeball" => ItemCategory.Pokeball,
        "Medicine" => ItemCategory.Medicine,
        "Battle" => ItemCategory.Battle,
        "Berry" => ItemCategory.Berry,
        "KeyItem" => ItemCategory.KeyItem,
        "TM" => ItemCategory.TM,
        "HM" => ItemCategory.HM,
        "EvolutionStone" => ItemCategory.EvolutionStone,
        "HeldItem" => ItemCategory.HeldItem,
        "Valuable" => ItemCategory.Valuable,
        "Mail" => ItemCategory.Mail,
        _ => ItemCategory.Valuable
    };

    private static void EnsureInitialized()
    {
        if (_items != null) return;

        lock (_lock)
        {
            if (_items != null) return;

            _items = new Dictionary<int, ItemDefinition>();
            try
            {
                var dbItems = GameDataDb.GetAllItems();
                foreach (var data in dbItems)
                {
                    var category = ParseCategory(data.Category);
                    var definition = new ItemDefinition(
                        data.Id,
                        data.Name,
                        data.Sprite,
                        category,
                        data.BuyPrice,
                        data.SellPrice,
                        data.UsableInBattle,
                        data.UsableOverworld,
                        data.Effect
                    );
                    _items[data.Id] = definition;
                }
            }
            catch
            {
                // DB not available — empty registry
            }
        }
    }

    public static ItemDefinition? GetItem(int id)
    {
        EnsureInitialized();
        return _items!.TryGetValue(id, out var item) ? item : null;
    }

    public static IEnumerable<ItemDefinition> GetItemsByCategory(ItemCategory category)
    {
        EnsureInitialized();
        return _items!.Values.Where(i => i.Category == category);
    }

    public static IEnumerable<ItemDefinition> AllItems
    {
        get
        {
            EnsureInitialized();
            return _items!.Values;
        }
    }
}
