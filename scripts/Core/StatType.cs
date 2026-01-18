namespace DAIgame.Core;

/// <summary>
/// All derived stats that can be calculated from attributes, feats, and equipment.
/// These are the "kitchen sink" of all possible stats for future-proofing.
/// </summary>
public enum StatType
{
    #region Mobility

    /// <summary>
    /// Movement speed in pixels per second. Base: 200.
    /// </summary>
    MoveSpeed,

    /// <summary>
    /// Multiplier for sprint speed. Base: 1.5 (50% faster).
    /// </summary>
    SprintSpeedMult,

    /// <summary>
    /// Turn/aim speed in radians per second. Base: 20.
    /// </summary>
    TurnSpeed,

    /// <summary>
    /// Multiplier for backward movement speed. Base: 0.5 (50% speed).
    /// </summary>
    BackpedalPenalty,

    /// <summary>
    /// Multiplier for movement speed while crouched. Base: 0.5.
    /// </summary>
    CrouchSpeedMult,

    #endregion

    #region Vitality

    /// <summary>
    /// Maximum health points. Base: 100.
    /// </summary>
    MaxHealth,

    /// <summary>
    /// Health regeneration per second. Base: 0.
    /// </summary>
    HealthRegen,

    /// <summary>
    /// Maximum stamina points. Base: 100.
    /// </summary>
    MaxStamina,

    /// <summary>
    /// Stamina regeneration per second. Base: 25.
    /// </summary>
    StaminaRegen,

    #endregion

    #region Environmental

    /// <summary>
    /// Reduces cold exposure gain rate. Base: 0. Higher = better.
    /// </summary>
    ColdResistance,

    /// <summary>
    /// Tolerance to heat effects. Base: 0. Higher = better.
    /// </summary>
    HeatTolerance,

    /// <summary>
    /// Damage threshold before trauma effects apply. Base: 0.
    /// </summary>
    TraumaThreshold,

    #endregion

    #region Combat - Gunplay

    /// <summary>
    /// Multiplier for reload speed. Base: 1.0. Higher = faster.
    /// </summary>
    ReloadSpeedMult,

    /// <summary>
    /// Speed of swapping weapons. Base: 1.0. Higher = faster.
    /// </summary>
    WeaponSwapSpeed,

    /// <summary>
    /// Reduces weapon recoil. Base: 0. Subtracted from WeaponData.Recoil.
    /// </summary>
    RecoilControl,

    /// <summary>
    /// Reduces aim-down-sights sway. Base: 0. Higher = steadier.
    /// </summary>
    AimStability,

    /// <summary>
    /// Multiplier for weapon effective range. Base: 1.0.
    /// </summary>
    RangeMultiplier,

    #endregion

    #region Combat - Melee

    /// <summary>
    /// Multiplier for melee damage. Base: 1.0.
    /// </summary>
    MeleeDamageMult,

    /// <summary>
    /// Multiplier for melee attack speed. Base: 1.0. Higher = faster.
    /// </summary>
    MeleeAttackSpeed,

    /// <summary>
    /// Stamina cost for kick attacks. Base: 20.
    /// </summary>
    KickCost,

    /// <summary>
    /// Multiplier for knockback force applied to enemies. Base: 1.0.
    /// </summary>
    KnockbackForceMult,

    #endregion

    #region Senses

    /// <summary>
    /// View/detection distance in pixels. Base: 500.
    /// </summary>
    ViewDistance,

    /// <summary>
    /// Range at which footsteps can be heard. Base: 200.
    /// </summary>
    FootstepHearingRange,

    /// <summary>
    /// Ability to see in darkness. Base: 0. Higher = better night vision.
    /// </summary>
    DarknessVision,

    #endregion

    #region Meta

    /// <summary>
    /// Multiplier for crafting speed. Base: 1.0. Higher = faster.
    /// </summary>
    CraftingSpeed,

    /// <summary>
    /// Multiplier for scrap yield from salvaging. Base: 1.0. Higher = more scrap.
    /// </summary>
    ScrapEfficiency,

    /// <summary>
    /// Chance for critical hits (0.0 to 1.0). Base: 0.05 (5%).
    /// </summary>
    CritChance,

    /// <summary>
    /// Modifier for loot quality rolls. Base: 0. Higher = better loot.
    /// </summary>
    LootQuality

    #endregion
}
