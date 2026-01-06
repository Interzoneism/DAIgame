namespace DAIgame.Core;

using Godot;

/// <summary>
/// Interface for entities that can receive damage.
/// Implement this on any Node that should be damageable.
/// </summary>
public interface IDamageable
{
  /// <summary>
  /// Apply damage to this entity.
  /// </summary>
  /// <param name="amount">Amount of damage to apply.</param>
  /// <param name="fromPos">Position the damage came from (e.g., shooter position).</param>
  /// <param name="hitPos">Position where the hit occurred.</param>
  /// <param name="hitNormal">Normal vector at the hit point.</param>
  void ApplyDamage(float amount, Vector2 fromPos, Vector2 hitPos, Vector2 hitNormal);
}
