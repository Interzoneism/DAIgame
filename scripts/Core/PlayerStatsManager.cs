namespace DAIgame.Core;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Manages player attributes and derived stats. Recalculates stats dynamically
/// based on base attributes, active feats, and equipment modifiers.
/// Serves as the "data provider" for PlayerController and other systems.
/// </summary>
public partial class PlayerStatsManager : Node
{
    #region Constants - Base Stat Values

    private const int DefaultAttributeValue = 10;

    // Mobility
    private const float BaseMoveSpeed = 100f;
    private const float BaseSprintSpeedMult = 1.5f;
    private const float BaseTurnSpeed = 10f;
    private const float BaseBackpedalPenalty = 0.5f;
    private const float BaseCrouchSpeedMult = 0.5f;

    // Vitality
    private const float BaseMaxHealth = 100f;
    private const float BaseHealthRegen = 0f;
    private const float BaseMaxStamina = 100f;
    private const float BaseStaminaRegen = 25f;

    // Environmental
    private const float BaseColdResistance = 0f;
    private const float BaseHeatTolerance = 0f;
    private const float BaseTraumaThreshold = 0f;

    // Combat - Gunplay
    private const float BaseReloadSpeedMult = 1.0f;
    private const float BaseWeaponSwapSpeed = 1.0f;
    private const float BaseRecoilControl = 0f;
    private const float BaseAimStability = 0f;
    private const float BaseRangeMultiplier = 1.0f;

    // Combat - Melee
    private const float BaseMeleeDamageMult = 1.0f;
    private const float BaseMeleeAttackSpeed = 1.0f;
    private const float BaseKickCost = 20f;
    private const float BaseKnockbackForceMult = 1.0f;

    // Senses
    private const float BaseViewDistance = 500f;
    private const float BaseFootstepHearingRange = 200f;
    private const float BaseDarknessVision = 0f;

    // Meta
    private const float BaseCraftingSpeed = 1.0f;
    private const float BaseScrapEfficiency = 1.0f;
    private const float BaseCritChance = 0.05f;
    private const float BaseLootQuality = 0f;

    #endregion

    #region Signals

    /// <summary>
    /// Emitted when any stat is recalculated.
    /// </summary>
    [Signal]
    public delegate void StatsRecalculatedEventHandler();

    /// <summary>
    /// Emitted when an attribute changes.
    /// </summary>
    [Signal]
    public delegate void AttributeChangedEventHandler(AttributeType attribute, int oldValue, int newValue);

    /// <summary>
    /// Emitted when a feat is added or removed.
    /// </summary>
    [Signal]
    public delegate void FeatsChangedEventHandler();

    #endregion

    #region Data Storage

    /// <summary>
    /// Base attributes (STR, DEX, CON, INT, PER, INT). Default: 10 each.
    /// </summary>
    private readonly Dictionary<AttributeType, int> _baseAttributes = new();

    /// <summary>
    /// Calculated derived stats. Updated by RecalculateStats().
    /// </summary>
    private readonly Dictionary<StatType, float> _derivedStats = new();

    /// <summary>
    /// Currently active feats that modify stats.
    /// </summary>
    private readonly List<FeatDefinition> _activeFeats = new();

    /// <summary>
    /// Equipment-based stat modifiers. Key: StatType, Value: total modifier.
    /// Set by equipment system when gear changes.
    /// </summary>
    private readonly Dictionary<StatType, float> _equipmentModifiers = new();

    #endregion

    #region Lifecycle

    public override void _Ready()
    {
        InitializeDefaults();
        RecalculateStats();
    }

    /// <summary>
    /// Initialize all attributes to default values.
    /// </summary>
    private void InitializeDefaults()
    {
        // Initialize all attributes to 10
        foreach (AttributeType attr in System.Enum.GetValues<AttributeType>())
        {
            _baseAttributes[attr] = DefaultAttributeValue;
        }

        // Initialize all stats to their base values (will be properly calculated in RecalculateStats)
        foreach (StatType stat in System.Enum.GetValues<StatType>())
        {
            _derivedStats[stat] = 0f;
        }
    }

    #endregion

    #region Public API - Attributes

    /// <summary>
    /// Get the value of an attribute.
    /// </summary>
    public int GetAttribute(AttributeType attribute)
    {
        return _baseAttributes.TryGetValue(attribute, out int value) ? value : DefaultAttributeValue;
    }

