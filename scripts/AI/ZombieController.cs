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
	/// Delay between starting the attack animation and applying damage.
	/// </summary>
	[Export]
	public float AttackWindupDelay { get; set; } = 0.5f;

	/// <summary>
	/// Damage dealt to player per attack.
	/// </summary>
	[Export]
	public float AttackDamage { get; set; } = 10f;

	/// <summary>
	/// Minimum distance to keep from the player to avoid body overlap.
	/// </summary>
	[Export]
	public float MinPlayerSeparation { get; set; } = 14f;

	/// <summary>
	/// Push strength used to separate from the player when too close.
	/// </summary>
	[Export]
	public float SeparationPushStrength { get; set; } = 80f;

	/// <summary>
	/// Extra range beyond MinPlayerSeparation where player pushing can influence zombies.
	/// </summary>
	[Export]
	public float PlayerPushRange { get; set; } = 6f;

	/// <summary>
	/// Scale of the player's push applied to zombies when walking into them.
	/// </summary>
	[Export]
	public float PlayerPushFactor { get; set; } = 0.35f;

	/// <summary>
	/// Clamp on how fast the player's push can move zombies.
	/// </summary>
	[Export]
	public float MaxPlayerPushSpeed { get; set; } = 60f;

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

	/// <summary>
	/// Duration of knockdown state in seconds.
	/// </summary>
	[Export]
	public float KnockdownDuration { get; set; } = 2f;

	/// <summary>
	/// Maximum rotation angle (in degrees) applied to body on impact.
	/// </summary>
	[Export]
	public float ImpactRotationDegrees { get; set; } = 30f;

	/// <summary>
	/// Duration of impact rotation effect in seconds.
	/// </summary>
	[Export]
	public float ImpactRotationDuration { get; set; } = 0.3f;

	private Node2D? _bodyNode;
	private AnimatedSprite2D? _sprite;
	private NavigationAgent2D? _navAgent;
	private Node2D? _player;
	private float _attackCooldownTimer;
	private float _attackWindupTimer;
	private bool _attackPending;
	private float _currentHealth;
	private ZombieState _state = ZombieState.Idle;
	private bool _isAttackAnimating;
	private bool _hasSeenPlayer;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private float _hitFlashTimer;
	private Vector2 _lastDamageDirection = Vector2.Down;
	private float _pathUpdateTimer;
	private bool _wasTooCloseToPlayer;
	private bool _wasPlayerPushing;
	private bool _isKnockedDown;
	private float _knockdownTimer;
	private bool _isDying;
	private Tween? _impactRotationTween;
	private static PackedScene? _corpseScene;
	private static PackedScene? _bloodSpatterScene;

	// Legs for separate leg animation (matches player behavior)
	private Node2D? _legsNode;
	private AnimatedSprite2D? _legsSprite;
	private Vector2 _lastMoveDir = Vector2.Down;

	// Raycast directions for wall avoidance (8 directions)
	private static readonly Vector2[] _avoidanceDirections =
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

		_bodyNode = GetNodeOrNull<Node2D>("Body");
		_sprite = _bodyNode?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_navAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
		_currentHealth = MaxHealth;

		if (_bodyNode is null)
		{
			GD.PrintErr("ZombieController._Ready: Body node not found - body rotation will not update");
		}

		if (_sprite is not null)
		{
			if (_sprite.SpriteFrames is not null)
			{
				if (_sprite.SpriteFrames.HasAnimation("attack"))
				{
					_sprite.SpriteFrames.SetAnimationLoop("attack", false);
				}
				else
				{
					GD.PrintErr("ZombieController._Ready: attack animation not found in SpriteFrames");
				}

				if (_sprite.SpriteFrames.HasAnimation("lean"))
				{
					_sprite.SpriteFrames.SetAnimationLoop("lean", false);
				}
				else
				{
					GD.PrintErr("ZombieController._Ready: lean animation not found in SpriteFrames");
				}

				if (_sprite.SpriteFrames.HasAnimation("death"))
				{
					_sprite.SpriteFrames.SetAnimationLoop("death", false);
				}
				else
				{
					GD.PrintErr("ZombieController._Ready: corpse animation not found in SpriteFrames");
				}
			}
			else
			{
				GD.PrintErr("ZombieController._Ready: SpriteFrames missing on AnimatedSprite2D");
			}

			_sprite.AnimationFinished += OnAnimationFinished;
			_sprite.Play("idle");
		}
		else
		{
			GD.PrintErr("ZombieController._Ready: Body/AnimatedSprite2D not found - animations will not play");
		}

		// Legs node: rotate & play/pause leg walk animations separately from body
		_legsNode = GetNodeOrNull<Node2D>("Legs");
		_legsSprite = _legsNode?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_legsSprite?.Play();
		if (_legsNode is null)
		{
			GD.PrintErr("ZombieController._Ready: Legs node not found - leg rotation will not update");
		}

		_corpseScene ??= GD.Load<PackedScene>("res://scripts/Combat/ZombieCorpse.tscn");

		_bloodSpatterScene ??= GD.Load<PackedScene>("res://scenes/effects/BloodSpatter.tscn");
	}

	public override void _PhysicsProcess(double delta)
	{
		// Handle dying state - play death animation, then spawn corpse
		if (_isDying)
		{
			HandleDying((float)delta);
			return;
		}

		// Handle knockdown state - locked and cannot move
		if (_isKnockedDown)
		{
			HandleKnockdown((float)delta);
			return;
		}

		UpdatePlayerReference();

		// Handle attack cooldown
		if (_attackCooldownTimer > 0f)
		{
			_attackCooldownTimer -= (float)delta;
		}

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
		if (_attackPending || _isAttackAnimating)
		{
			SetState(ZombieState.Attacking);
		}
		else if (_hasSeenPlayer)
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

		// Not moving while idle
		UpdateLegsFacing(Vector2.Zero, false);
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

		var separationVelocity = GetPlayerSeparationVelocity();
		if (separationVelocity != Vector2.Zero)
		{
			direction = Vector2.Zero;
		}

		// Combine navigation direction with wall avoidance
		var finalVelocity = (direction * MoveSpeed) + avoidance + separationVelocity + _knockbackVelocity;

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
		if (direction != Vector2.Zero && _bodyNode is not null)
		{
			_bodyNode.Rotation = direction.Angle();
		}

		// Update legs facing and animation based on movement
		if (direction != Vector2.Zero)
		{
			_lastMoveDir = direction;
			UpdateLegsFacing(direction, true);
		}
		else
		{
			UpdateLegsFacing(Vector2.Zero, false);
		}
	}

	private void HandleAttacking(float delta)
	{
		ApplyKnockbackDamp(delta);
		UpdateHitFlash(delta);

		// Stop moving during attack (but allow knockback)
		var separationVelocity = GetPlayerSeparationVelocity();
		Velocity = _knockbackVelocity + separationVelocity;
		MoveAndSlide();

		// Legs should pause while attacking
		UpdateLegsFacing(Vector2.Zero, false);

		if (_player is null)
		{
			_attackPending = false;
			_attackWindupTimer = 0f;
			return;
		}

		// Face the player
		var direction = _player.GlobalPosition - GlobalPosition;
		if (direction != Vector2.Zero && _bodyNode is not null)
		{
			_bodyNode.Rotation = direction.Angle();
		}

		if (_attackPending)
		{
			_attackWindupTimer -= delta;
			if (_attackWindupTimer <= 0f)
			{
				PerformAttack();
			}
			return;
		}

		if (_attackCooldownTimer <= 0f && !_isAttackAnimating)
		{
			StartAttackWindup();
			return;
		}

		if (!_isAttackAnimating)
		{
			SetBodyIdleIfPossible();
		}
	}

	private void StartAttackWindup()
	{
		if (_player is null)
		{
			return;
		}

		_attackPending = true;
		_attackWindupTimer = AttackWindupDelay;

		// Play attack animation from the start each time we attack
		if (_sprite is not null)
		{
			if (_sprite.SpriteFrames is not null && _sprite.SpriteFrames.HasAnimation("attack"))
			{
				_sprite.SpriteFrames.SetAnimationLoop("attack", false);
			}

			_isAttackAnimating = true;
			_sprite.Stop();
			_sprite.Play("attack");
			GD.Print("ZombieController: Playing attack animation");
		}
	}

	private void PerformAttack()
	{
		_attackPending = false;

		if (_player is null)
		{
			return;
		}

		var distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
		if (distanceToPlayer > AttackRange)
		{
			GD.Print("ZombieController: Attack windup finished but player is out of range");
			return;
		}

		// Try to damage the player through the interface
		if (_player is IDamageable damageable)
		{
			var hitPos = _player.GlobalPosition;
			var fromPos = GlobalPosition;
			var hitNormal = (fromPos - hitPos).Normalized();

			damageable.ApplyDamage(AttackDamage, fromPos, hitPos, hitNormal);
			_attackCooldownTimer = AttackCooldown;
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

		foreach (var dir in _avoidanceDirections)
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

		// Do not interrupt the attack animation mid-swing
		if (_isAttackAnimating)
		{
			GD.Print($"ZombieController: State changed to {_state} but attack animation is playing; will swap animations after it finishes");
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
				// Don't force the body animation here - attack animation is
				// started explicitly in PerformAttack() and resumed in
				// OnAnimationFinished(). This prevents overriding the attack
				// playback and avoids leaving the sprite paused on a single frame.
				break;
		}
	}

	private void OnAnimationFinished()
	{
		if (_sprite is null)
		{
			return;
		}

		// Handle death animation finishing
		if (_sprite.Animation == "death" && _isDying)
		{
			OnDeathAnimationFinished();
			return;
		}

		// Handle lean animation finishing (after knockdown recovery)
		if (_sprite.Animation == "lean")
		{
			// Return to appropriate state animation
			if (_state == ZombieState.Chasing)
			{
				_sprite.Play("walk");
				GD.Print("ZombieController: Lean finished, resuming walk");
			}
			else
			{
				_sprite.Play("idle");
				GD.Print("ZombieController: Lean finished, returning to idle");
			}
			return;
		}

		// If the attack animation finished, resume an appropriate animation
		if (_sprite.Animation == "attack")
		{
			_isAttackAnimating = false;

			if (_state == ZombieState.Chasing)
			{
				_sprite.Play("walk");
				GD.Print("ZombieController: Attack finished, resuming walk");
			}
			else
			{
				_sprite.Play("idle");
				GD.Print("ZombieController: Attack finished, returning to idle");
			}
		}
	}

	private void SetBodyIdleIfPossible()
	{
		if (_sprite is null || _isAttackAnimating)
		{
			return;
		}

		if (_sprite.Animation != "idle")
		{
			_sprite.Play("idle");
		}
	}

	private Vector2 GetPlayerSeparationVelocity()
	{
		if (_player is null)
		{
			_wasTooCloseToPlayer = false;
			return Vector2.Zero;
		}

		var toPlayer = _player.GlobalPosition - GlobalPosition;
		var distance = toPlayer.Length();

		if (distance <= 0.001f)
		{
			if (!_wasTooCloseToPlayer)
			{
				GD.Print("ZombieController: Overlapping player, applying separation push");
				_wasTooCloseToPlayer = true;
			}
			return Vector2.Right * SeparationPushStrength;
		}

		if (distance < MinPlayerSeparation)
		{
			if (!_wasTooCloseToPlayer)
			{
				GD.Print("ZombieController: Too close to player, applying separation push");
				_wasTooCloseToPlayer = true;
			}

			var away = -toPlayer / distance;
			var strength = (MinPlayerSeparation - distance) / MinPlayerSeparation;
			var separation = away * SeparationPushStrength * strength;
			return separation + GetPlayerPushVelocity(distance);
		}

		_wasTooCloseToPlayer = false;
		return GetPlayerPushVelocity(distance);
	}

	private Vector2 GetPlayerPushVelocity(float distanceToPlayer)
	{
		if (_player is not CharacterBody2D playerBody)
		{
			_wasPlayerPushing = false;
			return Vector2.Zero;
		}

		if (distanceToPlayer > MinPlayerSeparation + PlayerPushRange)
		{
			_wasPlayerPushing = false;
			return Vector2.Zero;
		}

		var toZombie = GlobalPosition - _player.GlobalPosition;
		if (toZombie.LengthSquared() <= 0.0001f)
		{
			_wasPlayerPushing = false;
			return Vector2.Zero;
		}

		var pushDir = toZombie.Normalized();
		var towardSpeed = Mathf.Max(0f, playerBody.Velocity.Dot(pushDir));
		if (towardSpeed <= 0.01f)
		{
			_wasPlayerPushing = false;
			return Vector2.Zero;
		}

		if (!_wasPlayerPushing)
		{
			GD.Print("ZombieController: Player pushing zombie, applying push");
			_wasPlayerPushing = true;
		}

		var pushSpeed = Mathf.Min(towardSpeed * PlayerPushFactor, MaxPlayerPushSpeed);
		return pushDir * pushSpeed;
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

		// Knockback is now applied externally via ApplyExternalKnockback
		// from weapons/projectiles, not from inherent zombie property

		// Trigger hit flash
		_hitFlashTimer = HitFlashDuration;
		ApplyHitFlash();

		// Aggro on taking damage
		_hasSeenPlayer = true;

		if (_currentHealth <= 0f)
		{
			Die();
		}
		else
		{
			// Non-lethal hit - apply impact rotation to body
			ApplyImpactRotation(hitPos);
		}
	}

	/// <summary>
	/// Applies external knockback force to the zombie (from melee hits, explosions, etc.).
	/// </summary>
	/// <param name="direction">Direction of the knockback (normalized).</param>
	/// <param name="strength">Strength of the knockback force.</param>
	public void ApplyExternalKnockback(Vector2 direction, float strength)
	{
		_knockbackVelocity += direction * strength;
		GD.Print($"ZombieController: Applied external knockback - dir: {direction}, strength: {strength}");
	}

	/// <summary>
	/// Applies knockdown state to the zombie.
	/// Zombie plays "lean" animation, stops at frame 3, and cannot move for knockdown duration.
	/// </summary>
	public void ApplyKnockdown()
	{
		if (_isKnockedDown)
		{
			return;
		}

		_isKnockedDown = true;
		_knockdownTimer = KnockdownDuration;
		_attackPending = false;
		_isAttackAnimating = false;

		if (_sprite is not null)
		{
			_sprite.Stop();
			_sprite.Play("lean");
		}

		// Stop legs
		if (_legsSprite is not null)
		{
			_legsSprite.Stop();
			_legsSprite.Frame = 0;
		}

		GD.Print("Zombie knocked down!");
	}

	private void HandleKnockdown(float delta)
	{
		ApplyKnockbackDamp(delta);
		UpdateHitFlash(delta);

		// Keep zombie still during knockdown
		Velocity = _knockbackVelocity;
		MoveAndSlide();

		// Stop sprite at frame 3 once reached
		if (_sprite is not null && _sprite.Animation == "lean" && _sprite.Frame >= 3)
		{
			_sprite.Stop();
			_sprite.Frame = 3;
		}

		_knockdownTimer -= delta;
		if (_knockdownTimer <= 0f)
		{
			EndKnockdown();
		}
	}

	private void EndKnockdown()
	{
		_isKnockedDown = false;

		// Resume lean animation to finish naturally
		_sprite?.Play("lean");

		GD.Print("Zombie knockdown ended, resuming");
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

		// White flash overlay effect for body
		_sprite.Modulate = new Color(2f, 2f, 2f, 1f);

		// Mirror the flash on the legs if present
		if (_legsSprite is not null)
		{
			_legsSprite.Modulate = new Color(2f, 2f, 2f, 1f);
		}
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
			if (_legsSprite is not null)
			{
				_legsSprite.Modulate = Colors.White;
			}
		}
	}

	/// <summary>
	/// Rotate the legs to face the movement direction (snapped to eight directions) and
	/// pause/play the walk animation depending on whether the zombie is moving.
	/// </summary>
	private void UpdateLegsFacing(Vector2 movementDir, bool isMoving)
	{
		if (_legsNode is null)
		{
			return;
		}

		var dir = movementDir.LengthSquared() > 0 ? movementDir : _lastMoveDir;
		var snappedAngle = SnapToEightDirections(dir.Angle());
		_legsNode.Rotation = snappedAngle;

		if (_legsSprite is not null)
		{
			// Flip horizontally when moving left
			_legsSprite.FlipH = dir.X < 0;

			if (!isMoving)
			{
				_legsSprite.Stop();
			}
			else
			{
				_legsSprite.Play();
			}
		}
	}

	private static float SnapToEightDirections(float angle)
	{
		var step = Mathf.Pi / 4f;
		return Mathf.Round(angle / step) * step;
	}

	/// <summary>
	/// Applies a brief rotation to the body towards the hit direction to simulate impact force.
	/// </summary>
	private void ApplyImpactRotation(Vector2 hitPos)
	{
		if (_bodyNode is null)
		{
			return;
		}

		// Cancel any existing impact rotation tween
		_impactRotationTween?.Kill();

		// Calculate which side was hit relative to the body's facing direction
		var toHit = hitPos - GlobalPosition;
		var bodyForward = Vector2.Right.Rotated(_bodyNode.Rotation);

		_ = bodyForward.Rotated(Mathf.Pi / 2f);

		// Positive cross = hit from left, negative cross = hit from right
		var cross = (bodyForward.X * toHit.Y) - (bodyForward.Y * toHit.X);
		var rotationDirection = cross > 0 ? 1f : -1f;

		var impactAngle = Mathf.DegToRad(ImpactRotationDegrees) * rotationDirection;
		var originalRotation = _bodyNode.Rotation;
		var targetRotation = originalRotation + impactAngle;

		// Create tween: rotate to impact angle, then back to original
		_impactRotationTween = CreateTween();
		_impactRotationTween.SetProcessMode(Tween.TweenProcessMode.Physics);
		_impactRotationTween.TweenProperty(_bodyNode, "rotation", targetRotation, ImpactRotationDuration * 0.3f)
			.SetEase(Tween.EaseType.Out);
		_impactRotationTween.TweenProperty(_bodyNode, "rotation", originalRotation, ImpactRotationDuration * 0.7f)
			.SetEase(Tween.EaseType.InOut);
	}

	/// <summary>
	/// Handles the dying state - continues knockback while death animation plays.
	/// </summary>
	private void HandleDying(float delta)
	{
		ApplyKnockbackDamp(delta);
		Velocity = _knockbackVelocity;
		MoveAndSlide();
	}

	/// <summary>
	/// Called when the death animation finishes.
	/// </summary>
	private void OnDeathAnimationFinished()
	{
		SpawnBloodSpatter();
		SpawnCorpse();
		QueueFree();
	}

	private void Die()
	{
		if (_isDying)
		{
			return;
		}

		_isDying = true;
		_impactRotationTween?.Kill();

		// Hide legs during death
		if (_legsNode is not null)
		{
			_legsNode.Visible = false;
		}

		// Play death animation (using corpse animation as death sequence)
		if (_sprite is not null && _sprite.SpriteFrames is not null && _sprite.SpriteFrames.HasAnimation("death"))
		{
			_sprite.Stop();
			_sprite.Play("death");
			GD.Print("ZombieController: Playing death animation");
		}
		else
		{
			// No death animation available, die immediately
			GD.Print("ZombieController: No death animation, dying immediately");
			OnDeathAnimationFinished();
		}
	}

	private void SpawnCorpse()
	{
		if (_corpseScene is null)
		{
			return;
		}

		if (_corpseScene.Instantiate<Node2D>() is not Node2D corpse)
		{
			return;
		}

		corpse.GlobalPosition = GlobalPosition;
		corpse.GlobalRotation = _bodyNode?.GlobalRotation ?? GlobalRotation;

		// If the corpse is a ZombieCorpse, set its velocity to the zombie's knockback velocity
		if (corpse is ZombieCorpse zc)
		{
			zc.SetVelocity(_knockbackVelocity);
		}

		GetTree().Root.AddChild(corpse);
	}

	private void SpawnBloodSpatter()
	{
		if (_bloodSpatterScene is null)
		{
			return;
		}

		if (_bloodSpatterScene.Instantiate() is not BloodSpatter spatter)
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
