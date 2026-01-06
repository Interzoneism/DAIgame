namespace DAIgame.AI;
using DAIgame.Combat;
using DAIgame.Core;
using Godot;

/// <summary>
/// Simple zombie AI that idles, chases the player when within detection range,
/// and attacks when in melee range. Uses NavigationAgent2D for pathfinding.
/// </summary>
public partial class ZombieController : CharacterBody2D, IDamageable
{
    /// <summary>
    /// Zombie movement speed in pixels per second.
    /// </summary>
    [Export]
    public float MoveSpeed { get; set; } = 100f;

    /// <summary>
    /// How often to recalculate path (in seconds). Lower = more responsive but more CPU.
    /// </summary>
    [Export]
    public float PathUpdateInterval { get; set; } = 0.25f;

    /// <summary>
    /// Distance at which the zombie starts chasing the player.
    /// </summary>
    [Export]
    public float DetectionRange { get; set; } = 200f;

    /// <summary>
    /// Distance at which the zombie can attack the player.
    /// </summary>
    [Export]
    public float AttackRange { get; set; } = 30f;

    /// <summary>
    /// Time between attacks in seconds.
    /// </summary>
    [Export]
    public float AttackCooldown { get; set; } = 1.5f;

    /// <summary>
    /// Damage dealt to player per attack.
    /// </summary>
    [Export]
    public float AttackDamage { get; set; } = 10f;

    /// <summary>
    /// Maximum health of the zombie.
    /// </summary>
    [Export]
    public float MaxHealth { get; set; } = 50f;

    /// <summary>
    /// Knockback force applied when hit.
    /// </summary>
    [Export]
    public float KnockbackStrength { get; set; } = 150f;

    /// <summary>
    /// Rate at which knockback is damped per second.
    /// </summary>
    [Export]
    public float KnockbackDamp { get; set; } = 400f;

    /// <summary>
    /// Duration of the hit flash effect in seconds.
    /// </summary>
    [Export]
    public float HitFlashDuration { get; set; } = 0.1f;

    /// <summary>
    /// Distance to check for walls with raycasts for avoidance steering.
    /// </summary>
    [Export]
    public float WallAvoidanceDistance { get; set; } = 15f;

    /// <summary>
    /// Strength of steering force to push away from walls.
    /// </summary>
    [Export]
    public float WallAvoidanceStrength { get; set; } = 60f;

    private AnimatedSprite2D? _sprite;
    private NavigationAgent2D? _navAgent;
    private Node2D? _player;
    private float _attackCooldownTimer;
    private float _currentHealth;
    private ZombieState _state = ZombieState.Idle;
    private bool _hasSeenPlayer;
    private Vector2 _knockbackVelocity = Vector2.Zero;
    private float _hitFlashTimer;
    private Vector2 _lastDamageDirection = Vector2.Down;
    private float _pathUpdateTimer;
    private static PackedScene? s_corpseScene;
    private static PackedScene? s_bloodSpatterScene;

    // Raycast directions for wall avoidance (8 directions)
    private static readonly Vector2[] s_avoidanceDirections =
    [
        Vector2.Right,
        Vector2.Right.Rotated(Mathf.Pi / 4f),
        Vector2.Up,
        Vector2.Up.Rotated(Mathf.Pi / 4f),
        Vector2.Left,
        Vector2.Left.Rotated(Mathf.Pi / 4f),
        Vector2.Down,
        Vector2.Down.Rotated(Mathf.Pi / 4f)
    ];

    private enum ZombieState
    {
        Idle,
        Chasing,
        Attacking
    }

    public override void _Ready()
    {
        AddToGroup("enemies");
        AddToGroup("damageable");

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _navAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
        _currentHealth = MaxHealth;

        if (_sprite is not null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            _sprite.Play("idle");
        }

        s_corpseScene ??= GD.Load<PackedScene>("res://scripts/Combat/ZombieCorpse.tscn");

        s_bloodSpatterScene ??= GD.Load<PackedScene>("res://scenes/effects/BloodSpatter.tscn");
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdatePlayerReference();

        if (_player is null || !IsInstanceValid(_player))
        {
            SetState(ZombieState.Idle);
            return;
        }

        var distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);

        // Once player is detected, zombie remembers them forever (persistent aggro)
        if (!_hasSeenPlayer && distanceToPlayer <= DetectionRange)
        {
            _hasSeenPlayer = true;
        }

