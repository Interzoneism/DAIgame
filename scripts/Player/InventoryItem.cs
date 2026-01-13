namespace DAIgame.Player;

using DAIgame.Combat;
using Godot;

public enum InventoryItemType
{
    Weapon,
    Outfit,
    Headwear,
    Shoes,
    Usable,
    Ammo,
    Misc
}

public enum InventorySlotType
{
    Backpack,
    Outfit,
    Headwear,
    Shoes,
    Usable1,
    Usable2,
    LeftHand,
    RightHand
}

[GlobalClass]
public partial class InventoryItem : Resource
{
    [Export]
    public string ItemId { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "";

    [Export]
    public Texture2D? Icon { get; set; }

    [Export]
    public InventoryItemType ItemType { get; set; } = InventoryItemType.Misc;

    [Export]
    public Combat.AmmoType AmmoType { get; set; } = Combat.AmmoType.None;

    [Export]
    public int StackCount { get; set; } = 1;

    [Export]
    public int MaxStack { get; set; } = 1;

    [Export]
    public WeaponData? WeaponData { get; set; }

    public bool IsStackable => MaxStack > 1;

    public bool CanStackWith(InventoryItem other)
    {
        if (!IsStackable || !other.IsStackable)
        {
            return false;
        }

        if (ItemType == InventoryItemType.Ammo || other.ItemType == InventoryItemType.Ammo)
        {
            return ItemType == InventoryItemType.Ammo
                && other.ItemType == InventoryItemType.Ammo
                && AmmoType == other.AmmoType;
        }

        return ItemId == other.ItemId;
    }

    public InventoryItem CreateStackCopy(int stackCount)
    {
        return new InventoryItem
        {
            ItemId = ItemId,
            DisplayName = DisplayName,
            Icon = Icon,
            ItemType = ItemType,
            AmmoType = AmmoType,
            StackCount = stackCount,
            MaxStack = MaxStack,
            WeaponData = WeaponData
        };
    }
}