    /// <summary>
    /// Set the value of an attribute. Triggers stat recalculation.
    /// </summary>
    public void SetAttribute(AttributeType attribute, int value)
    {
        int oldValue = GetAttribute(attribute);
        _baseAttributes[attribute] = value;

        if (oldValue != value)
        {
            GD.Print($"[PlayerStatsManager] Attribute {attribute} changed: {oldValue} -> {value}");
            EmitSignal(SignalName.AttributeChanged, (int)attribute, oldValue, value);
            RecalculateStats();
        }
    }

    /// <summary>
    /// Modify an attribute by a delta. Triggers stat recalculation.
    /// </summary>
    public void ModifyAttribute(AttributeType attribute, int delta)
    {
        SetAttribute(attribute, GetAttribute(attribute) + delta);
    }

    /// <summary>
    /// Gets the difference from the default value (10) for an attribute.
    /// Useful for formulas: (ATT - 10) * modifier.
    /// </summary>
    public int GetAttributeOffset(AttributeType attribute)
    {
        return GetAttribute(attribute) - DefaultAttributeValue;
    }

    #endregion

    #region Public API - Stats

    /// <summary>
    /// Get the calculated value of a derived stat.
    /// </summary>
    public float GetStat(StatType stat)
    {
        return _derivedStats.TryGetValue(stat, out float value) ? value : 0f;
    }

    /// <summary>
    /// Convenience property accessors for commonly used stats.
    /// </summary>
    public float MoveSpeed => GetStat(StatType.MoveSpeed);
    public float TurnSpeed => GetStat(StatType.TurnSpeed);
    public float MaxHealth => GetStat(StatType.MaxHealth);
    public float MaxStamina => GetStat(StatType.MaxStamina);
    public float StaminaRegen => GetStat(StatType.StaminaRegen);
    public float ReloadSpeedMult => GetStat(StatType.ReloadSpeedMult);
    public float RecoilControl => GetStat(StatType.RecoilControl);
    public float ColdResistance => GetStat(StatType.ColdResistance);
    public float KickCost => GetStat(StatType.KickCost);
    public float BackpedalPenalty => GetStat(StatType.BackpedalPenalty);

    #endregion

    #region Public API - Feats

    /// <summary>
    /// Add a feat to the active feats list. Triggers stat recalculation.
    /// </summary>
    public void AddFeat(FeatDefinition feat)
    {
        if (feat == null || _activeFeats.Contains(feat))
        {
            GD.PrintErr($"[PlayerStatsManager] AddFeat failed: feat is null or already active");
            return;
        }

        // Ensure modifier dictionary is built from inspector arrays
        feat.BuildModifierDictionary();
        _activeFeats.Add(feat);

        GD.Print($"[PlayerStatsManager] Feat added: {feat.DisplayName} ({feat.Id})");
        EmitSignal(SignalName.FeatsChanged);
        RecalculateStats();
    }

    /// <summary>
    /// Remove a feat from the active feats list. Triggers stat recalculation.
    /// </summary>
    public bool RemoveFeat(FeatDefinition feat)
    {
        if (feat == null || !_activeFeats.Remove(feat))
        {
            GD.PrintErr($"[PlayerStatsManager] RemoveFeat failed: feat is null or not active");
            return false;
        }

        GD.Print($"[PlayerStatsManager] Feat removed: {feat.DisplayName} ({feat.Id})");
        EmitSignal(SignalName.FeatsChanged);
        RecalculateStats();
        return true;
    }

    /// <summary>
    /// Remove a feat by ID. Triggers stat recalculation.
    /// </summary>
    public bool RemoveFeatById(string featId)
    {
        FeatDefinition? feat = _activeFeats.Find(f => f.Id == featId);
        return feat != null && RemoveFeat(feat);
    }

    /// <summary>
    /// Check if a feat is currently active.
    /// </summary>
    public bool HasFeat(string featId)
    {
        return _activeFeats.Exists(f => f.Id == featId);
    }

    /// <summary>
    /// Get all active feats (read-only).
    /// </summary>
    public IReadOnlyList<FeatDefinition> GetActiveFeats() => _activeFeats.AsReadOnly();

    #endregion

    #region Public API - Equipment Modifiers

