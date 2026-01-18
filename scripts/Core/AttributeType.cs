namespace DAIgame.Core;

/// <summary>
/// The "Hex-Core" attributes that define a character's base capabilities.
/// All attributes default to 10 (average human). Values above/below 10
/// provide bonuses/penalties to derived stats.
/// </summary>
public enum AttributeType
{
    /// <summary>
    /// Physical power. Affects melee damage, carrying capacity, recoil control,
    /// and knockback force.
    /// </summary>
    Strength,

    /// <summary>
    /// Agility and fine motor control. Affects movement speed, turn speed,
    /// reload speed, and reduces backward movement penalty.
    /// </summary>
    Dexterity,

    /// <summary>
    /// Physical resilience. Affects max health, cold resistance, and
    /// trauma threshold.
    /// </summary>
    Constitution,

    /// <summary>
    /// Mental acuity and learning. Affects crafting speed, scrap efficiency,
    /// and tech-related abilities.
    /// </summary>
    Intelligence,

    /// <summary>
    /// Sensory awareness. Affects aim stability, reload speed, view distance,
    /// and hearing range.
    /// </summary>
    Perception,

    /// <summary>
    /// Gut instinct and awareness. Affects crit chance, loot quality,
    /// and danger sense.
    /// </summary>
    Intuition
}
