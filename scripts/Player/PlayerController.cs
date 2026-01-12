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

	#region Player Attributes

	/// <summary>
	/// Strength attribute. Affects melee damage and carrying capacity.
	/// </summary>
	[Export]
	public int Strength { get; set; } = 10;

	/// <summary>
	/// Dexterity attribute. Affects aim turning speed and reduces backward movement penalty.
	/// </summary>
	[Export]
	public int Dexterity { get; set; } = 10;

	/// <summary>
	/// Constitution attribute. Affects health and resistance.
	/// </summary>
	[Export]
	public int Constitution { get; set; } = 10;

	/// <summary>
	/// Intelligence attribute. Affects crafting and tech abilities.
	/// </summary>
	[Export]
	public int Intelligence { get; set; } = 10;

	/// <summary>
	/// Intuition attribute. Affects perception and awareness.
	/// </summary>
	[Export]
	public int Intuition { get; set; } = 10;

	#endregion

	/// <summary>
	/// Base turning speed in radians per second for aiming.
	/// </summary>
	[Export]
	public float BaseTurnSpeed { get; set; } = 20f;

	/// <summary>
	/// Maximum backward movement penalty (0.3 = 30% slower).
	/// </summary>
	private const float MaxBackwardPenalty = 0.3f;

	/// <summary>
	/// Dexterity offset per point above/below 10 for backward penalty (2% per point).
	/// </summary>
	private const float DexBackwardPenaltyOffset = 0.02f;

	/// <summary>
	/// Dexterity scaling for turn speed (5% per point above/below 10).
	/// </summary>
	private const float DexTurnSpeedScale = 0.05f;

	/// <summary>
	/// Maximum stamina of the player.
	/// </summary>
	[Export]
	public float MaxStamina { get; set; } = 100f;

	/// <summary>
	/// Stamina regeneration rate per second.
	/// </summary>
	[Export]
	public float StaminaRegenRate { get; set; } = 25f;

	/// <summary>
	/// Delay in seconds after using stamina before regeneration starts.
	/// </summary>
	[Export]
	public float StaminaRegenDelay { get; set; } = 0.5f;

	/// <summary>
	/// Stamina cost for kick attacks.
	/// </summary>
	[Export]
	public float KickStaminaCost { get; set; } = 20f;

	/// <summary>
	/// Number of healing items the player has.
	/// </summary>
	public int HealingItems { get; private set; } = 3;

	/// <summary>
	/// Current health of the player.
	/// </summary>
	public float CurrentHealth { get; private set; }

	/// <summary>
	/// Current stamina of the player.
	/// </summary>
	public float CurrentStamina { get; private set; }

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
	private float _staminaRegenDelayTimer;
	private string _currentWalkAnim = "walk";
	private string _currentAttackAnim = "attack_pistol";
	private float _baseAttackAnimSpeed = 12f;
	private int _currentAttackFrameCount = 3;

	public override void _Ready()
	{
		// Add to player group for easy lookup
		AddToGroup("player");
		AddToGroup("damageable");

		CurrentHealth = MaxHealth;
		CurrentStamina = MaxStamina;

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
				// Set non-automatic attack animations to not loop (they play once per shot)
				// Automatic weapon animations (uzi) stay looping for continuous fire
				SetAnimationNoLoop(frames, "attack_shotgun");
				SetAnimationNoLoop(frames, "attack_pistol");
				SetAnimationNoLoop(frames, "attack_bat");
				// attack_uzi stays looping for continuous automatic fire
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
			_currentAttackFrameCount = 3;
			CursorManager.Instance?.SetWeaponEquipped(false);
			return;
		}

		_currentWalkAnim = weapon.GetWalkAnimation();
		_currentAttackAnim = weapon.GetAttackAnimation();

		// Cache attack animation frame count for speed calculations
		if (_bodySprite?.SpriteFrames is SpriteFrames frames && frames.HasAnimation(_currentAttackAnim))
		{
			_currentAttackFrameCount = frames.GetFrameCount(_currentAttackAnim);
			_baseAttackAnimSpeed = (float)frames.GetAnimationSpeed(_currentAttackAnim);
			GD.Print($"PlayerController: Attack anim '{_currentAttackAnim}' has {_currentAttackFrameCount} frames at base speed {_baseAttackAnimSpeed}");
		}

		GD.Print($"PlayerController: Weapon changed to {weapon.DisplayName}, walk={_currentWalkAnim}, attack={_currentAttackAnim}");

		// Update current animation if not attacking or kicking
		if (!_attackPlaying && !_kickPlaying && _bodySprite is not null)
		{
			_bodySprite.Play(_currentWalkAnim);
		}

		CursorManager.Instance?.SetWeaponEquipped(true);
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
		UpdateMeleeSwingOrigin();
		UpdateStamina((float)delta);
	}

	/// <summary>
	/// Updates stamina regeneration.
	/// </summary>
	private void UpdateStamina(float delta)
	{
		if (_staminaRegenDelayTimer > 0f)
		{
			_staminaRegenDelayTimer -= delta;
			return;
		}

		if (CurrentStamina < MaxStamina)
		{
			CurrentStamina = Mathf.Min(CurrentStamina + (StaminaRegenRate * delta), MaxStamina);
		}
	}

	/// <summary>
	/// Consumes stamina for an action. Returns true if successful.
	/// </summary>
	/// <param name="amount">Amount of stamina to consume.</param>
	/// <returns>True if stamina was consumed, false if not enough stamina.</returns>
	public bool TryConsumeStamina(float amount)
	{
		if (CurrentStamina < amount)
		{
			GD.Print($"Not enough stamina! Need {amount}, have {CurrentStamina:F1}");
			return false;
		}

		CurrentStamina -= amount;
		_staminaRegenDelayTimer = StaminaRegenDelay;
		GD.Print($"Consumed {amount} stamina. Remaining: {CurrentStamina:F1}/{MaxStamina}");
		return true;
	}

	/// <summary>
	/// Checks if player has enough stamina for an action.
	/// </summary>
	public bool HasStamina(float amount) => CurrentStamina >= amount;

	/// <summary>
	/// Updates the melee swing origin position if a swing attack is in progress.
	/// </summary>
	private void UpdateMeleeSwingOrigin()
	{
		if (_weaponManager?.MeleeHandler is { IsSwinging: true } handler)
		{
			handler.UpdateSwingOrigin(GlobalPosition);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleMovement();
		ApplyKnockbackDamp((float)delta);
	}

	/// <summary>
	/// Handles WASD input for immediate, snappy movement.
	/// No acceleration - instant velocity change based on input.
	/// Applies backward movement penalty when moving away from aim direction.
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

		// Apply backward movement penalty when moving away from aim direction
		speedMultiplier *= CalculateBackwardPenaltyMultiplier(inputDir);

		Velocity = (inputDir * MoveSpeed * speedMultiplier) + _knockbackVelocity;
		MoveAndSlide();

		UpdateLegsFacing(inputDir);
	}

	/// <summary>
	/// Calculates the speed multiplier based on the angle between movement and aim direction.
	/// Penalty starts at 90 degrees and maxes out at 180 degrees (directly backward).
	/// Dexterity offsets the penalty: higher dex reduces it, lower dex increases it.
	/// </summary>
	private float CalculateBackwardPenaltyMultiplier(Vector2 moveDir)
	{
		if (moveDir.LengthSquared() < 0.01f)
		{
			return 1f;
		}

		// Calculate angle between movement direction and aim direction
		var angleBetween = Mathf.Abs(moveDir.AngleTo(_aimDirection));

		// No penalty if moving within 90 degrees of aim direction
		if (angleBetween <= Mathf.Pi / 2f)
		{
			return 1f;
		}

		// Calculate how far past 90 degrees we are (0 at 90°, 1 at 180°)
		var penaltyFactor = (angleBetween - (Mathf.Pi / 2f)) / (Mathf.Pi / 2f);

		// Base penalty scaled by how far backward we're moving
		var basePenalty = MaxBackwardPenalty * penaltyFactor;

		// Dexterity offset: each point above 10 reduces penalty, below 10 increases it
		var dexOffset = (Dexterity - 10) * DexBackwardPenaltyOffset;
		var finalPenalty = Mathf.Clamp(basePenalty - dexOffset, 0f, 0.5f);

		return 1f - finalPenalty;
	}

	/// <summary>
	/// Rotates the player body to face the mouse cursor.
	/// Uses dexterity-based turning speed for smooth but responsive aiming.
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

		var targetDirection = direction.Normalized();
		var targetAngle = targetDirection.Angle();

		if (_bodyNode is null)
		{
			return;
		}

		// Calculate dexterity-modified turn speed
		var dexModifier = 1f + ((Dexterity - 10) * DexTurnSpeedScale);
		var turnSpeed = BaseTurnSpeed * Mathf.Max(dexModifier, 0.2f);

		// Calculate the shortest angular distance
		var currentAngle = _bodyNode.Rotation;
		var angleDiff = Mathf.Wrap(targetAngle - currentAngle, -Mathf.Pi, Mathf.Pi);

		// Apply turn speed limit (use unscaled delta for smooth turning regardless of slow-mo)
		var delta = (float)GetProcessDeltaTime();
		var maxTurn = turnSpeed * delta;

		var newAngle = Mathf.Abs(angleDiff) <= maxTurn
			? targetAngle
			: currentAngle + (Mathf.Sign(angleDiff) * maxTurn);

		_bodyNode.Rotation = newAngle;
		_aimDirection = new Vector2(Mathf.Cos(newAngle), Mathf.Sin(newAngle));
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

			// Stop attack animation if out of ammo while holding fire
			if (_weaponManager.CurrentAmmo <= 0 && _attackPlaying && _bodySprite?.Animation == _currentAttackAnim)
			{
				_attackPlaying = false;
				_weaponWalkTimer = WeaponWalkDuration;
				_bodySprite?.Play(_currentWalkAnim);
			}

			// Handle releasing fire button on automatic weapon
			if (!shouldFire && _attackPlaying && _bodySprite?.Animation == _currentAttackAnim)
			{
				// Stop automatic fire animation and return to walk
				_attackPlaying = false;
				_weaponWalkTimer = WeaponWalkDuration;
				_bodySprite?.Play(_currentWalkAnim);
			}
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

		// Check stamina for melee weapons
		if (weapon.IsMelee && weapon.StaminaCost > 0f)
		{
			if (!HasStamina(weapon.StaminaCost))
			{
				return;
			}
		}

		// TryFire returns true if a shot was fired (ammo was available before call)
		if (_weaponManager.TryFire(GlobalPosition, _aimDirection))
		{
			// Consume stamina for melee weapons
			if (weapon.IsMelee && weapon.StaminaCost > 0f)
			{
				TryConsumeStamina(weapon.StaminaCost);
			}

			// Pass true to indicate this shot was just fired, even if ammo is now 0
			StartAttackAnimation(true);
			ApplyKnockbackImpulse(weapon.KnockbackPlayer);
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

		// Check stamina for kick
		if (!TryConsumeStamina(KickStaminaCost))
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

	private void StartAttackAnimation(bool force = false)
	{
		if (_bodySprite is null)
		{
			return;
		}

		var weapon = _weaponManager?.CurrentWeapon;
		if (weapon is null || _currentAttackFrameCount <= 0)
		{
			return;
		}

		// Prevent attack animation if out of ammo, unless forced (i.e., shot was just fired)
		if (!force && (_weaponManager == null || _weaponManager.CurrentAmmo <= 0))
		{
			GD.Print("PlayerController: Cannot play attack animation - weapon manager is null or out of ammo.");
			_attackPlaying = false;
			return;
		}

		_attackPlaying = true;
		_weaponWalkTimer = 0f;

		var timeBetweenShots = weapon.TimeBetweenShots;
		var targetSpeed = _currentAttackFrameCount / timeBetweenShots;

		// For automatic weapons, we want to ensure both frames always play
		// so use a minimum speed that still looks good but plays all frames
		if (weapon.FireMode == WeaponFireMode.Automatic)
		{
			// For uzi (2 frames), we need at least some minimum speed to see both frames
			// At high fire rates, the animation loops during sustained fire
			// Ensure a minimum that plays smoothly (at least 8 FPS to see frames)
			targetSpeed = Mathf.Max(targetSpeed, 8f);

			// For automatic weapons, don't restart the animation if already playing
			// This ensures both frames play continuously without jumping back to frame 0
			_bodySprite.SpriteFrames?.SetAnimationSpeed(_currentAttackAnim, targetSpeed);
			if (_bodySprite.Animation != _currentAttackAnim)
			{
				_bodySprite.Play(_currentAttackAnim);
			}
			else if (!_bodySprite.IsPlaying())
			{
				_bodySprite.Play(_currentAttackAnim);
			}
			GD.Print($"PlayerController: Auto attack anim speed = {targetSpeed:F1} FPS (frames={_currentAttackFrameCount}, interval={timeBetweenShots:F3}s)");
			return;
		}

		GD.Print($"PlayerController: Attack anim speed = {targetSpeed:F1} FPS (frames={_currentAttackFrameCount}, interval={timeBetweenShots:F3}s)");
		_bodySprite.SpriteFrames?.SetAnimationSpeed(_currentAttackAnim, targetSpeed);

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

	/// <summary>
	/// Checks if the animation name is an attack animation.
	/// Uses prefix matching to support any weapon type without hardcoding.
	/// </summary>
	private static bool IsAttackAnimation(StringName animation)
	{
		var name = animation.ToString();
		return name.StartsWith("attack_", System.StringComparison.Ordinal);
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

	/// <summary>
	/// Checks if the animation name is a walk animation.
	/// Uses prefix matching to support any weapon type without hardcoding.
	/// </summary>
	private static bool IsWalkAnimation(StringName animation)
	{
		var name = animation.ToString();
		return name == "walk" || name.StartsWith("walk_", System.StringComparison.Ordinal);
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
