using System.Collections.Generic;
using System.Linq;
using Starfield2026.Core.Pokemon;

namespace Starfield2026.Core.Items;

/// <summary>
/// Player's bag, organized by pouch (category).
/// </summary>
public class PlayerInventory
{
    private readonly Dictionary<ItemCategory, List<InventorySlot>> _pouches = new();

    public IReadOnlyList<InventorySlot> GetPouch(ItemCategory category)
    {
        return _pouches.TryGetValue(category, out var pouch)
            ? pouch
            : (IReadOnlyList<InventorySlot>)[];
    }

    public IReadOnlyList<InventorySlot> GetPouch(ItemCategory[] categories)
    {
        var result = new List<InventorySlot>();
        foreach (var cat in categories)
        {
            if (_pouches.TryGetValue(cat, out var pouch))
                result.AddRange(pouch);
        }
        return result;
    }

    public void AddItem(int itemId, int quantity = 1)
    {
        var def = ItemRegistry.GetItem(itemId);
        if (def == null) return;

        if (!_pouches.TryGetValue(def.Category, out var pouch))
        {
            pouch = new List<InventorySlot>();
            _pouches[def.Category] = pouch;
        }

        var existing = pouch.FirstOrDefault(s => s.ItemId == itemId);
        if (existing != null)
            existing.Quantity += quantity;
        else
            pouch.Add(new InventorySlot(itemId, quantity));
    }

    /// <summary>Enumerate all non-empty pouches for serialization.</summary>
    public IEnumerable<(ItemCategory category, IReadOnlyList<InventorySlot> items)> GetAllPouches()
    {
        foreach (var kvp in _pouches)
        {
            if (kvp.Value.Count > 0)
                yield return (kvp.Key, kvp.Value);
        }
    }

    public bool RemoveItem(int itemId, int quantity = 1)
    {
        var def = ItemRegistry.GetItem(itemId);
        if (def == null) return false;

        if (!_pouches.TryGetValue(def.Category, out var pouch))
            return false;

        var slot = pouch.FirstOrDefault(s => s.ItemId == itemId);
        if (slot == null || slot.Quantity < quantity)
            return false;

        slot.Quantity -= quantity;
        if (slot.Quantity <= 0)
            pouch.Remove(slot);
        return true;
    }

    public ItemUseResult? UseItemOnPokemon(int itemId, PartyPokemon target,
        bool inBattle, int? moveIndex = null)
    {
        var def = ItemRegistry.GetItem(itemId);
        if (def == null)
            return new ItemUseResult(false, "Unknown item.");

        if (!ItemUseHandler.CanUseItem(def, target, inBattle, moveIndex))
            return new ItemUseResult(false, "It won't have any effect.");

        var result = ItemUseHandler.UseItem(def, target, moveIndex);
        if (result.Success)
            RemoveItem(itemId, 1);
        return result;
    }

    /// <summary>Remove all items. Used during save load.</summary>
    public void Clear()
    {
        _pouches.Clear();
    }

    /// <summary>Create an inventory with test items for development.</summary>
    public static PlayerInventory CreateTestInventory()
    {
        var inv = new PlayerInventory();
        // Medicine
        inv.AddItem(100, 5);  // Potion
        inv.AddItem(101, 3);  // Super Potion
        inv.AddItem(102, 1);  // Hyper Potion
        inv.AddItem(120, 3);  // Antidote
        inv.AddItem(124, 2);  // Parlyz Heal
        inv.AddItem(105, 1);  // Full Heal
        inv.AddItem(130, 2);  // Revive
        // Pokeballs
        inv.AddItem(0, 10);   // Poke Ball
        inv.AddItem(1, 3);    // Great Ball
        // Berries
        inv.AddItem(200, 5);  // Oran Berry
        inv.AddItem(201, 3);  // Sitrus Berry
        inv.AddItem(212, 2);  // Pecha Berry
        return inv;
    }
}