    /// <summary>
    /// Set equipment modifier for a stat. Call RecalculateStats() after all equipment is set.
    /// </summary>
    public void SetEquipmentModifier(StatType stat, float value)
    {
        _equipmentModifiers[stat] = value;
    }

    /// <summary>
    /// Clear all equipment modifiers. Call RecalculateStats() after.
    /// </summary>
    public void ClearEquipmentModifiers()
    {
        _equipmentModifiers.Clear();
    }

    /// <summary>
    /// Get the current equipment modifier for a stat.
    /// </summary>
    public float GetEquipmentModifier(StatType stat)
    {
        return _equipmentModifiers.TryGetValue(stat, out float value) ? value : 0f;
    }

    #endregion

    #region Stat Calculation

    /// <summary>
    /// Recalculate all derived stats based on current attributes, feats, and equipment.
    /// Call this after any attribute, feat, or equipment change.
    /// </summary>
    public void RecalculateStats()
    {
        // Get attribute offsets (value - 10) for formulas
        int str = GetAttributeOffset(AttributeType.Strength);
        int dex = GetAttributeOffset(AttributeType.Dexterity);
        int con = GetAttributeOffset(AttributeType.Constitution);
        int intl = GetAttributeOffset(AttributeType.Intelligence);
        int per = GetAttributeOffset(AttributeType.Perception);
        int intu = GetAttributeOffset(AttributeType.Intuition);

        // --- Mobility ---
        // MoveSpeed: 200 + ((DEX - 10) * 5) + ((STR - 10) * 2)
        _derivedStats[StatType.MoveSpeed] = BaseMoveSpeed + (dex * 5f) + (str * 2f);

        // TurnSpeed: 20 + ((DEX - 10) * 0.5f) + ((PER - 10) * 0.2f)
        _derivedStats[StatType.TurnSpeed] = BaseTurnSpeed + (dex * 0.5f) + (per * 0.2f);

        // SprintSpeedMult: Base + small DEX bonus
        _derivedStats[StatType.SprintSpeedMult] = BaseSprintSpeedMult + (dex * 0.02f);

        // BackpedalPenalty: Base - DEX improvement (lower is better for player)
        _derivedStats[StatType.BackpedalPenalty] = Mathf.Max(0.3f, BaseBackpedalPenalty - (dex * 0.02f));

        // CrouchSpeedMult: Base
        _derivedStats[StatType.CrouchSpeedMult] = BaseCrouchSpeedMult;

        // --- Vitality ---
        // MaxHealth: 100 + ((CON - 10) * 5)
        _derivedStats[StatType.MaxHealth] = BaseMaxHealth + (con * 5f);

        // HealthRegen: Base + CON bonus
        _derivedStats[StatType.HealthRegen] = BaseHealthRegen + (con * 0.1f);

        // MaxStamina: Base + CON bonus
        _derivedStats[StatType.MaxStamina] = BaseMaxStamina + (con * 3f);

        // StaminaRegen: Base + CON bonus
        _derivedStats[StatType.StaminaRegen] = BaseStaminaRegen + (con * 1f);

        // --- Environmental ---
        // ColdResistance: 0 + ((CON - 10) * 2.0f)
        _derivedStats[StatType.ColdResistance] = BaseColdResistance + (con * 2.0f);

        // HeatTolerance: Base + CON bonus
        _derivedStats[StatType.HeatTolerance] = BaseHeatTolerance + (con * 1.0f);

        // TraumaThreshold: Base + CON bonus
        _derivedStats[StatType.TraumaThreshold] = BaseTraumaThreshold + (con * 2.0f);

        // --- Combat - Gunplay ---
        // ReloadSpeedMult: 1.0f + ((DEX - 10) * 0.05f) + ((PER - 10) * 0.02f)
        _derivedStats[StatType.ReloadSpeedMult] = BaseReloadSpeedMult + (dex * 0.05f) + (per * 0.02f);

        // WeaponSwapSpeed: Base + DEX bonus
        _derivedStats[StatType.WeaponSwapSpeed] = BaseWeaponSwapSpeed + (dex * 0.03f);

        // RecoilControl: 0.0f + ((STR - 10) * 0.05f)
        _derivedStats[StatType.RecoilControl] = BaseRecoilControl + (str * 0.05f);

        // AimStability: Base + PER bonus
        _derivedStats[StatType.AimStability] = BaseAimStability + (per * 0.05f);

        // RangeMultiplier: Base + PER bonus
        _derivedStats[StatType.RangeMultiplier] = BaseRangeMultiplier + (per * 0.01f);

        // --- Combat - Melee ---
        // MeleeDamageMult: Base + STR bonus
        _derivedStats[StatType.MeleeDamageMult] = BaseMeleeDamageMult + (str * 0.05f);

        // MeleeAttackSpeed: Base + DEX bonus
        _derivedStats[StatType.MeleeAttackSpeed] = BaseMeleeAttackSpeed + (dex * 0.03f);

        // KickCost: Base - STR reduction (lower is better)
        _derivedStats[StatType.KickCost] = Mathf.Max(5f, BaseKickCost - (str * 1f));

        // KnockbackForceMult: Base + STR bonus
        _derivedStats[StatType.KnockbackForceMult] = BaseKnockbackForceMult + (str * 0.05f);

        // --- Senses ---
        // ViewDistance: Base + PER bonus
        _derivedStats[StatType.ViewDistance] = BaseViewDistance + (per * 20f);

        // FootstepHearingRange: Base + PER bonus
        _derivedStats[StatType.FootstepHearingRange] = BaseFootstepHearingRange + (per * 10f);

        // DarknessVision: Base + PER + INTU bonus
        _derivedStats[StatType.DarknessVision] = BaseDarknessVision + (per * 0.05f) + (intu * 0.03f);

        // --- Meta ---
        // CraftingSpeed: Base + INT bonus
        _derivedStats[StatType.CraftingSpeed] = BaseCraftingSpeed + (intl * 0.05f);

        // ScrapEfficiency: Base + INT bonus
        _derivedStats[StatType.ScrapEfficiency] = BaseScrapEfficiency + (intl * 0.03f);

        // CritChance: Base + INTU bonus
        _derivedStats[StatType.CritChance] = BaseCritChance + (intu * 0.01f);

        // LootQuality: Base + INTU bonus
        _derivedStats[StatType.LootQuality] = BaseLootQuality + (intu * 0.5f);

        // Apply feat modifiers
        ApplyFeatModifiers();

        // Apply equipment modifiers
        ApplyEquipmentModifiers();

        // Emit signal for listeners
        EmitSignal(SignalName.StatsRecalculated);

        GD.Print($"[PlayerStatsManager] Stats recalculated. MoveSpeed: {MoveSpeed:F1}, MaxHealth: {MaxHealth:F0}, TurnSpeed: {TurnSpeed:F2}");
    }

