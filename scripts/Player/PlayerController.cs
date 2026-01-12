namespace DAIgame.Player;

using DAIgame.Combat;
using DAIgame.Core;
using Godot;

/// <summary>
/// Hotline Miami-style player controller with snappy WASD movement and mouse aim.
/// Attach to a CharacterBody2D node.
/// </summary>
public partial class PlayerController : CharacterBody2D, IDamageable
{
	/// <summary>
	/// Movement speed in pixels per second.
	/// </summary>
	[Export]
	public float MoveSpeed { get; set; } = 200f;

	/// <summary>
	/// Rate at which knockback is damped per second.
	/// </summary>
	[Export]
	public float KnockbackDamp { get; set; } = 350f;

	/// <summary>
	/// Duration the weapon walk animation stays active after an attack.
	/// </summary>
	[Export]
	public float WeaponWalkDuration { get; set; } = 2f;

	/// <summary>
	/// Maximum health of the player.
	/// </summary>
	[Export]
	public float MaxHealth { get; set; } = 100f;

	/// <summary>
	/// Amount of health restored per heal item use.
	/// </summary>
	[Export]
	public float HealAmount { get; set; } = 25f;

	/// <summary>
	/// Knockback force applied when taking damage.
	/// </summary>
	[Export]
	public float DamageKnockbackStrength { get; set; } = 120f;

	/// <summary>
	/// Duration of the hit flash effect in seconds.
	/// </summary>
	[Export]
	public float HitFlashDuration { get; set; } = 0.1f;

	/// <summary>
	/// Number of healing items the player has.
	/// </summary>
	public int HealingItems { get; private set; } = 3;

	/// <summary>
	/// Current health of the player.
	/// </summary>
	public float CurrentHealth { get; private set; }

	/// <summary>
	/// Movement speed multiplier during kick animation (1/8 of normal).
	/// </summary>
	private const float KickSpeedMultiplier = 0.125f;

	/// <summary>
	/// Delay before kick damage is applied (in seconds).
	/// </summary>
	private const float KickDamageDelay = 0.1f;

	/// <summary>
	/// Range in pixels for kick to hit enemies.
	/// </summary>
	private const float KickRange = 35f;

	/// <summary>
	/// Damage dealt by kick.
	/// </summary>
	private const float KickDamage = 5f;

	/// <summary>
	/// Chance to inflict knockdown on kick (0.0 to 1.0).
	/// </summary>
	private const float KickKnockdownChance = 0.5f;

	/// <summary>
	/// Cone angle in degrees for kick (zombies must be within this cone in front of player).
	/// </summary>
	private const float KickConeAngle = 45f;

	private Node2D? _bodyNode;
	private AnimatedSprite2D? _bodySprite;
	private Node2D? _legsNode;
	private AnimatedSprite2D? _legsSprite;
	private WeaponManager? _weaponManager;
	private Vector2 _lastMoveDir = Vector2.Right;
	private Vector2 _aimDirection = Vector2.Right;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private float _weaponWalkTimer;
	private bool _attackPlaying;
	private bool _kickPlaying;
	private bool _kickDamagePending;
	private float _kickDamageTimer;
	private bool _isMoving;
	private float _hitFlashTimer;
	private string _currentWalkAnim = "walk";
	private string _currentAttackAnim = "attack_pistol";

	public override void _Ready()
	{
		// Add to player group for easy lookup
		AddToGroup("player");
		AddToGroup("damageable");

		CurrentHealth = MaxHealth;

		_bodyNode = GetNodeOrNull<Node2D>("Body");
		_bodySprite = _bodyNode?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_legsNode = GetNodeOrNull<Node2D>("Legs");
		_weaponManager = GetNodeOrNull<WeaponManager>("WeaponManager");
		_legsSprite = _legsNode?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

		if (_bodySprite is not null)
		{
			_bodySprite.AnimationFinished += OnBodyAnimationFinished;

			var frames = _bodySprite.SpriteFrames;
			if (frames is not null)
			{
				// Set attack animations to not loop (they play once per shot)
				SetAnimationNoLoop(frames, "attack_shotgun");
				SetAnimationNoLoop(frames, "attack_pistol");
				SetAnimationNoLoop(frames, "attack_uzi");
				SetAnimationNoLoop(frames, "kick");
			}

			_bodySprite.Play("walk");
		}

		_legsSprite?.Play("walk");

		// Subscribe to weapon change events
		if (_weaponManager is not null)
		{
			_weaponManager.WeaponChanged += OnWeaponChanged;
			// Initialize animations for starting weapon
			OnWeaponChanged(_weaponManager.CurrentWeapon);
		}
	}

