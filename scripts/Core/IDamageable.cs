namespace DAIgame.Core;

using Godot;

/// <summary>
/// Interface for entities that can receive damage.
/// Implement this on any node that should be damageable (player, enemies, destructibles).
/// </summary>
/// <remarks>
/// Entities implementing this interface should also add themselves to the "damageable" group
/// in their _Ready() method using AddToGroup("damageable").
/// </remarks>
public interface IDamageable
{
    /// <summary>
    /// Applies damage to the entity.
    /// </summary>
    /// <param name="amount">Amount of damage to apply.</param>
    /// <param name="fromPos">World position where the damage originated (e.g., attacker position).</param>
    /// <param name="hitPos">World position where the hit occurred.</param>
    /// <param name="hitNormal">Surface normal at the hit point (for effects like blood spray direction).</param>
    void ApplyDamage(float amount, Vector2 fromPos, Vector2 hitPos, Vector2 hitNormal);
}
