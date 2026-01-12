namespace DAIgame.Combat;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Manages the player's equipped weapons, handles firing, switching, and ammo.
/// Designed to work with inventory system later.
/// </summary>
public partial class WeaponManager : Node2D
{
    /// <summary>
    /// Signal emitted when the equipped weapon changes.
    /// </summary>
    [Signal]
    public delegate void WeaponChangedEventHandler(WeaponData? weapon);

    /// <summary>
    /// Signal emitted when the weapon fires.
    /// </summary>
    [Signal]
    public delegate void WeaponFiredEventHandler(WeaponData weapon);

    /// <summary>
    /// Signal emitted when a melee attack is triggered (instant or swing start).
    /// Includes attack duration for swing synchronization.
    /// </summary>
    [Signal]
    public delegate void MeleeAttackTriggeredEventHandler(WeaponData weapon, float attackDuration);

    /// <summary>
    /// Signal emitted when ammo count changes.
    /// </summary>
    [Signal]
    public delegate void AmmoChangedEventHandler(int currentAmmo, int magazineSize);

    /// <summary>
    /// Signal emitted when reload state changes.
    /// </summary>
    [Signal]
    public delegate void ReloadStateChangedEventHandler(bool isReloading, float progress);

    /// <summary>
    /// Bullet scene to instantiate when firing.
    /// </summary>
    [Export]
    public PackedScene? BulletScene { get; set; }

    /// <summary>
    /// Initial weapons to equip (for testing, will be replaced by inventory later).
    /// </summary>
    [Export]
    public Godot.Collections.Array<WeaponData> StartingWeapons { get; set; } = [];

    /// <summary>
    /// Currently equipped weapon data.
    /// </summary>
    public WeaponData? CurrentWeapon => CurrentWeaponIndex >= 0 && CurrentWeaponIndex < _weapons.Count
        ? _weapons[CurrentWeaponIndex]
        : null;

    /// <summary>
    /// Index of current weapon in the weapons list.
    /// </summary>
    public int CurrentWeaponIndex { get; private set; } = -1;

    /// <summary>
    /// Current ammo in the magazine for the equipped weapon.
    /// </summary>
    public int CurrentAmmo => CurrentWeaponIndex >= 0 && CurrentWeaponIndex < _ammoInMagazine.Count
        ? _ammoInMagazine[CurrentWeaponIndex]
        : 0;

    /// <summary>
    /// Returns true if currently reloading.
    /// </summary>
    public bool IsReloading { get; private set; }

    /// <summary>
    /// Reload progress (0-1) for UI display.
    /// </summary>
    public float ReloadProgress { get; private set; }

    /// <summary>
    /// Returns true if the weapon can fire (not on cooldown, has ammo for ranged, not reloading).
    /// Melee weapons don't require ammo.
    /// </summary>
    public bool CanFire
    {
        get
        {
            if (_fireCooldown > 0f || CurrentWeapon is null)
            {
                return false;
            }

            // Melee weapons don't need ammo
            if (CurrentWeapon.IsMelee)
            {
                return true;
            }

            // Ranged weapons need ammo and not reloading
            return CurrentAmmo > 0 && !IsReloading;
        }
    }

    /// <summary>
    /// Gets the MeleeWeaponHandler child node if available.
    /// </summary>
    public MeleeWeaponHandler? MeleeHandler { get; private set; }

    /// <summary>
    /// Current accuracy penalty from accumulated recoil (0 = no penalty, grows with each shot).
    /// </summary>
    public float CurrentRecoilPenalty { get; private set; }

    /// <summary>
    /// Number of shots fired within the recovery window (for warmup tracking).
    /// </summary>
    public int ShotsFiredInBurst { get; private set; }

    private readonly List<WeaponData> _weapons = [];
    private readonly List<int> _ammoInMagazine = [];
    private float _fireCooldown;
    private float _reloadTimer;
    private float _reloadDuration;
    private float _recoilRecoveryTimer;
    private RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        // Create melee handler as a child node
        MeleeHandler = new MeleeWeaponHandler();
        AddChild(MeleeHandler);

