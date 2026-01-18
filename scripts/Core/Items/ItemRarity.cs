namespace DAIgame.Core.Items;

using Godot;

/// <summary>
/// Defines the rarity tier of an item.
/// </summary>
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Extension methods for ItemRarity.
/// </summary>
public static class ItemRarityExtensions
{
    /// <summary>
    /// Gets the display color for a rarity tier.
    /// </summary>
    public static Color GetColor(this ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => new Color(0.8f, 0.8f, 0.8f),    // Gray
            ItemRarity.Uncommon => new Color(0.3f, 0.9f, 0.3f),  // Green
            ItemRarity.Rare => new Color(0.3f, 0.5f, 1.0f),      // Blue
            ItemRarity.Epic => new Color(0.7f, 0.3f, 0.9f),      // Purple
            ItemRarity.Legendary => new Color(1.0f, 0.7f, 0.2f), // Orange/Gold
            _ => Colors.White
        };
    }

    /// <summary>
    /// Gets the display name for a rarity tier.
    /// </summary>
    public static string GetDisplayName(this ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => "Common",
            ItemRarity.Uncommon => "Uncommon",
            ItemRarity.Rare => "Rare",
            ItemRarity.Epic => "Epic",
            ItemRarity.Legendary => "Legendary",
            _ => "Unknown"
        };
    }
}
