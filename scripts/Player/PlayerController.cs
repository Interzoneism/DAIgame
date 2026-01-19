namespace DAIgame.Player;

using System.Collections.Generic;
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
	/// Stats manager that provides all derived stats from attributes, feats, and equipment.
	/// </summary>
	private PlayerStatsManager? _statsManager;

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
	/// Max pixel amplitude for held-item walk wiggle at full move speed.
	/// </summary>
	[Export]
	public float HeldWiggleAmplitude { get; set; } = 2.5f;

	/// <summary>
	/// Wiggle frequency in cycles per second at full move speed.
	/// </summary>
	[Export]
	public float HeldWiggleFrequency { get; set; } = 9f;

	/// <summary>
	/// Pixels of recoil kick applied to held item per ranged shot.
	/// </summary>
	[Export]
	public float HeldRecoilKick { get; set; } = 1.5f;

	/// <summary>
	/// Max pixels of recoil offset allowed.
	/// </summary>
	[Export]
	public float HeldRecoilMax { get; set; } = 3f;

	/// <summary>
	/// Pixels per second to return held recoil offset to zero.
	/// </summary>
	[Export]
	public float HeldRecoilReturnSpeed { get; set; } = 16f;

	/// <summary>
	/// Pixel-based kick amount for subtle body scale recoil on ranged fire.
	/// </summary>
	[Export]
	public float BodyFireScalePixels { get; set; } = 1.5f;

	/// <summary>
	/// Spring strength for body scale recoil.
	/// </summary>
	[Export]
	public float BodyFireScaleSpring { get; set; } = 120f;

	/// <summary>
	/// Damping for body scale recoil (higher = less oscillation).
	/// </summary>
	[Export]
	public float BodyFireScaleDamping { get; set; } = 18f;

	/// <summary>
	/// Per-animation anchors for held items (offset + rotation when facing east).
	/// </summary>
	[Export]
	public Godot.Collections.Array<HeldAnimationAnchor> HeldAnimationAnchors { get; set; } = [];

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
	/// Movement speed multiplier while aiming down sights.
	/// </summary>
	[Export]
	public float AimDownSightsMoveSpeedMultiplier { get; set; } = 0.5f;

	/// <summary>
	/// Turn speed multiplier while aiming down sights.
	/// </summary>
	[Export]
	public float AimDownSightsTurnSpeedMultiplier { get; set; } = 0.5f;

	/// <summary>
	/// Delay in seconds after using stamina before regeneration starts.
	/// </summary>
	[Export]
	public float StaminaRegenDelay { get; set; } = 0.5f;

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
	/// Gets max health from stats manager.
	/// </summary>
	public float MaxHealth => _statsManager?.MaxHealth ?? 100f;

	/// <summary>
	/// Gets max stamina from stats manager.
	/// </summary>
	public float MaxStamina => _statsManager?.MaxStamina ?? 100f;

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
	private Sprite2D? _heldSprite;
	private Node2D? _legsNode;
	private AnimatedSprite2D? _legsSprite;
	private Node2D? _bodyScaleNode;
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
	private bool _isAimingDownSights;
	private float _hitFlashTimer;
	private float _staminaRegenDelayTimer;
	private string _currentWalkAnim = "walk";
	private string _currentAttackAnim = "attack_pistol";
	private float _baseAttackAnimSpeed = 12f;
	private int _currentAttackFrameCount = 3;
	private readonly Dictionary<string, float> _bodyWalkBaseSpeeds = [];
	private float _walkSpeedReference;
	private float _currentMoveSpeed;
	private readonly Dictionary<string, HeldAnimationAnchor> _heldAnchorLookup = [];
	private readonly HashSet<string> _missingHeldAnchors = [];
	private string _lastHeldAnimation = string.Empty;
	private Vector2 _heldBaseOffset = Vector2.Zero;
	private Vector2 _heldWiggleOffset = Vector2.Zero;
	private Vector2 _heldRecoilOffset = Vector2.Zero;
	private float _heldWiggleTime;
	private float _heldBaseRotationRad;
	private Vector2 _bodyBaseScale = Vector2.One;
	private Vector2 _bodyScaleOffset = Vector2.Zero;
	private Vector2 _bodyScaleVelocity = Vector2.Zero;
	private float _cachedBodySpriteSize = 32f;

	public override void _Ready()
	{
		// Add to player group for easy lookup
		AddToGroup("player");
		AddToGroup("damageable");

		// Get or create PlayerStatsManager
		_statsManager = GetNodeOrNull<PlayerStatsManager>("PlayerStatsManager");
		if (_statsManager is null)
		{
			_statsManager = new PlayerStatsManager();
			AddChild(_statsManager);
			GD.Print("[PlayerController] Created PlayerStatsManager");
		}

		CurrentHealth = _statsManager.MaxHealth;
		CurrentStamina = _statsManager.MaxStamina;
		_walkSpeedReference = _statsManager.MoveSpeed;
		if (_walkSpeedReference <= 0f)
		{
			_walkSpeedReference = 100f;
		}

		_bodyNode = GetNodeOrNull<Node2D>("Body");
		_bodySprite = _bodyNode?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_heldSprite = _bodyNode?.GetNodeOrNull<Sprite2D>("Held");
		_legsNode = GetNodeOrNull<Node2D>("Legs");
		_weaponManager = GetNodeOrNull<WeaponManager>("WeaponManager");
		_legsSprite = _legsNode?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_bodyScaleNode = _bodySprite ?? _bodyNode;

		CacheBodyWalkBaseSpeeds();
		EnsureDefaultHeldAnchors();
		BuildHeldAnchorLookup();
		CacheBodyBaseScaleAndSize();

		if (_bodySprite is not null)
		{
			_bodySprite.AnimationFinished += OnBodyAnimationFinished;

			var frames = _bodySprite.SpriteFrames;
			if (frames is not null)
			{
				// Now handled by WeaponData.AttackAnimationLoops
			}

			var idleAnim = _bodySprite.SpriteFrames?.HasAnimation("mod_walk") == true ? "mod_walk" : "walk";
			_bodySprite.Play(idleAnim);
		}

		_legsSprite?.Play("walk");

		// Subscribe to weapon change events
		if (_weaponManager is not null)
		{
			_weaponManager.WeaponChanged += OnWeaponChanged;
			_weaponManager.WeaponFired += OnWeaponFired;
			// Initialize animations for starting weapon
			OnWeaponChanged(_weaponManager.CurrentWeapon);
		}
	}

	private void OnWeaponChanged(WeaponData? weapon)
	{
		if (weapon is null)
		{
			_currentWalkAnim = "mod_walk";
			_currentAttackAnim = string.Empty;
			_currentAttackFrameCount = 0;
			_baseAttackAnimSpeed = 0f;
			CursorManager.Instance?.SetWeaponEquipped(false);
			if (_heldSprite is not null)
			{
				_heldSprite.Texture = null;
				_heldSprite.Visible = false;
			}
			_heldRecoilOffset = Vector2.Zero;
			_heldWiggleOffset = Vector2.Zero;
			return;
		}

		_currentWalkAnim = weapon.GetWalkAnimation();
		_currentAttackAnim = weapon.GetAttackAnimation();

		// Set held weapon sprite
		if (_heldSprite is not null)
		{
			_heldSprite.Texture = weapon.HeldSprite;
			_heldSprite.Visible = weapon.HeldSprite is not null;
			if (weapon.HeldSprite is null)
			{
				GD.PrintErr($"PlayerController: Weapon '{weapon.DisplayName}' has no HeldSprite assigned.");
			}
		}

		if (_bodySprite?.SpriteFrames is SpriteFrames frames)
		{
			if (!frames.HasAnimation(_currentWalkAnim))
			{
				var fallbackWalk = frames.HasAnimation("mod_walk") ? "mod_walk" : "walk";
				if (frames.HasAnimation(fallbackWalk))
				{
					GD.PrintErr($"PlayerController: Walk animation '{_currentWalkAnim}' not found for {weapon.DisplayName}; using '{fallbackWalk}'.");
					_currentWalkAnim = fallbackWalk;
				}
				else
				{
					GD.PrintErr($"PlayerController: Walk animation '{_currentWalkAnim}' not found for {weapon.DisplayName} and no fallback is available.");
				}
			}

			// Cache attack animation frame count for speed calculations
			_currentAttackFrameCount = 0;
			_baseAttackAnimSpeed = 0f;
			if (!string.IsNullOrEmpty(_currentAttackAnim) && frames.HasAnimation(_currentAttackAnim))
			{
				_currentAttackFrameCount = frames.GetFrameCount(_currentAttackAnim);
				_baseAttackAnimSpeed = (float)frames.GetAnimationSpeed(_currentAttackAnim);
				frames.SetAnimationLoop(_currentAttackAnim, weapon.AttackAnimationLoops);
				GD.Print($"PlayerController: Attack anim '{_currentAttackAnim}' has {_currentAttackFrameCount} frames at base speed {_baseAttackAnimSpeed}");
			}
			else if (!string.IsNullOrEmpty(_currentAttackAnim))
			{
				// Attack animation not found - weapon uses held sprite visual feedback only
				GD.Print($"PlayerController: No body attack animation for {weapon.DisplayName} (using held sprite visuals).");
				_currentAttackAnim = string.Empty;
			}

			// Set walk animation looping
			if (frames.HasAnimation(_currentWalkAnim))
			{
				frames.SetAnimationLoop(_currentWalkAnim, weapon.WalkAnimationLoops);
			}
			_bodySprite.Play(_currentWalkAnim);
		}

		CursorManager.Instance?.SetWeaponEquipped(true);
		ApplyHeldAnchorForAnimation(_bodySprite?.Animation.ToString() ?? _currentWalkAnim);
	}

	private void OnWeaponFired(WeaponData weapon)
	{
		if (weapon.IsMelee)
		{
			return;
		}

		_heldRecoilOffset.X = Mathf.Max(_heldRecoilOffset.X - HeldRecoilKick, -HeldRecoilMax);
		ApplyBodyFireScaleKick();
	}

	public override void _Process(double delta)
	{
		// Disable all player actions when inventory is open
		if (CursorManager.Instance?.IsInventoryOpen == true)
		{
			_currentMoveSpeed = 0f;
			UpdateAimDownSightsState(true);
			UpdateHitFlash((float)delta);
			UpdateStamina((float)delta);
			UpdateHeldItemOffset((float)delta);
			UpdateBodyScale((float)delta);
			return;
		}

		UpdateAimDownSightsState();
		RotateTowardsMouse();
		HandleFire();
		HandleWeaponSwitch();
		HandleReload();
		HandleHeal();
		HandleKick();
		UpdateKickDamage((float)delta);
		UpdateBodyAnimation((float)delta);
		UpdateHeldAnchorForCurrentAnimation();
		UpdateHeldItemOffset((float)delta);
		UpdateBodyScale((float)delta);
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

		var maxStamina = _statsManager?.MaxStamina ?? 100f;
		var staminaRegen = _statsManager?.StaminaRegen ?? 25f;

		if (CurrentStamina < maxStamina)
		{
			CurrentStamina = Mathf.Min(CurrentStamina + (staminaRegen * delta), maxStamina);
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
			return false;
		}

		CurrentStamina -= amount;
		_staminaRegenDelayTimer = StaminaRegenDelay;
		var maxStamina = _statsManager?.MaxStamina ?? 100f;
		GD.Print($"Consumed {amount} stamina. Remaining: {CurrentStamina:F1}/{maxStamina}");
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
		// Disable movement when inventory is open
		if (CursorManager.Instance?.IsInventoryOpen == true)
		{
			_currentMoveSpeed = 0f;
			Velocity = _knockbackVelocity;
			MoveAndSlide();
			ApplyKnockbackDamp((float)delta);
			return;
		}

		UpdateAimDownSightsState();
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

		if (_isAimingDownSights)
		{
			speedMultiplier *= AimDownSightsMoveSpeedMultiplier;
		}

		var moveSpeed = _statsManager?.MoveSpeed ?? 200f;
		_currentMoveSpeed = _isMoving ? moveSpeed * speedMultiplier : 0f;
		Velocity = (inputDir * moveSpeed * speedMultiplier) + _knockbackVelocity;
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

		// Get penalty from stats manager (already calculated from attributes)
		var backpedalPenalty = _statsManager?.BackpedalPenalty ?? 0.5f;

		// Apply penalty scaled by how far backward we're moving
		var finalPenalty = backpedalPenalty * penaltyFactor;

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

		// Get turn speed from stats manager (already calculated from attributes)
		var turnSpeed = _statsManager?.TurnSpeed ?? 20f;

		if (_isAimingDownSights)
		{
			turnSpeed *= AimDownSightsTurnSpeedMultiplier;
		}

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

		UpdateHeldSpriteRotation();
	}

	private void UpdateAimDownSightsState(bool forceDisable = false)
	{
		var canAim = !forceDisable
			&& Input.IsActionPressed("Aim")
			&& _weaponManager?.CurrentWeapon is { IsMelee: false };

		_isAimingDownSights = canAim;
		_weaponManager?.SetAimDownSights(canAim);
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
			if (_weaponManager.CurrentAmmo <= 0 && _attackPlaying && !string.IsNullOrEmpty(_currentAttackAnim) && _bodySprite?.Animation == _currentAttackAnim)
			{
				_attackPlaying = false;
				_weaponWalkTimer = WeaponWalkDuration;
				_bodySprite?.Play(_currentWalkAnim);
			}

			// Handle releasing fire button on automatic weapon
			if (!shouldFire && _attackPlaying && !string.IsNullOrEmpty(_currentAttackAnim) && _bodySprite?.Animation == _currentAttackAnim)
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
		if (!Input.IsActionJustPressed("SwitchHeld"))
		{
			return;
		}

		if (_kickPlaying || _attackPlaying)
		{
			GD.Print($"PlayerController: SwitchHeld blocked (kick={_kickPlaying}, attack={_attackPlaying}).");
			return;
		}

		if (_weaponManager is null)
		{
			GD.PrintErr("PlayerController: SwitchHeld pressed but WeaponManager is missing.");
			return;
		}

		var weaponCount = _weaponManager.GetWeapons().Count;
		if (weaponCount <= 1)
		{
			GD.Print($"PlayerController: SwitchHeld pressed but only {weaponCount} weapon(s) equipped.");
			return;
		}

		_weaponManager.CycleWeapon();
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
			return;
		}

		var maxHealth = _statsManager?.MaxHealth ?? 100f;
		if (CurrentHealth >= maxHealth)
		{
			return;
		}

		maxHealth = _statsManager?.MaxHealth ?? 100f;
		HealingItems--;
	var healedAmount = Mathf.Min(HealAmount, maxHealth - CurrentHealth);
	CurrentHealth = Mathf.Min(CurrentHealth + HealAmount, maxHealth);
	GD.Print($"Healed {healedAmount}! Health: {CurrentHealth}/{maxHealth} (Items left: {HealingItems})");
}