        // Add starting weapons for testing
        foreach (var weapon in StartingWeapons)
        {
            if (weapon is not null)
            {
                AddWeapon(weapon);
            }
        }

        // Equip first weapon if available
        if (_weapons.Count > 0)
        {
            CurrentWeaponIndex = 0;
            EmitSignal(SignalName.WeaponChanged, CurrentWeapon!);
            EmitAmmoChanged();
            GD.Print($"WeaponManager: Equipped {CurrentWeapon?.DisplayName ?? "nothing"}");
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        if (_fireCooldown > 0f)
        {
            _fireCooldown -= dt;
        }

        UpdateReload(dt);
        UpdateRecoilRecovery(dt);
    }

    private void UpdateRecoilRecovery(float delta)
    {
        if (_recoilRecoveryTimer <= 0f)
        {
            return;
        }

        _recoilRecoveryTimer -= delta;

        if (_recoilRecoveryTimer <= 0f)
        {
            // Reset recoil state when recovery timer expires
            CurrentRecoilPenalty = 0f;
            ShotsFiredInBurst = 0;
        }
    }

    private void UpdateRecoilOnFire(WeaponData weapon)
    {
        // Reset recovery timer on each shot
        _recoilRecoveryTimer = weapon.RecoilRecovery;

        // Increment shot counter
        ShotsFiredInBurst++;

        // Apply recoil penalty only after warmup shots
        if (ShotsFiredInBurst > weapon.RecoilWarmup)
        {
            CurrentRecoilPenalty += weapon.Recoil;
        }
    }

    private void UpdateReload(float delta)
    {
        if (!IsReloading)
        {
            return;
        }

        _reloadTimer -= delta;
        ReloadProgress = 1f - (_reloadTimer / _reloadDuration);
        EmitSignal(SignalName.ReloadStateChanged, true, ReloadProgress);

        if (_reloadTimer <= 0f)
        {
            CompleteReloadStep();
        }
    }

    private void CompleteReloadStep()
    {
        var weapon = CurrentWeapon;
        if (weapon is null || CurrentWeaponIndex < 0)
        {
            IsReloading = false;
            return;
        }

        if (weapon.ReloadMode == WeaponReloadMode.ShellByShell)
        {
            // Add one shell
            _ammoInMagazine[CurrentWeaponIndex]++;
            EmitAmmoChanged();
            GD.Print($"WeaponManager: Loaded shell, ammo now {CurrentAmmo}/{weapon.MagazineSize}");

            // Check if magazine is full
            if (_ammoInMagazine[CurrentWeaponIndex] >= weapon.MagazineSize)
            {
                FinishReload();
            }
            else
            {
                // Continue reloading next shell
                _reloadTimer = weapon.ReloadTime;
                _reloadDuration = weapon.ReloadTime;
                ReloadProgress = 0f;
            }
        }
        else
        {
            // Magazine reload - fill to max
            _ammoInMagazine[CurrentWeaponIndex] = weapon.MagazineSize;
            FinishReload();
        }
    }

    private void FinishReload()
    {
        IsReloading = false;
        ReloadProgress = 0f;
        EmitSignal(SignalName.ReloadStateChanged, false, 0f);
        EmitAmmoChanged();
        GD.Print($"WeaponManager: Reload complete, ammo {CurrentAmmo}/{CurrentWeapon?.MagazineSize ?? 0}");
    }

    private void EmitAmmoChanged()
    {
        var weapon = CurrentWeapon;
        if (weapon is not null)
        {
            EmitSignal(SignalName.AmmoChanged, CurrentAmmo, weapon.MagazineSize);
        }
    }

