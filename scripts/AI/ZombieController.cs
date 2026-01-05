namespace DAIgame.AI;

using Godot;

/// <summary>
/// Simple zombie AI that idles, chases the player when within detection range,
/// and attacks when in melee range.
/// </summary>
public partial class ZombieController : CharacterBody2D
{
	/// <summary>
	/// Zombie movement speed in pixels per second.
	/// </summary>
	[Export]
	public float MoveSpeed { get; set; } = 100f;

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

	private AnimatedSprite2D? _sprite;
	private Node2D? _player;
	private float _attackCooldownTimer;
	private float _currentHealth;
	private ZombieState _state = ZombieState.Idle;

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
		_currentHealth = MaxHealth;

		if (_sprite is not null)
		{
			_sprite.AnimationFinished += OnAnimationFinished;
			_sprite.Play("idle");
		}
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

		// State transitions
		if (distanceToPlayer <= AttackRange)
		{
			SetState(ZombieState.Attacking);
		}
		else if (distanceToPlayer <= DetectionRange)
		{
			SetState(ZombieState.Chasing);
		}
		else
		{
			SetState(ZombieState.Idle);
		}

		// State behavior
		switch (_state)
		{
			case ZombieState.Idle:
				HandleIdle();
				break;
			case ZombieState.Chasing:
				HandleChasing();
				break;
			case ZombieState.Attacking:
				HandleAttacking((float)delta);
				break;
		}
	}

	private void HandleIdle()
	{
		Velocity = Vector2.Zero;
		MoveAndSlide();
	}

	private void HandleChasing()
	{
		if (_player is null)
		{
			return;
		}

		var direction = (_player.GlobalPosition - GlobalPosition).Normalized();
		Velocity = direction * MoveSpeed;
		MoveAndSlide();

		// Face the player
		var angle = direction.Angle();
		Rotation = angle;
	}

	private void HandleAttacking(float delta)
	{
		// Stop moving during attack
		Velocity = Vector2.Zero;
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

		// Try to damage the player
		if (_player.HasMethod("ApplyDamage"))
		{
			var hitPos = _player.GlobalPosition;
			var fromPos = GlobalPosition;
			var hitNormal = (fromPos - hitPos).Normalized();

			_player.Call("ApplyDamage", AttackDamage, fromPos, hitPos, hitNormal);
		}
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
		_currentHealth -= amount;

		if (_currentHealth <= 0f)
		{
			Die();
		}
	}

	private void Die() => QueueFree();
}
