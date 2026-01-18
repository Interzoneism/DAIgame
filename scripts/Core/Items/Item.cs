namespace DAIgame.Core.Items;

using Godot;

/// <summary>
/// Base class for all items in the game. Everything that can go into inventory,
/// be looted, equipped, or used must derive from this class.
/// </summary>
[GlobalClass]
public partial class Item : Resource
{
    /// <summary>
    /// Unique identifier for this item type (e.g., "pistol", "ammo_small", "medkit").
    /// </summary>
    [Export]
    public string ItemId { get; set; } = "";

    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Description shown in tooltips.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = "";

    /// <summary>
    /// Icon displayed in inventory UI.
    /// </summary>
    [Export]
    public Texture2D? Icon { get; set; }

    /// <summary>
    /// The type/category of this item.
    /// </summary>
    [Export]
    public ItemType ItemType { get; set; } = ItemType.Misc;

    /// <summary>
    /// Rarity tier of this item.
    /// </summary>
    [Export]
    public ItemRarity Rarity { get; set; } = ItemRarity.Common;

    /// <summary>
    /// Current stack count for this item instance.
    /// </summary>
    [Export]
    public int StackCount { get; set; } = 1;

    /// <summary>
    /// Maximum number of items that can stack in one slot.
    /// </summary>
    [Export]
    public int MaxStack { get; set; } = 1;

    /// <summary>
    /// Base value of the item (for trading/selling).
    /// </summary>
    [Export]
    public int BaseValue { get; set; } = 0;

    /// <summary>
    /// Weight of a single item unit (for future encumbrance systems).
    /// </summary>
    [Export]
    public float Weight { get; set; } = 0f;

    /// <summary>
    /// Whether this item can stack with other items.
    /// </summary>
    public bool IsStackable => MaxStack > 1;

    /// <summary>
    /// Checks if this item can stack with another item.
    /// </summary>
    public virtual bool CanStackWith(Item other)
    {
        if (!IsStackable || !other.IsStackable)
        {
            return false;
        }

        return ItemId == other.ItemId;
    }

    /// <summary>
    /// Creates a copy of this item with the specified stack count.
    /// </summary>
    public virtual Item CreateStackCopy(int stackCount)
    {
        var copy = Clone();
        copy.StackCount = stackCount;
        return copy;
    }

    /// <summary>
    /// Creates a deep copy of this item.
    /// Override in derived classes to copy specific properties.
    /// </summary>
    public virtual Item Clone()
    {
        return new Item
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
            Weight = Weight
        };
    }

    /// <summary>
    /// Gets a formatted display string for this item (including stack count if stackable).
    /// </summary>
    public string GetDisplayString() => StackCount > 1 ? $"{DisplayName} x{StackCount}" : DisplayName;
}