        // State transitions - keep chasing once player has been seen
        if (_hasSeenPlayer)
        {
            if (distanceToPlayer <= AttackRange)
            {
                SetState(ZombieState.Attacking);
            }
            else
            {
                SetState(ZombieState.Chasing);
            }
        }
        else
        {
            SetState(ZombieState.Idle);
        }

        // State behavior
        var deltaF = (float)delta;
        switch (_state)
        {
            case ZombieState.Idle:
                HandleIdle(deltaF);
                break;
            case ZombieState.Chasing:
                HandleChasing(deltaF);
                break;
            case ZombieState.Attacking:
                HandleAttacking(deltaF);
                break;
        }
    }

    private void HandleIdle(float delta)
    {
        ApplyKnockbackDamp(delta);
        UpdateHitFlash(delta);
        Velocity = _knockbackVelocity;
        MoveAndSlide();
    }

    private void HandleChasing(float delta)
    {
        ApplyKnockbackDamp(delta);
        UpdateHitFlash(delta);

        if (_player is null)
        {
            Velocity = _knockbackVelocity;
            MoveAndSlide();
            return;
        }

        Vector2 direction;

        // Use navigation agent if available, otherwise direct movement
        if (_navAgent is not null)
        {
            // Periodically update path target for performance
            _pathUpdateTimer -= delta;
            if (_pathUpdateTimer <= 0f)
            {
                _navAgent.TargetPosition = _player.GlobalPosition;
                _pathUpdateTimer = PathUpdateInterval;
            }

            if (!_navAgent.IsNavigationFinished())
            {
                var nextPos = _navAgent.GetNextPathPosition();
                direction = (nextPos - GlobalPosition).Normalized();
            }
            else
            {
                direction = (_player.GlobalPosition - GlobalPosition).Normalized();
            }
        }
        else
        {
            // Fallback to direct movement if no nav agent
            direction = (_player.GlobalPosition - GlobalPosition).Normalized();
        }

        // Calculate wall avoidance steering
        var avoidance = CalculateWallAvoidance();

        // Combine navigation direction with wall avoidance
        var finalVelocity = (direction * MoveSpeed) + avoidance + _knockbackVelocity;

        // If avoidance is being applied, use avoidance velocity from NavigationAgent if enabled
        if (_navAgent is not null && _navAgent.AvoidanceEnabled)
        {
            _navAgent.Velocity = finalVelocity;
            // The navigation agent will call back with a safe velocity, but for immediate response
            // we use our calculated velocity directly
        }

        Velocity = finalVelocity;
        MoveAndSlide();

        // Face movement direction
        if (direction != Vector2.Zero)
        {
            Rotation = direction.Angle();
        }
    }

    private void HandleAttacking(float delta)
    {
        ApplyKnockbackDamp(delta);
        UpdateHitFlash(delta);

        // Stop moving during attack (but allow knockback)
        Velocity = _knockbackVelocity;
        MoveAndSlide();

        if (_player is null)
        {
            return;
        }

        // Face the player
        var direction = _player.GlobalPosition - GlobalPosition;
        if (direction != Vector2.Zero)
        {
            Rotation = direction.Angle();
        }

        // Handle attack cooldown
        if (_attackCooldownTimer > 0f)
        {
            _attackCooldownTimer -= delta;
        }
        else
        {
            PerformAttack();
            _attackCooldownTimer = AttackCooldown;
        }
    }

    private void PerformAttack()
    {
        if (_player is null)
        {
            return;
        }

        // Try to damage the player through the interface
        if (_player is IDamageable damageable)
        {
            var hitPos = _player.GlobalPosition;
            var fromPos = GlobalPosition;
            var hitNormal = (fromPos - hitPos).Normalized();

            damageable.ApplyDamage(AttackDamage, fromPos, hitPos, hitNormal);
        }
    }

    /// <summary>
    /// Casts rays in multiple directions to detect nearby walls and returns a steering
    /// vector that pushes away from obstacles. This prevents zombies from getting stuck
    /// on wall corners and edges.
    /// </summary>
    private Vector2 CalculateWallAvoidance()
    {
        var spaceState = GetWorld2D().DirectSpaceState;
        if (spaceState is null)
        {
            return Vector2.Zero;
        }

        var avoidance = Vector2.Zero;
        var selfRid = GetRid();

        foreach (var dir in s_avoidanceDirections)
        {
            var query = PhysicsRayQueryParameters2D.Create(
                GlobalPosition,
                GlobalPosition + (dir * WallAvoidanceDistance),
                1 // Collision layer 1 = walls
            );
            query.Exclude = [selfRid];

            var result = spaceState.IntersectRay(query);
            if (result.Count > 0)
            {
                // Calculate how close the wall is (0 = at max distance, 1 = touching)
                var hitPos = (Vector2)result["position"];
                var distance = GlobalPosition.DistanceTo(hitPos);
                var proximity = 1f - (distance / WallAvoidanceDistance);

                // Push away from the wall, stronger when closer
                avoidance -= dir * proximity * WallAvoidanceStrength;
            }
        }

        return avoidance;
    }

    private void SetState(ZombieState newState)
    {
        if (_state == newState)
        {
            return;
        }

        _state = newState;

        // Update animation based on state
        if (_sprite is null)
        {
            return;
        }

        switch (_state)
        {
            case ZombieState.Idle:
                _sprite.Play("idle");
                break;
            case ZombieState.Chasing:
                _sprite.Play("walk");
                break;
            case ZombieState.Attacking:
                // Only start attack animation if not already playing
                if (_sprite.Animation != "attack")
                {
                    _sprite.Play("attack");
                }
                break;
        }
    }

    private void OnAnimationFinished()
    {
        if (_sprite is null)
        {
            return;
        }

        // Loop attack animation while in attacking state
        if (_state == ZombieState.Attacking && _sprite.Animation == "attack")
        {
            _sprite.Play("attack");
        }
    }

    private void UpdatePlayerReference()
    {
        if (_player is not null && IsInstanceValid(_player))
        {
            return;
        }

        var players = GetTree().GetNodesInGroup("player");
        _player = players.Count > 0 ? players[0] as Node2D : null;
    }

    /// <summary>
    /// Damage interface implementation.
    /// </summary>
    public void ApplyDamage(float amount, Vector2 fromPos, Vector2 hitPos, Vector2 hitNormal)
    {
        var hitDirection = (hitPos - fromPos).Normalized();
        if (hitDirection != Vector2.Zero)
        {
            _lastDamageDirection = hitDirection;
        }

        _currentHealth -= amount;

        // Apply knockback away from damage source
        var knockbackDir = (GlobalPosition - fromPos).Normalized();
        if (knockbackDir == Vector2.Zero)
        {
            knockbackDir = Vector2.Right;
        }
        _knockbackVelocity = knockbackDir * KnockbackStrength;

        // Trigger hit flash
        _hitFlashTimer = HitFlashDuration;
        ApplyHitFlash();

        // Aggro on taking damage
        _hasSeenPlayer = true;

        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    private void ApplyKnockbackDamp(float delta)
    {
        if (_knockbackVelocity == Vector2.Zero)
        {
            return;
        }

        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDamp * delta);
    }

    private void ApplyHitFlash()
    {
        if (_sprite is null)
        {
            return;
        }

        // White flash overlay effect
        _sprite.Modulate = new Color(2f, 2f, 2f, 1f);
    }

    private void UpdateHitFlash(float delta)
    {
        if (_hitFlashTimer <= 0f)
        {
            return;
        }

        _hitFlashTimer -= delta;

        if (_hitFlashTimer <= 0f && _sprite is not null)
        {
            _sprite.Modulate = Colors.White;
        }
    }

    private void Die()
    {
        SpawnBloodSpatter();
        SpawnCorpse();
        QueueFree();
    }

    private void SpawnCorpse()
    {
        if (s_corpseScene is null)
        {
            return;
        }

        if (s_corpseScene.Instantiate<Node2D>() is not Node2D corpse)
        {
            return;
        }

        corpse.GlobalPosition = GlobalPosition;
        corpse.GlobalRotation = GlobalRotation;
        GetTree().Root.AddChild(corpse);
    }

    private void SpawnBloodSpatter()
    {
        if (s_bloodSpatterScene is null)
        {
            return;
        }

        if (s_bloodSpatterScene.Instantiate() is not BloodSpatter spatter)
        {
            return;
        }

        spatter.GlobalPosition = GlobalPosition;
        var sprayDirection = _lastDamageDirection;
        if (sprayDirection == Vector2.Zero)
        {
            sprayDirection = Vector2.Down;
        }

        spatter.SprayDirection = sprayDirection;
        GetTree().Root.AddChild(spatter);
    }
}
