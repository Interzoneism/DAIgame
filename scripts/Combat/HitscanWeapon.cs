namespace DAIgame.Combat;

using DAIgame.Core;
using Godot;

/// <summary>
/// Hitscan weapon that performs instant raycast-based shooting.
/// Single shot with configurable range and damage.
/// </summary>
public partial class HitscanWeapon : Node2D
{
    /// <summary>
    /// Maximum range of the hitscan weapon in pixels.
    /// </summary>
    [Export]
    public float Range { get; set; } = 400f;

    /// <summary>
    /// Damage dealt per shot.
    /// </summary>
    [Export]
    public float Damage { get; set; } = 25f;

    /// <summary>
    /// Scene for grey miss particles.
    /// </summary>
    [Export]
    public PackedScene? MissParticlesScene { get; set; }

    /// <summary>
    /// Scene for red blood particles.
    /// </summary>
    [Export]
    public PackedScene? BloodParticlesScene { get; set; }

    /// <summary>
    /// Fires a hitscan shot in the given direction from the given origin.
    /// </summary>
    public void Fire(Vector2 origin, Vector2 direction)
    {
        var space = GetWorld2D().DirectSpaceState;
        var endPoint = origin + direction * Range;

        var query = PhysicsRayQueryParameters2D.Create(origin, endPoint);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = space.IntersectRay(query);

        if (result.Count > 0)
        {
            HandleHit(result);
        }
        else
        {
            HandleMiss(endPoint);
        }
    }

    private void HandleHit(Godot.Collections.Dictionary result)
    {
        var hitPosition = (Vector2)result["position"];
        var hitNormal = (Vector2)result["normal"];
        var collider = result["collider"].As<GodotObject>();

        // Spawn blood particles
        SpawnBloodParticles(hitPosition, hitNormal);

        // Check if the collider or its parent is damageable
        if (collider is Node node)
        {
            var damageable = FindDamageableNode(node);
            if (damageable is IDamageable damageableTarget)
            {
                var fromPos = GlobalPosition;
                damageableTarget.ApplyDamage(Damage, fromPos, hitPosition, hitNormal);
            }
        }
    }

    private void HandleMiss(Vector2 missPosition)
    {
        SpawnMissParticles(missPosition);
    }

    private void SpawnBloodParticles(Vector2 position, Vector2 normal)
    {
        if (BloodParticlesScene is null)
        {
            return;
        }

        var particles = BloodParticlesScene.Instantiate<GpuParticles2D>();
        GetTree().Root.AddChild(particles);
        particles.GlobalPosition = position;

        // Rotate particles to match hit normal (spray away from surface)
        particles.Rotation = normal.Angle();

        particles.Emitting = true;
        particles.Finished += () => particles.QueueFree();
    }

    private void SpawnMissParticles(Vector2 position)
    {
        if (MissParticlesScene is null)
        {
            return;
        }

        var particles = MissParticlesScene.Instantiate<GpuParticles2D>();
        GetTree().Root.AddChild(particles);
        particles.GlobalPosition = position;
        particles.Emitting = true;
        particles.Finished += () => particles.QueueFree();
    }

    private static Node? FindDamageableNode(Node node)
    {
        // Check if the node itself is damageable (implements IDamageable)
        if (node is IDamageable)
        {
            return node;
        }

        // Check parent nodes for IDamageable implementation
        var parent = node.GetParent();
        while (parent is not null)
        {
            if (parent is IDamageable)
            {
                return parent;
            }
            parent = parent.GetParent();
        }

        return null;
    }
}