    /// <summary>
    /// Adds a weapon to the available weapons list.
    /// </summary>
    public void AddWeapon(WeaponData weapon)
    {
        _weapons.Add(weapon);
        _ammoInMagazine.Add(weapon.MagazineSize); // Start with full magazine
        GD.Print($"WeaponManager: Added weapon {weapon.DisplayName} with {weapon.MagazineSize} ammo");

        // Auto-equip if this is the first weapon
        if (_weapons.Count == 1)
        {
            CurrentWeaponIndex = 0;
            EmitSignal(SignalName.WeaponChanged, CurrentWeapon!);
            EmitAmmoChanged();
        }
    }

    /// <summary>
    /// Removes a weapon from the available weapons list.
    /// </summary>
    public bool RemoveWeapon(WeaponData weapon)
    {
        var index = _weapons.IndexOf(weapon);
        if (index < 0)
        {
            return false;
        }

        _weapons.RemoveAt(index);
        _ammoInMagazine.RemoveAt(index);

        // Cancel reload if removing current weapon
        if (index == CurrentWeaponIndex)
        {
            CancelReload();
        }

        // Adjust current index if needed
        if (CurrentWeaponIndex >= _weapons.Count)
        {
            CurrentWeaponIndex = _weapons.Count - 1;
        }

        EmitSignal(SignalName.WeaponChanged, CurrentWeapon!);
        EmitAmmoChanged();
        return true;
    }

    private void CancelReload()
    {
        if (IsReloading)
        {
            IsReloading = false;
            ReloadProgress = 0f;
            EmitSignal(SignalName.ReloadStateChanged, false, 0f);
            GD.Print("WeaponManager: Reload cancelled");
        }
    }

    /// <summary>
    /// Cycles to the next weapon in the list.
    /// </summary>
    public void CycleWeapon()
    {
        if (_weapons.Count <= 1)
        {
            return;
        }

        CancelReload();
        CurrentWeaponIndex = (CurrentWeaponIndex + 1) % _weapons.Count;
        EmitSignal(SignalName.WeaponChanged, CurrentWeapon!);
        EmitAmmoChanged();
        GD.Print($"WeaponManager: Switched to {CurrentWeapon?.DisplayName ?? "nothing"} ({CurrentAmmo}/{CurrentWeapon?.MagazineSize})");
    }

    /// <summary>
    /// Starts reloading the current weapon. For shell-by-shell, can be interrupted by firing.
    /// </summary>
    public void StartReload()
    {
        var weapon = CurrentWeapon;
        if (weapon is null || CurrentWeaponIndex < 0)
        {
            return;
        }

        // Already reloading or magazine is full
        if (IsReloading || _ammoInMagazine[CurrentWeaponIndex] >= weapon.MagazineSize)
        {
            return;
        }

        IsReloading = true;
        _reloadTimer = weapon.ReloadTime;
        _reloadDuration = weapon.ReloadTime;
        ReloadProgress = 0f;
        EmitSignal(SignalName.ReloadStateChanged, true, 0f);
        GD.Print($"WeaponManager: Started reload for {weapon.DisplayName} ({weapon.ReloadMode})");
    }