    /// <summary>
    /// Apply all active feat modifiers to derived stats.
    /// </summary>
    private void ApplyFeatModifiers()
    {
        foreach (FeatDefinition feat in _activeFeats)
        {
            foreach (KeyValuePair<StatType, float> modifier in feat.StatModifiers)
            {
                if (_derivedStats.ContainsKey(modifier.Key))
                {
                    _derivedStats[modifier.Key] += modifier.Value;
                }
            }
        }
    }

    /// <summary>
    /// Apply all equipment modifiers to derived stats.
    /// </summary>
    private void ApplyEquipmentModifiers()
    {
        foreach (KeyValuePair<StatType, float> modifier in _equipmentModifiers)
        {
            if (_derivedStats.ContainsKey(modifier.Key))
            {
                _derivedStats[modifier.Key] += modifier.Value;
            }
        }
    }

    #endregion

    #region Debug

    /// <summary>
    /// Print all current stats to the console for debugging.
    /// </summary>
    public void DebugPrintAllStats()
    {
        GD.Print("=== PlayerStatsManager Debug ===");
        GD.Print("--- Attributes ---");
        foreach (AttributeType attr in System.Enum.GetValues<AttributeType>())
        {
            GD.Print($"  {attr}: {GetAttribute(attr)}");
        }

        GD.Print("--- Derived Stats ---");
        foreach (StatType stat in System.Enum.GetValues<StatType>())
        {
            GD.Print($"  {stat}: {GetStat(stat):F2}");
        }

        GD.Print($"--- Active Feats ({_activeFeats.Count}) ---");
        foreach (FeatDefinition feat in _activeFeats)
        {
            GD.Print($"  {feat.DisplayName} ({feat.Id})");
        }

        GD.Print("================================");
    }

    #endregion
}
