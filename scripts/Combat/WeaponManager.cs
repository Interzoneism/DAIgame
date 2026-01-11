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
    /// Returns true if the weapon can fire (not on cooldown, has ammo, not reloading).
    /// </summary>
    public bool CanFire => _fireCooldown <= 0f && CurrentWeapon is not null && CurrentAmmo > 0 && !IsReloading;

    private readonly List<WeaponData> _weapons = [];
    private readonly List<int> _ammoInMagazine = [];
    private float _fireCooldown;
    private float _reloadTimer;
    private float _reloadDuration;
    private RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
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

        // Consume ammo
        _ammoInMagazine[CurrentWeaponIndex]--;
        EmitAmmoChanged();

        // Set cooldown based on fire rate
        _fireCooldown = 1f / weapon.FireRate;

        // Fire projectiles
        FireProjectiles(origin, direction, weapon);

        EmitSignal(SignalName.WeaponFired, weapon);
        return true;
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

        // Calculate spawn position with offset
        // X offset is forward (in direction), Y offset is perpendicular (left of direction)
        var forward = direction.Normalized();
        var perpendicular = new Vector2(-forward.Y, forward.X); // Rotated 90 degrees CCW
        var spawnPos = origin + (forward * weapon.SpawnOffsetX) + (perpendicular * weapon.SpawnOffsetY);

        var pelletCount = weapon.PelletCount;
        var spreadRad = Mathf.DegToRad(weapon.SpreadAngle);
        var baseAngle = direction.Angle();

        for (var i = 0; i < pelletCount; i++)
        {
            // Calculate spread angle for this pellet
            float pelletAngle;
            if (spreadRad > 0f && pelletCount > 1)
            {
                // Randomize within the spread cone
                pelletAngle = baseAngle + _rng.RandfRange(-spreadRad / 2f, spreadRad / 2f);
            }
            else if (spreadRad > 0f)
            {
                // Single pellet with spread (slight inaccuracy)
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
            bullet.Initialize(pelletDir);
        }

        GD.Print($"WeaponManager: Fired {pelletCount} projectile(s) from {weapon.DisplayName}");
    }

    /// <summary>
    /// Gets the list of all weapons for UI/inventory purposes.
    /// </summary>
    public IReadOnlyList<WeaponData> GetWeapons() => _weapons;

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
