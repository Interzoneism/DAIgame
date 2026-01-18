namespace DAIgame.Core.Items;

/// <summary>
/// Defines which equipment slot an item can be placed in.
/// </summary>
public enum InventorySlotType
{
    /// <summary>Main backpack grid storage</summary>
    Backpack,

    /// <summary>Body outfit/armor slot</summary>
    Outfit,

    /// <summary>Headwear slot</summary>
    Headwear,

    /// <summary>Footwear slot</summary>
    Shoes,

    /// <summary>First quick-use slot</summary>
    Usable1,

    /// <summary>Second quick-use slot</summary>
    Usable2,

    /// <summary>Left hand weapon slot</summary>
    LeftHand,

    /// <summary>Right hand weapon slot</summary>
    RightHand
}

/// <summary>
/// Extension methods for inventory slot operations.
/// </summary>
public static class InventorySlotExtensions
{
    /// <summary>
    /// Checks if an item can be placed in a specific slot type.
    /// </summary>
    public static bool CanAcceptItem(this InventorySlotType slotType, Item? item)
    {
        if (item is null)
        {
            return true;
        }

        if (slotType == InventorySlotType.Backpack)
        {
            return true;
        }

        return slotType switch
        {
            InventorySlotType.LeftHand or InventorySlotType.RightHand => item.ItemType == ItemType.Weapon,
            InventorySlotType.Usable1 or InventorySlotType.Usable2 => item.ItemType == ItemType.Usable,
            InventorySlotType.Outfit => item.ItemType == ItemType.Outfit,
            InventorySlotType.Headwear => item.ItemType == ItemType.Headwear,
            InventorySlotType.Shoes => item.ItemType == ItemType.Shoes,
            InventorySlotType.Backpack => throw new System.NotImplementedException(),
            _ => false
        };
    }

    /// <summary>
    /// Gets the expected item type for a slot.
    /// </summary>
    public static ItemType? GetExpectedItemType(this InventorySlotType slotType)
    {
        return slotType switch
        {
            InventorySlotType.LeftHand or InventorySlotType.RightHand => ItemType.Weapon,
            InventorySlotType.Usable1 or InventorySlotType.Usable2 => ItemType.Usable,
            InventorySlotType.Outfit => ItemType.Outfit,
            InventorySlotType.Headwear => ItemType.Headwear,
            InventorySlotType.Shoes => ItemType.Shoes,
            InventorySlotType.Backpack => throw new System.NotImplementedException(),
            _ => null
        };
    }

    /// <summary>
    /// Checks if this is a weapon slot.
    /// </summary>
    public static bool IsWeaponSlot(this InventorySlotType slotType)
        => slotType is InventorySlotType.LeftHand or InventorySlotType.RightHand;
}
