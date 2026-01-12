namespace DAIgame.Combat;

using DAIgame.AI;
using DAIgame.Core;
using Godot;

/// <summary>
/// Projectile that moves in a straight line and applies damage on collision.
/// </summary>
public partial class Projectile : Area2D
{
    /// <summary>
    /// Speed of the projectile in pixels per second.
    /// </summary>
    [Export]
    public float Speed { get; set; } = 800f;

    /// <summary>
    /// Damage dealt on hit.
    /// </summary>
    [Export]
    public float Damage { get; set; } = 25f;

    /// <summary>
    /// Knockback force applied to enemies on hit.
    /// </summary>
    [Export]
    public float Knockback { get; set; } = 0f;

    /// <summary>
    /// Maximum lifetime in seconds before the bullet despawns.
    /// </summary>
    [Export]
    public float Lifetime { get; set; } = 2f;

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

    private Vector2 _direction = Vector2.Right;
    private float _lifeTimer;
    private bool _hasHit;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        _lifeTimer = Lifetime;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hasHit)
        {
            return;
        }

        var dt = (float)delta;
        var moveDistance = Speed * dt;

        // Use raycast to check for collision along the path
        var space = GetWorld2D().DirectSpaceState;
        var startPos = GlobalPosition;
        var endPos = startPos + (_direction * moveDistance);

        GD.Print($"Bullet moving: from {startPos} to {endPos}, distance={moveDistance}, direction={_direction}");

        var query = PhysicsRayQueryParameters2D.Create(startPos, endPos);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.CollisionMask = CollisionMask;
        query.Exclude = [GetRid()];

        var result = space.IntersectRay(query);

        if (result.Count > 0)
        {
            // Hit something along the path
            var hitPosition = (Vector2)result["position"];
            var hitNormal = (Vector2)result["normal"];
            var collider = result["collider"].As<GodotObject>();

            GD.Print($"Bullet raycast hit: {(collider as Node)?.Name ?? "Unknown"} at {hitPosition}, distance={(hitPosition - startPos).Length()}");

            GlobalPosition = hitPosition;
            HandleCollision(collider as Node2D, hitPosition, hitNormal);
        }
        else
        {
            // No collision, move normally
            GD.Print($"Bullet no collision, moving to {endPos}");
            GlobalPosition = endPos;
        }

        _lifeTimer -= dt;
        if (_lifeTimer <= 0f)
        {
            QueueFree();
        }
    }

    /// <summary>
    /// Initializes the projectile with direction and rotation.
    /// </summary>
    public void Initialize(Vector2 direction)
    {
        _direction = direction.Normalized();
        Rotation = _direction.Angle();
        GD.Print($"Bullet fired: Position={GlobalPosition}, Direction={_direction}, Speed={Speed}");
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_hasHit)
        {
            return;
        }

        // Fallback for Area2D collision (should rarely trigger now that we use raycast)
        GD.Print($"Bullet Area2D collision fallback: {body.Name} at {GlobalPosition}");
        var hitPosition = GlobalPosition;
        var hitNormal = -_direction;
        HandleCollision(body, hitPosition, hitNormal);
    }

    private void HandleCollision(Node2D? body, Vector2 hitPosition, Vector2 hitNormal)
    {
        if (_hasHit || body == null)
        {
            return;
        }

        _hasHit = true;
        GD.Print($"Bullet hit: {body.Name} at {hitPosition}");

        // Check if the body or its parent is damageable
        var damageable = FindDamageableTarget(body);
        if (damageable is not null)
        {
            SpawnBloodParticles(hitPosition, hitNormal);
            damageable.ApplyDamage(Damage, GlobalPosition - (_direction * 10f), hitPosition, hitNormal);

            // Apply knockback to zombies
            if (Knockback > 0f)
            {
                var zombie = FindZombieTarget(body);
                if (zombie is not null)
                {
                    zombie.ApplyExternalKnockback(_direction, Knockback);
                }
            }
        }
        else
        {
            SpawnMissParticles(hitPosition);
        }

        QueueFree();
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
        particles.Rotation = normal.Angle();
        particles.Emitting = true;
        particles.Finished += particles.QueueFree;
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
        // Rotate so the particles' -Y axis points opposite the bullet's direction
        var missRotation = _direction.Angle() + (Mathf.Pi / 2f) + Mathf.Pi;
        GD.Print($"MissParticles: direction={_direction}, rotation={missRotation}");
        particles.Rotation = missRotation;
        particles.Emitting = true;
        particles.Finished += particles.QueueFree;
    }

    /// <summary>
    /// Finds the nearest damageable target by walking up the node hierarchy.
    /// </summary>
    private static IDamageable? FindDamageableTarget(Node node)
    {
        if (node is IDamageable damageable)
        {
            return damageable;
        }

        var parent = node.GetParent();
        while (parent is not null)
        {
            if (parent is IDamageable parentDamageable)
            {
                return parentDamageable;
            }
            parent = parent.GetParent();
        }

        return null;
    }

    /// <summary>
    /// Finds the nearest zombie controller by walking up the node hierarchy.
    /// </summary>
    private static ZombieController? FindZombieTarget(Node node)
    {
        if (node is ZombieController zombie)
        {
            return zombie;
        }

        var parent = node.GetParent();
        while (parent is not null)
        {
            if (parent is ZombieController parentZombie)
            {
                return parentZombie;
            }
            parent = parent.GetParent();
        }

        return null;
    }
}
