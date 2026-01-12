namespace DAIgame.Core;

using Godot;

/// <summary>
/// Interface for entities that can receive external knockback forces.
/// Implement this on any node that should be pushed by attacks, explosions, etc.
/// </summary>
/// <remarks>
/// This interface separates knockback behavior from specific entity types,
/// allowing weapons and effects to apply knockback without knowing the target type.
/// </remarks>
public interface IKnockbackable
{
    /// <summary>
    /// Applies external knockback force to the entity.
    /// </summary>
    /// <param name="direction">Direction of the knockback (should be normalized).</param>
    /// <param name="strength">Strength of the knockback force in pixels per second.</param>
    void ApplyKnockback(Vector2 direction, float strength);
}