	private static void SetAnimationNoLoop(SpriteFrames frames, string animName)
	{
		if (frames.HasAnimation(animName))
		{
			frames.SetAnimationLoop(animName, false);
		}
	}

	private void OnWeaponChanged(WeaponData? weapon)
	{
		if (weapon is null)
		{
			_currentWalkAnim = "walk";
			_currentAttackAnim = "attack_pistol";
			return;
		}

		_currentWalkAnim = weapon.GetWalkAnimation();
		_currentAttackAnim = weapon.GetAttackAnimation();
		GD.Print($"PlayerController: Weapon changed to {weapon.DisplayName}, walk={_currentWalkAnim}, attack={_currentAttackAnim}");

		// Update current animation if not attacking or kicking
		if (!_attackPlaying && !_kickPlaying && _bodySprite is not null)
		{
			_bodySprite.Play(_currentWalkAnim);
		}
	}

	public override void _Process(double delta)
	{
		RotateTowardsMouse();
		HandleFire();
		HandleWeaponSwitch();
		HandleReload();
		HandleHeal();
		HandleKick();
		UpdateKickDamage((float)delta);
		UpdateBodyAnimation((float)delta);
		UpdateHitFlash((float)delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleMovement();
		ApplyKnockbackDamp((float)delta);
	}

	/// <summary>
	/// Handles WASD input for immediate, snappy movement.
	/// No acceleration - instant velocity change based on input.
	/// </summary>
	private void HandleMovement()
	{
		var inputDir = GetInputDirection();

		_isMoving = inputDir.LengthSquared() > 0;

		// Normalize to prevent faster diagonal movement
		if (inputDir.LengthSquared() > 0)
		{
			inputDir = inputDir.Normalized();
			_lastMoveDir = inputDir;
		}

		// Apply velocity directly - snappy, no acceleration. Knockback is additive and damped separately.
		// Reduce speed to 1/8 during kick animation.
		var speedMultiplier = _kickPlaying ? KickSpeedMultiplier : 1f;
		Velocity = (inputDir * MoveSpeed * speedMultiplier) + _knockbackVelocity;
		MoveAndSlide();

		UpdateLegsFacing(inputDir);
	}

	/// <summary>
	/// Rotates the player body to face the mouse cursor.
	/// Updates every frame for responsive aiming.
	/// Body direction is locked during kick animation.
	/// </summary>
	private void RotateTowardsMouse()
	{
		// Lock body direction during kick
		if (_kickPlaying)
		{
			return;
		}

		var mousePos = GetGlobalMousePosition();
		var direction = mousePos - GlobalPosition;

		if (direction == Vector2.Zero)
		{
			return;
		}

		_aimDirection = direction.Normalized();
		var targetAngle = _aimDirection.Angle();

		if (_bodyNode is not null)
		{
			_bodyNode.Rotation = targetAngle;
		}
	}

	private void HandleFire()
	{
		// Block firing during kick
		if (_kickPlaying)
		{
			return;
		}

		if (_weaponManager is null)
		{
			return;
		}

		var weapon = _weaponManager.CurrentWeapon;
		if (weapon is null)
		{
			return;
		}

		// Check fire input based on weapon fire mode
		bool shouldFire;
		if (weapon.FireMode == WeaponFireMode.Automatic)
		{
			// Automatic: fire while held
			shouldFire = Input.IsActionPressed("Fire");
		}
		else
		{
			// Semi-auto: fire on press only
			shouldFire = Input.IsActionJustPressed("Fire");
		}

		if (!shouldFire)
		{
			return;
		}

		if (_weaponManager.TryFire(GlobalPosition, _aimDirection))
		{
			StartAttackAnimation();
			ApplyKnockbackImpulse(weapon.KnockbackStrength);
		}
	}

	private void HandleWeaponSwitch()
	{
		if (_kickPlaying || _attackPlaying)
		{
			return;
		}

		if (!Input.IsActionJustPressed("SwitchHeld"))
		{
			return;
		}

		_weaponManager?.CycleWeapon();
	}

	private void HandleReload()
	{
		if (_kickPlaying)
		{
			return;
		}

		if (!Input.IsActionJustPressed("Reload"))
		{
			return;
		}

		_weaponManager?.StartReload();
	}

	private void HandleHeal()
	{
		// Block healing during kick
		if (_kickPlaying)
		{
			return;
		}

		if (!Input.IsActionJustPressed("UseHealItem"))
		{
			return;
		}

		if (HealingItems <= 0)
		{
			GD.Print("No healing items remaining!");
			return;
		}

		if (CurrentHealth >= MaxHealth)
		{
			GD.Print("Already at full health!");
			return;
		}

		HealingItems--;
		var healedAmount = Mathf.Min(HealAmount, MaxHealth - CurrentHealth);
		CurrentHealth = Mathf.Min(CurrentHealth + HealAmount, MaxHealth);
		GD.Print($"Healed {healedAmount}! Health: {CurrentHealth}/{MaxHealth} (Items left: {HealingItems})");
	}

	/// <summary>
	/// Adds healing items to the player's inventory.
    /// </summary>
    public void AddHealingItems(int count)
    {
        HealingItems += count;
        GD.Print($"Picked up {count} healing item(s). Total: {HealingItems}");
    }

    private void HandleKick()
    {
        // Block kicking during attack or already kicking
        if (_attackPlaying || _kickPlaying)
        {
            return;
        }

        if (!Input.IsActionJustPressed("Kick"))
        {
            return;
        }

        StartKickAnimation();
    }

    private void StartKickAnimation()
    {
        if (_bodySprite is null)
        {
            return;
        }

        _kickPlaying = true;
        _kickDamagePending = true;
        _kickDamageTimer = KickDamageDelay;
        _bodySprite.Stop();
        _bodySprite.Play("kick");

        // Stop legs animation at frame 0
        if (_legsSprite is not null)
        {
            _legsSprite.Stop();
            _legsSprite.Frame = 0;
        }

        GD.Print("Player kick started");
    }

    private void UpdateKickDamage(float delta)
    {
        if (!_kickDamagePending)
        {
            return;
        }

        _kickDamageTimer -= delta;
        if (_kickDamageTimer <= 0f)
        {
            _kickDamagePending = false;
            PerformKickDamage();
        }
    }

    private void PerformKickDamage()
    {
        // Get all enemies in the scene
        var enemies = GetTree().GetNodesInGroup("enemies");
        var kickOrigin = GlobalPosition;

        GD.Print($"PerformKickDamage: checking {enemies.Count} enemies, kick origin: {kickOrigin}, aim direction: {_aimDirection}");

        foreach (var enemy in enemies)
        {
            if (enemy is not Node2D enemyNode)
            {
                continue;
            }

            var distance = kickOrigin.DistanceTo(enemyNode.GlobalPosition);
            GD.Print($"  Enemy at {enemyNode.GlobalPosition}, distance: {distance:F1}px (range: {KickRange}px)");

            if (distance > KickRange)
            {
                continue;
            }

            // Check if enemy is within the kick cone (45 degrees in front of player)
            var directionToEnemy = (enemyNode.GlobalPosition - kickOrigin).Normalized();
            var angleDifference = Mathf.Abs(_aimDirection.AngleTo(directionToEnemy));
            var maxAngle = Mathf.DegToRad(KickConeAngle / 2f); // Half angle on each side

            GD.Print($"  Angle difference: {Mathf.RadToDeg(angleDifference):F1}° (max: {KickConeAngle / 2f}°)");

            if (angleDifference > maxAngle)
            {
                GD.Print($"  Enemy outside kick cone, skipping");
                continue;
            }

            // Apply damage
            if (enemy is IDamageable damageable)
            {
                var hitPos = enemyNode.GlobalPosition;
                var hitNormal = (enemyNode.GlobalPosition - kickOrigin).Normalized();
                damageable.ApplyDamage(KickDamage, kickOrigin, hitPos, hitNormal);
                GD.Print($"Kick hit enemy for {KickDamage} damage");
            }

            // 50% chance to inflict knockdown
            if (GD.Randf() < KickKnockdownChance && enemy is AI.ZombieController zombie)
            {
                zombie.ApplyKnockdown();
                GD.Print("Kick inflicted knockdown!");
            }
        }
    }

    private void StartAttackAnimation()
    {
        if (_bodySprite is null)
        {
            return;
        }

        _attackPlaying = true;
        _weaponWalkTimer = 0f;
        _bodySprite.Stop();
        _bodySprite.Play(_currentAttackAnim);
    }

    private void OnBodyAnimationFinished()
    {
        if (_bodySprite is null)
        {
            return;
        }

        if (_bodySprite.Animation == "kick")
        {
            _kickPlaying = false;
            _bodySprite.Play(_currentWalkAnim);
            GD.Print("Player kick finished");
            return;
        }

        // Check if any attack animation finished
        if (!IsAttackAnimation(_bodySprite.Animation))
        {
            return;
        }

        _attackPlaying = false;
        _weaponWalkTimer = WeaponWalkDuration;
        _bodySprite.Play(_currentWalkAnim);
    }

    private static bool IsAttackAnimation(StringName animation)
    {
        return animation == "attack_pistol" ||
               animation == "attack_shotgun" ||
               animation == "attack_uzi";
    }

    private void UpdateBodyAnimation(float delta)
    {
        if (_bodySprite is null)
        {
            return;
        }

		// Don't interfere with kick animation
		if (_kickPlaying)
		{
			return;
		}

		if (_weaponWalkTimer > 0f)
		{
			_weaponWalkTimer -= delta;
			if (_weaponWalkTimer <= 0f && !_attackPlaying)
			{
				_bodySprite.Play(_currentWalkAnim);
			}
		}
		else if (!_attackPlaying && _bodySprite.Animation != _currentWalkAnim)
		{
			_bodySprite.Play(_currentWalkAnim);
		}

		// Pause walk animation when not moving
		if (!_isMoving && !_attackPlaying && IsWalkAnimation(_bodySprite.Animation))
		{
			_bodySprite.Stop();
		}
		else if (_isMoving && !_attackPlaying)
		{
			_bodySprite.Play();
		}
	}

	private static bool IsWalkAnimation(StringName animation)
	{
		return animation == "walk" ||
			   animation == "walk_pistol" ||
			   animation == "walk_shotgun" ||
			   animation == "walk_uzi";
	}

	private void UpdateLegsFacing(Vector2 inputDir)
	{
		if (_legsNode is null)
		{
			return;
		}

		var dir = inputDir.LengthSquared() > 0 ? inputDir : _lastMoveDir;
		var snappedAngle = SnapToEightDirections(dir.Angle());
		_legsNode.Rotation = snappedAngle;

		// Pause legs animation when not moving or during kick
		if (_legsSprite is not null)
		{
			var walkAnim = _aimDirection.X < 0 ? "WalkL" : "WalkR";
			_legsSprite.FlipH = walkAnim == "WalkL";

			// Keep legs stopped at frame 0 during kick
			if (_kickPlaying)
			{
				_legsSprite.Stop();
				_legsSprite.Frame = 0;
			}
			else if (!_isMoving)
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

	private void ApplyKnockbackImpulse(float strength) => _knockbackVelocity = -_aimDirection * strength;

	private void ApplyKnockbackDamp(float delta)
	{
		if (_knockbackVelocity == Vector2.Zero)
		{
			return;
		}

		_knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDamp * delta);
	}

	private static Vector2 GetInputDirection()
	{
		var inputDir = Vector2.Zero;

		if (Input.IsActionPressed("MoveUp"))
		{
			inputDir.Y -= 1;
		}

		if (Input.IsActionPressed("MoveDown"))
		{
			inputDir.Y += 1;
		}

		if (Input.IsActionPressed("MoveLeft"))
		{
			inputDir.X -= 1;
		}

		if (Input.IsActionPressed("MoveRight"))
		{
			inputDir.X += 1;
		}

		return inputDir;
	}

	/// <summary>
	/// Damage interface implementation.
	/// </summary>
	public void ApplyDamage(float amount, Vector2 fromPos, Vector2 hitPos, Vector2 hitNormal)
	{
		CurrentHealth -= amount;
		GD.Print($"Player took {amount} damage! Health: {CurrentHealth}/{MaxHealth}");

		// Apply knockback away from damage source
		var knockbackDir = (GlobalPosition - fromPos).Normalized();
		if (knockbackDir == Vector2.Zero)
		{
			knockbackDir = Vector2.Right;
		}
		_knockbackVelocity += knockbackDir * DamageKnockbackStrength;

		// Trigger hit flash
		_hitFlashTimer = HitFlashDuration;
		ApplyHitFlash();

		if (CurrentHealth <= 0f)
		{
			Die();
		}
	}

	private void ApplyHitFlash()
	{
		// Red flash overlay effect on player
		if (_bodySprite is not null)
		{
			_bodySprite.Modulate = new Color(2f, 0.5f, 0.5f, 1f);
		}

		if (_legsSprite is not null)
		{
			_legsSprite.Modulate = new Color(2f, 0.5f, 0.5f, 1f);
		}
	}

	private void UpdateHitFlash(float delta)
	{
		if (_hitFlashTimer <= 0f)
		{
			return;
		}

		_hitFlashTimer -= delta;

		if (_hitFlashTimer <= 0f)
		{
			if (_bodySprite is not null)
			{
				_bodySprite.Modulate = Colors.White;
			}

			if (_legsSprite is not null)
			{
				_legsSprite.Modulate = Colors.White;
			}
		}
	}

	private void Die()
	{
		GD.Print("Player died!");
		// For now, just restart by resetting health
		// Later this could trigger a death screen or respawn system
		CurrentHealth = MaxHealth;
	}
}