    /// <summary>
    /// Interrupts shell-by-shell reload to fire immediately. Returns true if reload was interrupted.
    /// </summary>
    public bool TryInterruptReload()
    {
        if (!IsReloading)
        {
            return false;
        }

        var weapon = CurrentWeapon;
        if (weapon is null)
        {
            return false;
        }

        // Only shell-by-shell can be interrupted, and only if we have ammo
        if (weapon.ReloadMode == WeaponReloadMode.ShellByShell && CurrentAmmo > 0)
        {
            CancelReload();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to fire the current weapon. Returns true if fired.
    /// </summary>
    /// <param name="origin">World position to fire from.</param>
    /// <param name="direction">Direction to fire in (normalized).</param>
    /// <returns>True if the weapon fired, false if on cooldown or no weapon.</returns>
    public bool TryFire(Vector2 origin, Vector2 direction)
    {
        // Try to interrupt shell-by-shell reload to fire
        if (IsReloading)
        {
            if (!TryInterruptReload())
            {
                return false; // Can't interrupt magazine reload
            }
        }

        if (!CanFire || CurrentWeapon is null || CurrentWeaponIndex < 0)
        {
            return false;
        }

        var weapon = CurrentWeapon;

        // Set cooldown based on fire rate
        _fireCooldown = weapon.TimeBetweenShots;

        // Handle melee vs ranged weapons
        if (weapon.IsMelee)
        {
            PerformMeleeAttack(origin, direction, weapon);
        }
        else
        {
            // Consume ammo for ranged weapons
            _ammoInMagazine[CurrentWeaponIndex]--;
            EmitAmmoChanged();

            // Fire projectiles
            FireProjectiles(origin, direction, weapon);
            EmitSignal(SignalName.WeaponFired, weapon);
        }

        return true;
    }

    /// <summary>
    /// Performs a melee attack using the MeleeWeaponHandler.
    /// </summary>
    private void PerformMeleeAttack(Vector2 origin, Vector2 direction, WeaponData weapon)
    {
        if (MeleeHandler is null)
        {
            GD.PrintErr("WeaponManager: MeleeHandler is null, cannot perform melee attack!");
            return;
        }

        // Set up exclusions so the attacker doesn't hit themselves
        SetupMeleeExclusions();

        var attackDuration = weapon.TimeBetweenShots;

        if (weapon.MeleeHitType == MeleeHitType.Instant)
        {
            MeleeHandler.PerformInstantAttack(origin, direction, weapon);
        }
        else // Swing
        {
            MeleeHandler.StartSwingAttack(origin, direction, weapon, attackDuration);
        }

        EmitSignal(SignalName.MeleeAttackTriggered, weapon, attackDuration);
        GD.Print($"WeaponManager: Melee attack with {weapon.DisplayName} ({weapon.MeleeHitType})");
    }

    /// <summary>
    /// Sets up melee exclusions based on the parent node (attacker).
    /// </summary>
    private void SetupMeleeExclusions()
    {
        if (MeleeHandler is null)
        {
            return;
        }

        var parent = GetParent();
        if (parent is CharacterBody2D charBody)
        {
            // Build list of nodes to exclude (attacker and all children)
            var excludeNodes = new HashSet<Node2D> { charBody };
            CollectChildNode2Ds(charBody, excludeNodes);
            MeleeHandler.SetAttackerExclusions(charBody.GetRid(), excludeNodes);
        }
        else if (parent is Node2D node2D)
        {
            var excludeNodes = new HashSet<Node2D> { node2D };
            CollectChildNode2Ds(node2D, excludeNodes);
            // For non-physics bodies, we pass an invalid RID but still exclude nodes
            MeleeHandler.SetAttackerExclusions(new Rid(), excludeNodes);
        }
    }

    /// <summary>
    /// Recursively collects all Node2D children into the set.
    /// </summary>
    private static void CollectChildNode2Ds(Node parent, HashSet<Node2D> set)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Node2D node2D)
            {
                set.Add(node2D);
            }
            CollectChildNode2Ds(child, set);
        }
    }

    /// <summary>
    /// Checks if the weapon should continue firing (for automatic weapons).
    /// </summary>
    public bool ShouldContinueFiring() => CurrentWeapon?.FireMode == WeaponFireMode.Automatic;

    private void FireProjectiles(Vector2 origin, Vector2 direction, WeaponData weapon)
    {
        if (BulletScene is null)
        {
            GD.PrintErr("WeaponManager: BulletScene is not set!");
            return;
        }

        // Update recoil state before firing
        UpdateRecoilOnFire(weapon);

        // Calculate spawn position with offset
        // X offset is forward (in direction), Y offset is perpendicular (left of direction)
        var forward = direction.Normalized();
        var perpendicular = new Vector2(-forward.Y, forward.X); // Rotated 90 degrees CCW
        var spawnPos = origin + (forward * weapon.SpawnOffsetX) + (perpendicular * weapon.SpawnOffsetY);

        var pelletCount = weapon.PelletCount;

        // Calculate effective spread: base stability + accumulated recoil penalty
        // Stability is the base inaccuracy in degrees, recoil adds more inaccuracy per shot
        var effectiveSpreadDeg = weapon.Stability + CurrentRecoilPenalty;
        // Also add weapon's SpreadAngle for shotgun-style spread on top
        var totalSpreadDeg = effectiveSpreadDeg + weapon.SpreadAngle;
        var spreadRad = Mathf.DegToRad(totalSpreadDeg);
        var baseAngle = direction.Angle();

        for (var i = 0; i < pelletCount; i++)
        {
            // Calculate spread angle for this pellet
            float pelletAngle;
            if (spreadRad > 0f)
            {
                // Randomize within the spread cone
                pelletAngle = baseAngle + _rng.RandfRange(-spreadRad / 2f, spreadRad / 2f);
            }
            else
            {
                // No spread
                pelletAngle = baseAngle;
            }

            var pelletDir = Vector2.FromAngle(pelletAngle);

            var bullet = BulletScene.Instantiate<Projectile>();
            GetTree().Root.AddChild(bullet);
            bullet.GlobalPosition = spawnPos;
            bullet.Damage = weapon.Damage;
            bullet.Speed = weapon.ProjectileSpeed;
            bullet.Knockback = weapon.Knockback;
            bullet.Initialize(pelletDir);
        }

        GD.Print($"WeaponManager: Fired {pelletCount} projectile(s) from {weapon.DisplayName} (spread: {totalSpreadDeg:F1}° = stability {weapon.Stability:F1}° + recoil {CurrentRecoilPenalty:F1}° + weapon spread {weapon.SpreadAngle:F1}°)");
    }

    /// <summary>
    /// Gets the list of all weapons for UI/inventory purposes.
    /// </summary>
    public IReadOnlyList<WeaponData> GetWeapons() => _weapons;

    /// <summary>
    /// Replaces the current weapons list with the provided list and equips the given index.
    /// Intended for inventory-driven equipment.
    /// </summary>
    public void SetWeapons(IReadOnlyList<WeaponData> weapons, int equipIndex)
    {
        CancelReload();
        _weapons.Clear();
        _ammoInMagazine.Clear();
        CurrentWeaponIndex = -1;
        IsReloading = false;
        ReloadProgress = 0f;
        _fireCooldown = 0f;
        _reloadTimer = 0f;
        _reloadDuration = 0f;
        _recoilRecoveryTimer = 0f;
        CurrentRecoilPenalty = 0f;
        ShotsFiredInBurst = 0;

        foreach (var weapon in weapons)
        {
            if (weapon is null)
            {
                continue;
            }

            _weapons.Add(weapon);
            _ammoInMagazine.Add(weapon.MagazineSize);
        }

        if (_weapons.Count == 0)
        {
            EmitSignal(SignalName.WeaponChanged, null);
            return;
        }

        CurrentWeaponIndex = Mathf.Clamp(equipIndex, 0, _weapons.Count - 1);
        EmitSignal(SignalName.WeaponChanged, CurrentWeapon!);
        EmitAmmoChanged();
        GD.Print($"WeaponManager: Equipped {CurrentWeapon?.DisplayName ?? "nothing"}");
    }

    /// <summary>
    /// Equips a weapon by index.
    /// </summary>
    public void EquipWeaponByIndex(int index)
    {
        if (index < 0 || index >= _weapons.Count)
        {
            return;
        }

        CancelReload();
        CurrentWeaponIndex = index;
        EmitSignal(SignalName.WeaponChanged, CurrentWeapon!);
        EmitAmmoChanged();
        GD.Print($"WeaponManager: Equipped {CurrentWeapon?.DisplayName ?? "nothing"}");
    }
}
