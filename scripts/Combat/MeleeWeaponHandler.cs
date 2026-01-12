namespace DAIgame.Combat;

using System.Collections.Generic;
using DAIgame.Core;
using Godot;

/// <summary>
/// Handles melee weapon hit detection for both instant cone attacks and swing-based attacks.
/// Attach as a child of a node that has a WeaponManager and can provide position/aim direction.
/// </summary>
public partial class MeleeWeaponHandler : Node
{
    /// <summary>
    /// Signal emitted when a melee hit is registered.
    /// </summary>
    [Signal]
    public delegate void MeleeHitEventHandler(Node2D target, float damage, Vector2 hitPosition);

    /// <summary>
    /// Signal emitted when a swing attack starts.
    /// </summary>
    [Signal]
    public delegate void SwingStartedEventHandler(float swingDuration);

    /// <summary>
    /// Signal emitted when a swing attack ends.
    /// </summary>
    [Signal]
    public delegate void SwingEndedEventHandler();

    /// <summary>
    /// Current swing progress (0-1) for UI/debug purposes.
    /// </summary>
    public float SwingProgress { get; private set; }

    /// <summary>
    /// Returns true if currently in the middle of a swing attack.
    /// </summary>
    public bool IsSwinging { get; private set; }

    /// <summary>
    /// Returns true if waiting for damage delay before applying damage.
    /// </summary>
    public bool IsDamageDelayPending { get; private set; }

    private WeaponData? _currentWeapon;
    private Vector2 _swingOrigin;
    private Vector2 _swingAimDirection;
    private float _swingDuration;
    private float _swingTimer;
    private float _swingStartAngleRad;
    private float _swingEndAngleRad;
    private readonly HashSet<ulong> _hitTargetsThisSwing = [];

    // Damage delay state (for instant attacks with delay)
    private float _damageDelayTimer;
    private Vector2 _pendingDamageOrigin;
    private Vector2 _pendingDamageAimDirection;
    private WeaponData? _pendingDamageWeapon;

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        if (IsSwinging)
        {
            UpdateSwing(dt);
        }

