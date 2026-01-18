namespace DAIgame.Core.Items;

using Godot;

/// <summary>
/// Defines what effect a usable item has.
/// </summary>
public enum UsableEffect
{
    /// <summary>No effect (placeholder)</summary>
    None,

    /// <summary>Restores health</summary>
    Heal,

    /// <summary>Reduces cold exposure</summary>
    Warmth,

    /// <summary>Provides temporary speed boost</summary>
    SpeedBoost,

    /// <summary>Provides temporary damage resistance</summary>
    DamageResist,

    /// <summary>Throwable/grenade type</summary>
    Throwable
}

/// <summary>
/// An item that can be used/consumed (medkits, food, grenades, etc.).
/// </summary>
[GlobalClass]
public partial class UsableItem : Item
{
    private const int DefaultMaxStack = 10;

    /// <summary>
    /// The effect type when this item is used.
    /// </summary>
    [Export]
    public UsableEffect Effect { get; set; } = UsableEffect.None;

    /// <summary>
    /// Primary effect value (e.g., heal amount, speed boost %, duration).
    /// </summary>
    [Export]
    public float EffectValue { get; set; } = 0f;

    /// <summary>
    /// Duration of the effect in seconds (0 = instant).
    /// </summary>
    [Export]
    public float EffectDuration { get; set; } = 0f;

    /// <summary>
    /// Time in seconds to use/consume this item.
    /// </summary>
    [Export]
    public float UseTime { get; set; } = 0f;

    /// <summary>
    /// Cooldown in seconds before the item can be used again.
    /// </summary>
    [Export]
    public float Cooldown { get; set; } = 0f;

    /// <summary>
    /// Whether the item is consumed on use.
    /// </summary>
    [Export]
    public bool ConsumedOnUse { get; set; } = true;

    public UsableItem()
    {
        ItemType = ItemType.Usable;
        MaxStack = DefaultMaxStack;
    }

    /// <inheritdoc/>
    public override Item Clone()
    {
        return new UsableItem
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
            Effect = Effect,
            EffectValue = EffectValue,
            EffectDuration = EffectDuration,
            UseTime = UseTime,
            Cooldown = Cooldown,
            ConsumedOnUse = ConsumedOnUse
        };
    }

    /// <summary>
    /// Creates a healing usable item.
    /// </summary>
    public static UsableItem CreateHealingItem(
        string itemId,
        string displayName,
        float healAmount,
        float useTime = 1f,
        Texture2D? icon = null)
    {
        return new UsableItem
        {
            ItemId = itemId,
            DisplayName = displayName,
            Description = $"Restores {healAmount} health.",
            Icon = icon,
            Effect = UsableEffect.Heal,
            EffectValue = healAmount,
            UseTime = useTime,
            ConsumedOnUse = true
        };
    }

    /// <summary>
    /// Creates a warmth-providing usable item.
    /// </summary>
    public static UsableItem CreateWarmthItem(
        string itemId,
        string displayName,
        float warmthValue,
        float duration,
        Texture2D? icon = null)
    {
        return new UsableItem
        {
            ItemId = itemId,
            DisplayName = displayName,
            Description = $"Provides warmth for {duration} seconds.",
            Icon = icon,
            Effect = UsableEffect.Warmth,
            EffectValue = warmthValue,
            EffectDuration = duration,
            ConsumedOnUse = true
        };
    }
}
