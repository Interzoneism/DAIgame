namespace DAIgame.Player;

using DAIgame.Combat;
using Godot;

/// <summary>
/// Hotline Miami-style player controller with snappy WASD movement and mouse aim.
/// Attach to a CharacterBody2D node.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
  /// <summary>
  /// Movement speed in pixels per second.
  /// </summary>
  [Export]
  public float MoveSpeed { get; set; } = 200f;

  /// <summary>
  /// Force applied backwards when firing.
  /// </summary>
  [Export]
  public float KnockbackStrength { get; set; } = 100f;

  /// <summary>
  /// Rate at which knockback is damped per second.
  /// </summary>
  [Export]
  public float KnockbackDamp { get; set; } = 350f;

  /// <summary>
  /// Duration the shotgun walk animation stays active after an attack.
  /// </summary>
  [Export]
  public float WalkShotgunDuration { get; set; } = 2f;

  /// <summary>
  /// Maximum health of the player.
  /// </summary>
  [Export]
  public float MaxHealth { get; set; } = 100f;

  /// <summary>
  /// Current health of the player.
  /// </summary>
  public float CurrentHealth { get; private set; }

  private Node2D? _bodyNode;
  private AnimatedSprite2D? _bodySprite;
  private Node2D? _legsNode;
  private AnimatedSprite2D? _legsSprite;
  private HitscanWeapon? _weapon;
  private Vector2 _lastMoveDir = Vector2.Right;
  private Vector2 _aimDirection = Vector2.Right;
  private Vector2 _knockbackVelocity = Vector2.Zero;
  private float _walkShotgunTimer;
  private bool _attackPlaying;
  private bool _isMoving;

  public override void _Ready()
  {
    // Add to player group for easy lookup
    AddToGroup("player");
    AddToGroup("damageable");

    CurrentHealth = MaxHealth;

    _bodyNode = GetNodeOrNull<Node2D>("Body");
    _bodySprite = _bodyNode?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
    _legsNode = GetNodeOrNull<Node2D>("Legs");
    _weapon = GetNodeOrNull<HitscanWeapon>("HitscanWeapon");
    _legsSprite = _legsNode?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

    if (_bodySprite is not null)
    {
      _bodySprite.AnimationFinished += OnBodyAnimationFinished;

      var frames = _bodySprite.SpriteFrames;
      if (frames is not null && frames.HasAnimation("attack_shotgun"))
      {
        frames.SetAnimationLoop("attack_shotgun", false);
      }

      _bodySprite.Play("walk");
    }

    _legsSprite?.Play("walk");
  }

  public override void _Process(double delta)
  {
    RotateTowardsMouse();
    HandleFire();
    UpdateBodyAnimation((float)delta);
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
    Velocity = (inputDir * MoveSpeed) + _knockbackVelocity;
    MoveAndSlide();

    UpdateLegsFacing(inputDir);
  }

  /// <summary>
  /// Rotates the player body to face the mouse cursor.
  /// Updates every frame for responsive aiming.
  /// </summary>
  private void RotateTowardsMouse()
  {
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
    if (!Input.IsActionJustPressed("Fire"))
    {
      return;
    }

    StartAttackAnimation();

    // Fire the hitscan weapon
    if (_weapon is not null)
    {
      _weapon.Fire(GlobalPosition, _aimDirection);
    }
    ApplyKnockbackImpulse();
  }

  private void StartAttackAnimation()
  {
    if (_bodySprite is null)
    {
      return;
    }

    _attackPlaying = true;
    _walkShotgunTimer = 0f;
    _bodySprite.Stop();
    _bodySprite.Play("attack_shotgun");
  }

  private void OnBodyAnimationFinished()
  {
    if (_bodySprite is null || _bodySprite.Animation != "attack_shotgun")
    {
      return;
    }

    _attackPlaying = false;
    _walkShotgunTimer = WalkShotgunDuration;
    _bodySprite.Play("walk_shotgun");
  }

  private void UpdateBodyAnimation(float delta)
  {
    if (_bodySprite is null)
    {
      return;
    }

    if (_walkShotgunTimer > 0f)
    {
      _walkShotgunTimer -= delta;
      if (_walkShotgunTimer <= 0f && !_attackPlaying)
      {
        _bodySprite.Play("walk");
      }
    }
    else if (!_attackPlaying && _bodySprite.Animation != "walk")
    {
      _bodySprite.Play("walk");
    }

    // Pause walk animation when not moving
    if (!_isMoving && !_attackPlaying && (_bodySprite.Animation == "walk" || _bodySprite.Animation == "walk_shotgun"))
    {
      _bodySprite.Stop();
    }
    else if (_isMoving && !_attackPlaying)
    {
      _bodySprite.Play();
    }
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

    // Pause legs animation when not moving
    if (_legsSprite is not null)
    {
      if (!_isMoving)
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

  private void ApplyKnockbackImpulse()
  {
    _knockbackVelocity = -_aimDirection * KnockbackStrength;
  }

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

    if (CurrentHealth <= 0f)
    {
      Die();
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
