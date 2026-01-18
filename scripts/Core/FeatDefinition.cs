namespace DAIgame.Core;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Resource that defines a Feat/Perk that can modify player stats.
/// Create instances via .tres files in data/feats/.
/// </summary>
[GlobalClass]
public partial class FeatDefinition : Resource
{
    /// <summary>
    /// Unique identifier for this feat. Used for save/load and lookup.
    /// </summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = "Unknown Feat";

    /// <summary>
    /// Description of what this feat does.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Icon for the feat in UI.
    /// </summary>
    [Export]
    public Texture2D? Icon { get; set; }

    /// <summary>
    /// Stat modifiers this feat provides.
    /// These are ADDED to the final stat value after attribute calculations.
    /// For multipliers (like ReloadSpeedMult), add 0.2 to get +20%.
    /// </summary>
    /// <remarks>
    /// Note: Godot's Resource export system doesn't natively support Dictionary with enums.
    /// Use the helper arrays below for Inspector editing, or set via code.
    /// </remarks>
    public Dictionary<StatType, float> StatModifiers { get; set; } = new();

    #region Inspector-Friendly Modifier Setup

    /// <summary>
    /// Stats to modify as int values (parallel array with ModifierValues).
    /// Cast to StatType enum when using. Godot doesn't support enum array exports.
    /// </summary>
    [ExportGroup("Stat Modifiers")]
    [Export]
    public int[] ModifierStatIndices { get; set; } = [];

    /// <summary>
    /// Values for each stat modifier (parallel array with ModifierStatIndices).
    /// </summary>
    [Export]
    public float[] ModifierValues { get; set; } = [];

    /// <summary>
    /// Call this after loading to populate the StatModifiers dictionary
    /// from the inspector-friendly parallel arrays.
    /// </summary>
    public void BuildModifierDictionary()
    {
        StatModifiers.Clear();
        var count = Mathf.Min(ModifierStatIndices.Length, ModifierValues.Length);
        for (var i = 0; i < count; i++)
        {
            var statType = (StatType)ModifierStatIndices[i];
            StatModifiers[statType] = ModifierValues[i];
        }
    }

    #endregion

    #region Prerequisite System (for future use)

    /// <summary>
    /// Feat IDs that must be acquired before this feat can be taken.
    /// </summary>
    [ExportGroup("Prerequisites")]
    [Export]
    public string[] RequiredFeatIds { get; set; } = [];

    /// <summary>
    /// Minimum attribute requirements as int values (e.g., need 12 STR).
    /// Cast to AttributeType enum when using. Godot doesn't support enum array exports.
    /// </summary>
    [Export]
    public int[] RequiredAttributeIndices { get; set; } = [];

    /// <summary>
    /// Minimum values for required attributes (parallel array with RequiredAttributeIndices).
    /// </summary>
    [Export]
    public int[] RequiredAttributeValues { get; set; } = [];

    #endregion
}
