namespace DAIgame.Core.Items;

/// <summary>
/// Defines the category/type of an item.
/// </summary>
public enum ItemType
{
    /// <summary>Weapons (melee and ranged)</summary>
    Weapon,

    /// <summary>Ammunition for ranged weapons</summary>
    Ammo,

    /// <summary>Body outfit/armor</summary>
    Outfit,

    /// <summary>Headwear (hats, helmets)</summary>
    Headwear,

    /// <summary>Footwear (shoes, boots)</summary>
    Shoes,

    /// <summary>Consumables and usable items (medkits, grenades)</summary>
    Usable,

    /// <summary>Miscellaneous/junk items</summary>
    Misc
}