/// <summary>
/// Adds healing items to the player's inventory.
/// </summary>
public void AddHealingItems(int count) => HealingItems += count;

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
		var kickCost = _statsManager?.KickCost ?? 20f;
		if (!TryConsumeStamina(kickCost))
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


		foreach (var enemy in enemies)
		{
			if (enemy is not Node2D enemyNode)
			{
				continue;
			}

			var distance = kickOrigin.DistanceTo(enemyNode.GlobalPosition);

			if (distance > KickRange)
			{
				continue;
			}

			// Check if enemy is within the kick cone (45 degrees in front of player)
			var directionToEnemy = (enemyNode.GlobalPosition - kickOrigin).Normalized();
			var angleDifference = Mathf.Abs(_aimDirection.AngleTo(directionToEnemy));
			var maxAngle = Mathf.DegToRad(KickConeAngle / 2f); // Half angle on each side


			if (angleDifference > maxAngle)
			{
				continue;
			}

			// Apply damage
			if (enemy is IDamageable damageable)
			{
				var hitPos = enemyNode.GlobalPosition;
				var hitNormal = (enemyNode.GlobalPosition - kickOrigin).Normalized();
				damageable.ApplyDamage(KickDamage, kickOrigin, hitPos, hitNormal);
			}

			// 50% chance to inflict knockdown
			if (GD.Randf() < KickKnockdownChance && enemy is AI.ZombieController zombie)
			{
				zombie.ApplyKnockdown();
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
		if (weapon is null)
		{
			return;
		}

		// If no attack animation exists, weapon uses held sprite visuals only
		if (string.IsNullOrEmpty(_currentAttackAnim) || _currentAttackFrameCount <= 0)
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
		return name.StartsWith("attack_", System.StringComparison.Ordinal)
			|| name.StartsWith("mod_attack", System.StringComparison.Ordinal);
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
			// Hide held sprite during kick animation
			if (_heldSprite is not null)
			{
				_heldSprite.Visible = false;
			}
			return;
		}

		UpdateBodyWalkAnimationSpeed();

		// Show held sprite if there's a weapon (and it has a held sprite texture)
		var weapon = _weaponManager?.CurrentWeapon;
		if (_heldSprite is not null && weapon is not null)
		{
			_heldSprite.Visible = weapon.HeldSprite is not null;
		}

		// Update body animation (walk/idle)
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

		// Pause or play walk animation based on movement and looping preference
		var shouldLoopWalk = weapon?.WalkAnimationLoops ?? true;
		if (!_isMoving && !_attackPlaying && IsWalkAnimation(_bodySprite.Animation) && !shouldLoopWalk)
		{
			_bodySprite.Stop();
		}
		else if (_isMoving && !_attackPlaying && !shouldLoopWalk)
		{
			_bodySprite.Play();
		}
	}

	private void CacheBodyWalkBaseSpeeds()
	{
		_bodyWalkBaseSpeeds.Clear();
		if (_bodySprite?.SpriteFrames is not SpriteFrames frames)
		{
			return;
		}

		foreach (var animationName in frames.GetAnimationNames())
		{
			if (!IsModWalkAnimation(animationName))
			{
				continue;
			}

			var name = animationName.ToString();
			_bodyWalkBaseSpeeds[name] = (float)frames.GetAnimationSpeed(animationName);
		}
	}

	private void UpdateBodyWalkAnimationSpeed()
	{
		if (_bodySprite?.SpriteFrames is not SpriteFrames frames)
		{
			return;
		}

		var animation = _bodySprite.Animation;
		if (!IsModWalkAnimation(animation))
		{
			return;
		}

		var name = animation.ToString();
		if (!_bodyWalkBaseSpeeds.TryGetValue(name, out var baseSpeed))
		{
			baseSpeed = (float)frames.GetAnimationSpeed(animation);
			_bodyWalkBaseSpeeds[name] = baseSpeed;
		}

		if (_walkSpeedReference <= 0f)
		{
			return;
		}

		var speedScale = Mathf.Max(_currentMoveSpeed / _walkSpeedReference, 0f);
		frames.SetAnimationSpeed(animation, baseSpeed * speedScale);
	}

	private void BuildHeldAnchorLookup()
	{
		_heldAnchorLookup.Clear();
		foreach (var anchor in HeldAnimationAnchors)
		{
			if (anchor is null)
			{
				continue;
			}

			var name = anchor.AnimationName?.Trim();
			if (string.IsNullOrEmpty(name))
			{
				continue;
			}

			_heldAnchorLookup[name] = anchor;
		}
	}

	private void EnsureDefaultHeldAnchors()
	{
		if (HeldAnimationAnchors.Count > 0)
		{
			return;
		}

		HeldAnimationAnchors.Add(new HeldAnimationAnchor
		{
			AnimationName = "mod_walk_ranged_1h",
			Offset = new Vector2(6f, 0f),
			RotationDegrees = 0f
		});
		HeldAnimationAnchors.Add(new HeldAnimationAnchor
		{
			AnimationName = "mod_walk_ranged_2h",
			Offset = new Vector2(6f, 0f),
			RotationDegrees = 0f
		});
		HeldAnimationAnchors.Add(new HeldAnimationAnchor
		{
			AnimationName = "mod_walk_melee_2h",
			Offset = new Vector2(6f, 0f),
			RotationDegrees = 0f
		});
		HeldAnimationAnchors.Add(new HeldAnimationAnchor
		{
			AnimationName = "mod_attack_melee_2h",
			Offset = new Vector2(6f, 0f),
			RotationDegrees = 0f
		});
	}

	private void UpdateHeldAnchorForCurrentAnimation()
	{
		if (_bodySprite is null || _heldSprite is null || !_heldSprite.Visible || _kickPlaying)
		{
			return;
		}

		var animation = _bodySprite.Animation.ToString();
		if (animation == _lastHeldAnimation)
		{
			return;
		}

		ApplyHeldAnchorForAnimation(animation);
	}

	private void ApplyHeldAnchorForAnimation(string animationName)
	{
		if (_heldSprite is null)
		{
			return;
		}

		_lastHeldAnimation = animationName ?? string.Empty;

		var weapon = _weaponManager?.CurrentWeapon;
		var baseOffset = weapon?.HoldOffset ?? Vector2.Zero;
		_heldBaseRotationRad = 0f;

		if (!string.IsNullOrEmpty(animationName) && _heldAnchorLookup.TryGetValue(animationName, out var anchor))
		{
			baseOffset = anchor.Offset;
			_heldBaseRotationRad = Mathf.DegToRad(anchor.RotationDegrees);
		}
		else if (!string.IsNullOrEmpty(animationName) && _missingHeldAnchors.Add(animationName))
		{
			GD.PrintErr($"PlayerController: Missing held anchor for animation '{animationName}'. Using weapon HoldOffset.");
		}

		_heldBaseOffset = baseOffset;
		UpdateHeldSpritePosition();
		UpdateHeldSpriteRotation();
	}

	private void UpdateHeldSpriteRotation()
	{
		if (_heldSprite is null)
		{
			return;
		}

		if (_weaponManager?.CurrentWeapon?.SyncsWithBodyAnimation == false && _bodyNode is not null)
		{
			_heldSprite.Rotation = _heldBaseRotationRad - _bodyNode.Rotation;
			return;
		}

		_heldSprite.Rotation = _heldBaseRotationRad;
	}

	private void UpdateHeldItemOffset(float delta)
	{
		if (_heldSprite is null || !_heldSprite.Visible || _kickPlaying)
		{
			return;
		}

		UpdateHeldWiggle(delta);
		UpdateHeldRecoil(delta);
		UpdateHeldSpritePosition();
	}

	private void UpdateHeldWiggle(float delta)
	{
		if (_currentMoveSpeed <= 0.01f || _walkSpeedReference <= 0f)
		{
			_heldWiggleOffset = Vector2.Zero;
			return;
		}

		var speedScale = Mathf.Clamp(_currentMoveSpeed / _walkSpeedReference, 0f, 1.2f);
		_heldWiggleTime += delta * HeldWiggleFrequency * Mathf.Tau * speedScale;
		var amplitude = HeldWiggleAmplitude * speedScale;
		_heldWiggleOffset = new Vector2(0f, Mathf.Sin(_heldWiggleTime) * amplitude);
	}

	private void UpdateHeldRecoil(float delta)
	{
		if (_heldRecoilOffset == Vector2.Zero)
		{
			return;
		}

		_heldRecoilOffset = _heldRecoilOffset.MoveToward(Vector2.Zero, HeldRecoilReturnSpeed * delta);
	}

	private void ApplyBodyFireScaleKick()
	{
		if (_bodyScaleNode is null || BodyFireScalePixels <= 0f)
		{
			if (_bodyScaleNode is null)
			{
				GD.PrintErr("PlayerController: Body recoil scale node is missing.");
			}
			return;
		}

		var size = _cachedBodySpriteSize > 0f ? _cachedBodySpriteSize : 32f;
		var scaleKick = BodyFireScalePixels / size;
		_bodyScaleVelocity += new Vector2(scaleKick, -scaleKick);
		GD.Print($"PlayerController: Body recoil kick applied (pixels={BodyFireScalePixels:F2}, scaleKick={scaleKick:F4}).");
	}

	private void UpdateBodyScale(float delta)
	{
		if (_bodyScaleNode is null)
		{
			return;
		}

		if (_bodyScaleOffset == Vector2.Zero && _bodyScaleVelocity == Vector2.Zero)
		{
			_bodyScaleNode.Scale = _bodyBaseScale;
			return;
		}

		var accel = (-_bodyScaleOffset * BodyFireScaleSpring) - (_bodyScaleVelocity * BodyFireScaleDamping);
		_bodyScaleVelocity += accel * delta;
		_bodyScaleOffset += _bodyScaleVelocity * delta;

		_bodyScaleNode.Scale = _bodyBaseScale + _bodyScaleOffset;
	}

	private void UpdateHeldSpritePosition()
	{
		if (_heldSprite is null)
		{
			return;
		}

		_heldSprite.Position = _heldBaseOffset + _heldWiggleOffset + _heldRecoilOffset;
	}

	private void CacheBodyBaseScaleAndSize()
	{
		if (_bodyScaleNode is not null)
		{
			_bodyBaseScale = _bodyScaleNode.Scale;
		}

		var frames = _bodySprite?.SpriteFrames;
		if (frames is null || _bodySprite is null)
		{
			return;
		}

		var anim = _bodySprite.Animation;
		if (!frames.HasAnimation(anim))
		{
			return;
		}

		var texture = frames.GetFrameTexture(anim, 0);
		if (texture is null)
		{
			return;
		}

		var size = texture.GetSize();
		if (size.Y > 0f)
		{
			_cachedBodySpriteSize = size.Y;
		}
	}

	/// <summary>
	/// Checks if the animation name is a walk animation.
	/// Uses prefix matching to support any weapon type without hardcoding.
	/// </summary>
	private static bool IsWalkAnimation(StringName animation)
	{
		var name = animation.ToString();
		return name == "walk"
			|| name.StartsWith("walk_", System.StringComparison.Ordinal)
			|| name.StartsWith("mod_walk", System.StringComparison.Ordinal);
	}

	private static bool IsModWalkAnimation(StringName animation)
	{
		var name = animation.ToString();
		return name.StartsWith("mod_walk", System.StringComparison.Ordinal);
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
		var maxHealth = _statsManager?.MaxHealth ?? 100f;
		CurrentHealth -= amount;
		GD.Print($"Player took {amount} damage! Health: {CurrentHealth}/{maxHealth}");

		// Apply knockback away from damage source
		var knockbackDir = (GlobalPosition - fromPos).Normalized();
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

    private void Die() =>
        // For now, just restart by resetting health
        // Later this could trigger a death screen or respawn system
        CurrentHealth = _statsManager?.MaxHealth ?? 100f;
}
