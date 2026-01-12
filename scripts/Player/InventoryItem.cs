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
    public WeaponData? WeaponData { get; set; }
}
