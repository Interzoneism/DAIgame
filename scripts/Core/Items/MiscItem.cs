namespace DAIgame.Core.Items;

using Godot;

/// <summary>
/// A generic/miscellaneous item that doesn't fit other categories.
/// Used for crafting materials, junk, quest items, etc.
/// </summary>
[GlobalClass]
public partial class MiscItem : Item
{
    private const int DefaultMaxStack = 99;

    /// <summary>
    /// Whether this item can be scrapped for materials.
    /// </summary>
    [Export]
    public bool CanScrap { get; set; } = true;

    /// <summary>
    /// Whether this is a quest/key item that cannot be dropped.
    /// </summary>
    [Export]
    public bool IsQuestItem { get; set; } = false;

    /// <summary>
    /// Category tag for filtering/sorting (e.g., "material", "junk", "quest").
    /// </summary>
    [Export]
    public string Category { get; set; } = "misc";

    public MiscItem()
    {
        ItemType = ItemType.Misc;
        MaxStack = DefaultMaxStack;
    }

    /// <inheritdoc/>
    public override Item Clone()
    {
        return new MiscItem
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
            CanScrap = CanScrap,
            IsQuestItem = IsQuestItem,
            Category = Category
        };
    }

    /// <summary>
    /// Creates a miscellaneous item.
    /// </summary>
    public static MiscItem Create(
        string itemId,
        string displayName,
        string description = "",
        int maxStack = DefaultMaxStack,
        Texture2D? icon = null)
    {
        return new MiscItem
        {
            ItemId = itemId,
            DisplayName = displayName,
            Description = description,
            Icon = icon,
            MaxStack = maxStack
        };
    }
}
