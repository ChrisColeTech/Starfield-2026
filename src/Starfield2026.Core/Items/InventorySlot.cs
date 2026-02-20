namespace Starfield2026.Core.Items;

public class InventorySlot
{
    public int ItemId { get; set; }
    public int Quantity { get; set; }

    public InventorySlot(int itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}
