using System.Collections.Generic;
using System.Text.Json;

namespace Starfield2026.Core.Items;

public static class ItemRegistry
{
    private static Dictionary<int, ItemDefinition>? _items;
    private static readonly object _lock = new();
    
    public static void Initialize(string jsonData)
    {
        lock (_lock)
        {
            var items = JsonSerializer.Deserialize<ItemData[]>(jsonData);
            _items = new Dictionary<int, ItemDefinition>();
            
            if (items != null)
            {
                foreach (var data in items)
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
        }
    }
    
    private static void EnsureInitialized()
    {
        if (_items == null)
        {
            try 
            {
                var json = System.IO.File.ReadAllText("Starfield2026.Assets/Data/items.json");
                Initialize(json);
            } 
            catch
            {
                _items = new Dictionary<int, ItemDefinition>();
            }
        }
    }
    
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
