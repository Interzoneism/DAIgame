namespace DAIgame.Core;

using Godot;

/// <summary>
/// Data-driven resource that defines stats for entities (players, enemies, NPCs).
/// Use this to configure health, knockback, and visual feedback properties
/// without modifying code. Each entity type can have its own stats resource.
/// </summary>
/// <remarks>
/// To create a new entity type:
/// 1. Create a new .tres file in data/entities/
/// 2. Set resource type to EntityStats
/// 3. Configure values for your entity
/// 4. Assign to your entity's Stats property in the inspector
/// </remarks>
[GlobalClass]
public partial class EntityStats : Resource
{
    #region Health

    /// <summary>
    /// Maximum health of the entity.
    /// </summary>
    [ExportGroup("Health")]
    [Export]
    public float MaxHealth { get; set; } = 100f;

    /// <summary>
    /// Whether the entity can be healed.
    /// </summary>
    [Export]
    public bool CanHeal { get; set; } = true;

    #endregion

    #region Knockback

    /// <summary>
    /// Knockback force applied to this entity when hit.
    /// Higher values = pushed further by attacks.
    /// </summary>
    [ExportGroup("Knockback")]
    [Export]
    public float KnockbackStrength { get; set; } = 150f;

    /// <summary>
    /// Rate at which knockback velocity is reduced per second.
    /// Higher values = quicker recovery from knockback.
    /// </summary>
    [Export]
    public float KnockbackDamp { get; set; } = 400f;

    #endregion

    #region Visual Feedback

    /// <summary>
    /// Duration of the hit flash effect in seconds.
    /// </summary>
    [ExportGroup("Visual Feedback")]
    [Export]
    public float HitFlashDuration { get; set; } = 0.1f;

    /// <summary>
    /// Color to flash when hit.
    /// </summary>
    [Export]
    public Color HitFlashColor { get; set; } = new Color(2f, 0.5f, 0.5f, 1f);

    #endregion

    #region Death Effects

    /// <summary>
    /// Scene to spawn when entity dies (corpse, explosion, etc.).
    /// </summary>
    [ExportGroup("Death Effects")]
    [Export]
    public PackedScene? DeathEffectScene { get; set; }

    /// <summary>
    /// Scene to spawn for blood/debris effect on death.
    /// </summary>
    [Export]
    public PackedScene? DeathParticleScene { get; set; }

    #endregion

    /// <summary>
    /// Creates a deep copy of this stats resource.
    /// Useful for per-instance modifications without affecting the template.
    /// </summary>
    public EntityStats Clone()
    {
        return new EntityStats
        {
            MaxHealth = MaxHealth,
            CanHeal = CanHeal,
            KnockbackStrength = KnockbackStrength,
            KnockbackDamp = KnockbackDamp,
            HitFlashDuration = HitFlashDuration,
            HitFlashColor = HitFlashColor,
            DeathEffectScene = DeathEffectScene,
            DeathParticleScene = DeathParticleScene
        };
    }
}