        if (IsDamageDelayPending)
        {
            UpdateDamageDelay(dt);
        }
    }

    /// <summary>
    /// Performs an instant melee attack - hits all targets within the cone immediately.
    /// If weapon has DamageDelay > 0, delays the hit registration.
    /// </summary>
    /// <param name="origin">World position of the attacker.</param>
    /// <param name="aimDirection">Direction the attacker is facing (normalized).</param>
    /// <param name="weapon">Melee weapon data.</param>
    public void PerformInstantAttack(Vector2 origin, Vector2 aimDirection, WeaponData weapon)
    {
        if (!weapon.IsMelee)
        {
            GD.PrintErr("MeleeWeaponHandler: PerformInstantAttack called with non-melee weapon!");
            return;
        }

        // If there's a damage delay, queue the damage for later
        if (weapon.DamageDelay > 0f)
        {
            _pendingDamageOrigin = origin;
            _pendingDamageAimDirection = aimDirection;
            _pendingDamageWeapon = weapon;
            _damageDelayTimer = weapon.DamageDelay;
            IsDamageDelayPending = true;
            GD.Print($"MeleeWeaponHandler: Instant attack delayed by {weapon.DamageDelay:F2}s");
            return;
        }

        // No delay - apply damage immediately
        ApplyInstantDamage(origin, aimDirection, weapon);
    }

    private void UpdateDamageDelay(float delta)
    {
        _damageDelayTimer -= delta;
        if (_damageDelayTimer <= 0f && _pendingDamageWeapon is not null)
        {
            IsDamageDelayPending = false;

            // Check if this is for a swing attack or instant attack
            if (_pendingDamageWeapon.MeleeHitType == MeleeHitType.Swing && _currentWeapon is not null)
            {
                // Start the swing now that delay is complete
                _swingOrigin = _pendingDamageOrigin;
                _swingAimDirection = _pendingDamageAimDirection;
                BeginSwing();
            }
            else
            {
                // Instant attack - apply damage now
                ApplyInstantDamage(_pendingDamageOrigin, _pendingDamageAimDirection, _pendingDamageWeapon);
            }
            _pendingDamageWeapon = null;
        }
    }

    /// <summary>
    /// Updates the pending damage origin (call this each frame if attacker moves during delay).
    /// </summary>
    public void UpdatePendingDamageOrigin(Vector2 newOrigin, Vector2 newAimDirection)
    {
        if (IsDamageDelayPending)
        {
            _pendingDamageOrigin = newOrigin;
            _pendingDamageAimDirection = newAimDirection;
        }
    }

    private void ApplyInstantDamage(Vector2 origin, Vector2 aimDirection, WeaponData weapon)
    {
        var halfSpreadRad = Mathf.DegToRad(weapon.MeleeSpreadAngle / 2f);
        var targets = FindTargetsInCone(origin, aimDirection, weapon.MeleeRange, halfSpreadRad);

        GD.Print($"MeleeWeaponHandler: Instant attack - found {targets.Count} targets in cone ({weapon.MeleeSpreadAngle}° spread, {weapon.MeleeRange}px range)");

        foreach (var target in targets)
        {
            ApplyDamageToTarget(target, origin, weapon);
        }
    }

    /// <summary>
    /// Starts a swing attack that registers hits over time as the weapon arcs.
    /// If weapon has DamageDelay > 0, delays the start of hit registration.
    /// </summary>
    /// <param name="origin">World position of the attacker.</param>
    /// <param name="aimDirection">Direction the attacker is facing (normalized).</param>
    /// <param name="weapon">Melee weapon data.</param>
    /// <param name="attackDuration">Duration of the attack animation in seconds.</param>
    public void StartSwingAttack(Vector2 origin, Vector2 aimDirection, WeaponData weapon, float attackDuration)
    {
        if (!weapon.IsMelee || weapon.MeleeHitType != MeleeHitType.Swing)
        {
            GD.PrintErr("MeleeWeaponHandler: StartSwingAttack called with non-swing weapon!");
            return;
        }

        _currentWeapon = weapon;
        _swingOrigin = origin;
        _swingAimDirection = aimDirection;

        // Adjust swing duration to account for damage delay
        // The swing starts after the delay, so effective swing time is reduced
        var effectiveSwingDuration = attackDuration - weapon.DamageDelay;
        if (effectiveSwingDuration <= 0f)
        {
            effectiveSwingDuration = attackDuration; // Fallback if delay is too long
        }
        _swingDuration = effectiveSwingDuration;

        // If there's a damage delay, the swing starts delayed
        if (weapon.DamageDelay > 0f)
        {
            _damageDelayTimer = weapon.DamageDelay;
            IsDamageDelayPending = true;
            _pendingDamageWeapon = weapon;
            _pendingDamageOrigin = origin;
            _pendingDamageAimDirection = aimDirection;
            GD.Print($"MeleeWeaponHandler: Swing delayed by {weapon.DamageDelay:F2}s, then swing for {effectiveSwingDuration:F2}s");
        }
        else
        {
            // No delay - start swinging immediately
            BeginSwing();
        }

        EmitSignal(SignalName.SwingStarted, attackDuration);
    }

    private void BeginSwing()
    {
        if (_currentWeapon is null)
        {
            return;
        }

        _swingTimer = 0f;

        // Convert swing angles from degrees to radians
        // Positive = clockwise (in Godot's coordinate system where Y is down)
        _swingStartAngleRad = Mathf.DegToRad(_currentWeapon.SwingStartAngle);
        _swingEndAngleRad = Mathf.DegToRad(_currentWeapon.SwingEndAngle);

        _hitTargetsThisSwing.Clear();
        IsSwinging = true;
        SwingProgress = 0f;

        GD.Print($"MeleeWeaponHandler: Swing started - {_currentWeapon.SwingStartAngle}° to {_currentWeapon.SwingEndAngle}° over {_swingDuration:F2}s");
    }

    /// <summary>
    /// Updates the swing origin position (call this each frame during swing if attacker moves).
    /// </summary>
    public void UpdateSwingOrigin(Vector2 newOrigin)
    {
        _swingOrigin = newOrigin;
    }

    /// <summary>
    /// Cancels an in-progress swing attack (e.g., if player is stunned).
    /// </summary>
    public void CancelSwing()
    {
        if (IsSwinging)
        {
            IsSwinging = false;
            SwingProgress = 0f;
            _hitTargetsThisSwing.Clear();
            EmitSignal(SignalName.SwingEnded);
            GD.Print("MeleeWeaponHandler: Swing cancelled");
        }
    }

    private void UpdateSwing(float delta)
    {
        if (_currentWeapon is null)
        {
            IsSwinging = false;
            return;
        }

        _swingTimer += delta;
        SwingProgress = Mathf.Clamp(_swingTimer / _swingDuration, 0f, 1f);

        // Calculate current swing angle based on progress
        var currentAngleOffset = Mathf.Lerp(_swingStartAngleRad, _swingEndAngleRad, SwingProgress);
        var baseAngle = _swingAimDirection.Angle();
        var currentSwingAngle = baseAngle + currentAngleOffset;
        var currentSwingDir = Vector2.FromAngle(currentSwingAngle);

        // Check for hits at current swing position
        // Use a narrow cone at the current swing angle (represents the weapon arc at this moment)
        var swingSliceAngle = Mathf.DegToRad(15f); // Small angle for the current hit detection slice
        var targets = FindTargetsInCone(_swingOrigin, currentSwingDir, _currentWeapon.MeleeRange, swingSliceAngle);

        foreach (var target in targets)
        {
            // Only hit each target once per swing
            if (_hitTargetsThisSwing.Contains(target.GetInstanceId()))
            {
                continue;
            }

            _hitTargetsThisSwing.Add(target.GetInstanceId());
            ApplyDamageToTarget(target, _swingOrigin, _currentWeapon);
        }

        // End swing when duration is complete
        if (_swingTimer >= _swingDuration)
        {
            IsSwinging = false;
            SwingProgress = 0f;
            _hitTargetsThisSwing.Clear();
            EmitSignal(SignalName.SwingEnded);
            GD.Print("MeleeWeaponHandler: Swing ended");
        }
    }

    private List<Node2D> FindTargetsInCone(Vector2 origin, Vector2 direction, float range, float halfAngleRad)
    {
        var result = new List<Node2D>();
        var targets = GetTree().GetNodesInGroup("damageable");

        foreach (var node in targets)
        {
            if (node is not Node2D target)
            {
                continue;
            }

            // Skip self (player shouldn't hit themselves)
            if (target == GetParent() || target == GetParent()?.GetParent())
            {
                continue;
            }

            var toTarget = target.GlobalPosition - origin;
            var distance = toTarget.Length();

            if (distance > range)
            {
                continue;
            }

            // Check if target is within the cone angle
            var angleToTarget = toTarget.Normalized().Angle();
            var aimAngle = direction.Angle();
            var angleDiff = Mathf.Abs(Mathf.AngleDifference(aimAngle, angleToTarget));

            if (angleDiff <= halfAngleRad)
            {
                result.Add(target);
            }
        }

        return result;
    }

    private void ApplyDamageToTarget(Node2D target, Vector2 origin, WeaponData weapon)
    {
        if (target is IDamageable damageable)
        {
            var hitPos = target.GlobalPosition;
            var hitNormal = (hitPos - origin).Normalized();
            damageable.ApplyDamage(weapon.Damage, origin, hitPos, hitNormal);
            GD.Print($"MeleeWeaponHandler: Hit {target.Name} for {weapon.Damage} damage");

            // Apply knockback to any knockbackable target (zombies, destructibles, etc.)
            if (weapon.Knockback > 0f && target is IKnockbackable knockbackable)
            {
                knockbackable.ApplyKnockback(hitNormal, weapon.Knockback);
                GD.Print($"MeleeWeaponHandler: Applied {weapon.Knockback} knockback to {target.Name}");
            }

            EmitSignal(SignalName.MeleeHit, target, weapon.Damage, hitPos);
        }
    }
}
