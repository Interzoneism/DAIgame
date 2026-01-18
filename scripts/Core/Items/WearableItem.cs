namespace DAIgame.Core.Items;

using Godot;

/// <summary>
/// The slot type for wearable items.
/// </summary>
public enum WearableSlot
{
    Outfit,
    Headwear,
    Shoes
}

/// <summary>
/// An item that can be worn by the player (outfit, headwear, shoes).
/// </summary>
[GlobalClass]
public partial class WearableItem : Item
{
    /// <summary>
    /// Which equipment slot this wearable goes into.
    /// </summary>
    [Export]
    public WearableSlot Slot { get; set; } = WearableSlot.Outfit;

    /// <summary>
    /// Armor/protection value (damage reduction).
    /// </summary>
    [Export]
    public float Armor { get; set; } = 0f;

    /// <summary>
    /// Cold resistance bonus (reduces cold exposure rate).
    /// </summary>
    [Export]
    public float ColdResistance { get; set; } = 0f;

    /// <summary>
    /// Movement speed modifier (1.0 = normal, 1.1 = 10% faster, 0.9 = 10% slower).
    /// </summary>
    [Export]
    public float SpeedModifier { get; set; } = 1f;

    /// <summary>
    /// Noise modifier (1.0 = normal, 0.8 = 20% quieter for stealth).
    /// </summary>
    [Export]
    public float NoiseModifier { get; set; } = 1f;

    public WearableItem()
    {
        MaxStack = 1;
    }

    /// <summary>
    /// Sets the ItemType based on the WearableSlot.
    /// Call after setting Slot.
    /// </summary>
    public void UpdateItemType()
    {
        ItemType = Slot switch
        {
            WearableSlot.Outfit => ItemType.Outfit,
            WearableSlot.Headwear => ItemType.Headwear,
            WearableSlot.Shoes => ItemType.Shoes,
            _ => ItemType.Outfit
        };
    }

    /// <inheritdoc/>
    public override bool CanStackWith(Item other) =>
        // Wearables never stack
        false;

    /// <inheritdoc/>
    public override Item Clone()
    {
        return new WearableItem
        {
            ItemId = ItemId,
            DisplayName = DisplayName,
            Description = Description,
            Icon = Icon,
            ItemType = ItemType,
            Rarity = Rarity,
            StackCount = StackCount,
            MaxStack = MaxStack,
            BaseValue = BaseValue,
            Weight = Weight,
            Slot = Slot,
            Armor = Armor,
            ColdResistance = ColdResistance,
            SpeedModifier = SpeedModifier,
            NoiseModifier = NoiseModifier
        };
    }

    /// <summary>
    /// Creates a WearableItem with the specified properties.
    /// </summary>
    public static WearableItem Create(
        string itemId,
        string displayName,
        WearableSlot slot,
        Texture2D? icon = null,
        float armor = 0f,
        float coldResistance = 0f)
    {
        var item = new WearableItem
        {
            ItemId = itemId,
            DisplayName = displayName,
            Slot = slot,
            Icon = icon,
            Armor = armor,
            ColdResistance = coldResistance
        };
        item.UpdateItemType();
        return item;
    }
}
