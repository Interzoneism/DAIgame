namespace DAIgame.Combat;

using System.Collections.Generic;
using DAIgame.Core;
using DAIgame.Player;
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
    /// Includes the actual firing direction (after spread/recoil) for visuals.
    /// </summary>
    [Signal]
    public delegate void WeaponFiredEventHandler(WeaponData weapon, Vector2 fireDirection);

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
    /// Global recoil multiplier applied to all weapons.
    /// </summary>
    [Export]
    public float RecoilMultiplier { get; set; } = 1f;

    /// <summary>
    /// Additional recoil multiplier applied while aiming down sights.
    /// </summary>
    [Export]
    public float AimDownSightsRecoilMultiplier { get; set; } = 1f;

    /// <summary>
    /// Optional override for the muzzle flash particle material. If set in the Inspector,
    /// this will be used instead of creating a runtime-only default material.
    /// </summary>
    [Export]
    public ParticleProcessMaterial? MuzzleFlashParticleMaterialOverride { get; set; }

    /// <summary>
    /// Optional override for the muzzle flash sprite frames resource.
    /// </summary>
    [Export]
    public SpriteFrames? MuzzleFlashFramesOverride { get; set; }

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
    /// Current accuracy penalty from accumulated recoil in percentage points.
    /// </summary>
    public float CurrentRecoilPenalty { get; private set; }

    /// <summary>
    /// True when the player is aiming down sights.
    /// </summary>
    public bool IsAimingDownSights { get; private set; }

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
    private PlayerInventory? _inventory;
    private int _lastReticuleWeaponIndex = -1;
    private bool _lastReticuleWasRanged;
    private SpriteFrames? _muzzleFlashFrames;
    private ParticleProcessMaterial? _muzzleFlashParticleMaterial;

    public override void _Ready()
    {
        // Allow overriding the spriteframes and particle material from the Inspector.
        _muzzleFlashFrames = MuzzleFlashFramesOverride ?? GD.Load<SpriteFrames>("res://assets/spriteframes/sprF_muzzleflash.tres");

        if (MuzzleFlashParticleMaterialOverride is not null)
        {
            _muzzleFlashParticleMaterial = MuzzleFlashParticleMaterialOverride;
        }
        else
        {
            _muzzleFlashParticleMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point,
                Direction = new Vector3(1f, 0f, 0f),
                Spread = 25f,
                InitialVelocityMin = 0f,
                InitialVelocityMax = 80f,
                Gravity = Vector3.Zero,
                ScaleMin = 0.6f,
                ScaleMax = 1.1f,
                Color = new Color(1f, 0.8f, 0.4f, 1f),
                HueVariationMin = -0.03f,
                HueVariationMax = 0.03f
            };
            var muzzleScaleCurve = new Curve();
            muzzleScaleCurve.AddPoint(new Vector2(0f, 1f));
            muzzleScaleCurve.AddPoint(new Vector2(1f, 0f));
            _muzzleFlashParticleMaterial.ScaleCurve = new CurveTexture
            {
                Curve = muzzleScaleCurve
            };
        }

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

        _inventory = GetParent().GetNodeOrNull<PlayerInventory>("PlayerInventory");

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
        UpdateReticuleAccuracy();
    }

    private void UpdateReticuleAccuracy()
    {
        var weapon = CurrentWeapon;
        if (weapon is null || weapon.IsMelee)
        {
            CursorManager.Instance?.SetReticuleAccuracy(100f);
            if (_lastReticuleWasRanged)
            {
                _lastReticuleWasRanged = false;
                GD.Print("WeaponManager: Reticule accuracy reset (no ranged weapon equipped).");
            }
            return;
        }

        var totalAccuracy = GetTotalAccuracyPercent();
        CursorManager.Instance?.SetReticuleAccuracy(totalAccuracy);

        if (!_lastReticuleWasRanged || _lastReticuleWeaponIndex != CurrentWeaponIndex)
        {
            _lastReticuleWasRanged = true;
            _lastReticuleWeaponIndex = CurrentWeaponIndex;
            var totalSpreadDeg = GetTotalSpreadDegrees(weapon);
            var maxInaccuracyDeg = GetMaxInaccuracyDegrees(weapon);
            GD.Print($"WeaponManager: Reticule accuracy {totalAccuracy:F1}% for {weapon.DisplayName} (spread {totalSpreadDeg:F1} deg / max {maxInaccuracyDeg:F1} deg)");
        }
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
            CurrentRecoilPenalty += weapon.Recoil * GetRecoilMultiplier();
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

        if (weapon.IsMelee || weapon.AmmoType == AmmoType.None)
        {
            _ammoInMagazine[CurrentWeaponIndex] = weapon.MagazineSize;
            FinishReload();
            return;
        }

        if (weapon.ReloadMode == WeaponReloadMode.ShellByShell)
        {
            var consumed = ConsumeAmmoFromInventory(weapon.AmmoType, 1);
            if (consumed <= 0)
            {
                FinishReload();
                return;
            }

            // Add one shell
            _ammoInMagazine[CurrentWeaponIndex] = Mathf.Min(
                _ammoInMagazine[CurrentWeaponIndex] + consumed,
                weapon.MagazineSize);
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
            // Magazine reload - fill as much as possible
            var needed = weapon.MagazineSize - _ammoInMagazine[CurrentWeaponIndex];
            var consumed = ConsumeAmmoFromInventory(weapon.AmmoType, needed);
            if (consumed <= 0)
            {
                FinishReload();
                return;
            }

            _ammoInMagazine[CurrentWeaponIndex] = Mathf.Min(
                _ammoInMagazine[CurrentWeaponIndex] + consumed,
                weapon.MagazineSize);
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

        if (weapon.IsMelee || weapon.AmmoType == AmmoType.None)
        {
            return;
        }

        if (_inventory is not null && _inventory.CountAmmo(weapon.AmmoType) <= 0)
        {
            GD.Print($"WeaponManager: No ammo available for {weapon.DisplayName}.");
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
            var fireDirection = FireProjectiles(origin, direction, weapon);
            EmitSignal(SignalName.WeaponFired, weapon, fireDirection);
        }

        return true;
    }

    private int ConsumeAmmoFromInventory(AmmoType ammoType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        if (_inventory is null)
        {
            return amount;
        }

        return _inventory.ConsumeAmmo(ammoType, amount);
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

    public void SetAimDownSights(bool isAiming)
    {
        IsAimingDownSights = isAiming;
    }

    private Vector2 FireProjectiles(Vector2 origin, Vector2 direction, WeaponData weapon)
    {
        if (BulletScene is null)
        {
            GD.PrintErr("WeaponManager: BulletScene is not set!");
            return direction;
        }

        // Update recoil state before firing
        UpdateRecoilOnFire(weapon);

        // Calculate spawn position with offset
        // X offset is forward (in direction), Y offset is perpendicular (left of direction)
        var forward = direction.Normalized();
        var perpendicular = new Vector2(-forward.Y, forward.X); // Rotated 90 degrees CCW
        var spawnPos = origin + (forward * weapon.SpawnOffsetX) + (perpendicular * weapon.SpawnOffsetY);

        var pelletCount = weapon.PelletCount;

        // Calculate effective spread from recoil-adjusted accuracy.
        // Stability is the max inaccuracy at 0% accuracy; higher accuracy reduces base spread.
        var baseAccuracyPercent = GetBaseAccuracyPercent(weapon);
        var accuracyPercent = GetRecoilAdjustedAccuracyPercent(weapon);
        var stabilitySpreadDeg = GetStabilitySpreadDegrees(weapon, accuracyPercent);
        var totalSpreadDeg = stabilitySpreadDeg + weapon.SpreadAngle;
        var spreadRad = Mathf.DegToRad(totalSpreadDeg);
        var baseAngle = direction.Angle();

        var visualDirSum = Vector2.Zero;
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
            visualDirSum += pelletDir;

            var bullet = BulletScene.Instantiate<Projectile>();
            GetTree().Root.AddChild(bullet);
            bullet.GlobalPosition = spawnPos;
            bullet.Damage = weapon.Damage;
            bullet.Speed = weapon.ProjectileSpeed;
            bullet.Knockback = weapon.Knockback;
            bullet.Initialize(pelletDir);
        }

        var visualDirection = visualDirSum.LengthSquared() > 0.0001f
            ? visualDirSum.Normalized()
            : direction.Normalized();

        PlayMuzzleFlash(weapon, spawnPos, visualDirection);

        GD.Print($"WeaponManager: Fired {pelletCount} projectile(s) from {weapon.DisplayName} (spread: {totalSpreadDeg:F1} deg = stability {stabilitySpreadDeg:F1} deg @ {accuracyPercent:F0}% (base {baseAccuracyPercent:F0}% - recoil {CurrentRecoilPenalty:F1}%) + weapon spread {weapon.SpreadAngle:F1} deg)");
        return visualDirection;
    }

    private void PlayMuzzleFlash(WeaponData weapon, Vector2 spawnPos, Vector2 direction)
    {
        if (_muzzleFlashFrames is null || string.IsNullOrEmpty(weapon.MuzzleFlash) || weapon.MuzzleFlash.Equals("none", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!_muzzleFlashFrames.HasAnimation(weapon.MuzzleFlash))
        {
            GD.PrintErr($"WeaponManager: Muzzle flash animation '{weapon.MuzzleFlash}' not found in sprF_muzzleflash.tres");
            return;
        }

        if (weapon.UseParticleMuzzleFlash)
        {
            PlayParticleMuzzleFlash(weapon, spawnPos, direction);
            return;
        }

        var flash = new AnimatedSprite2D
        {
            SpriteFrames = _muzzleFlashFrames,
            Animation = weapon.MuzzleFlash
        };

        // Set offset to be left-middle
        flash.Centered = false;
        var frame = _muzzleFlashFrames.GetFrameTexture(weapon.MuzzleFlash, 0);
        if (frame is not null)
        {
            flash.Offset = new Vector2(0, -frame.GetSize().Y / 2f);
        }


        flash.ZAsRelative = true;
        flash.ZIndex = 1;

        // The animation should not loop. The user said it should be removed when finished.
        // I can achieve this by connecting to the "animation_finished" signal.
        flash.AnimationFinished += () => flash.QueueFree();

        var parentForFlash = GetParent()?.GetNodeOrNull<Node2D>("Body")
            ?? GetParent() as Node2D
            ?? GetTree().CurrentScene as Node2D
            ?? this;
        parentForFlash.AddChild(flash);
        flash.GlobalPosition = spawnPos;
        flash.GlobalRotation = direction.Angle();
        flash.Play();
        GD.Print($"WeaponManager: Muzzle flash '{weapon.MuzzleFlash}' spawned at {spawnPos} (parent: {parentForFlash.Name}).");
    }

    private void PlayParticleMuzzleFlash(WeaponData weapon, Vector2 spawnPos, Vector2 direction)
    {
        // Use the canonical muzzle flash scene rather than runtime particle construction
        const string muzzleScenePath = "res://scenes/effects/MuzzleFlash.tscn";
        var muzzleScene = GD.Load<PackedScene>(muzzleScenePath);
        if (muzzleScene is null)
        {
            GD.PrintErr($"WeaponManager: Failed to load muzzle flash scene at {muzzleScenePath}");
            return;
        }

        var instance = muzzleScene.Instantiate();

        var parentForFlash = GetParent()?.GetNodeOrNull<Node2D>("Body")
            ?? GetParent() as Node2D
            ?? GetTree().CurrentScene as Node2D
            ?? this;

        parentForFlash.AddChild(instance);

        // Position/rotate the root node if it's a Node2D
        if (instance is Node2D rootNode2D)
        {
            rootNode2D.GlobalPosition = spawnPos;
            rootNode2D.GlobalRotation = direction.Angle();
        }

        // Collect ALL GPUParticles2D in the instantiated scene (including root)
        var emitters = new List<GpuParticles2D>();
        void CollectEmitters(Node? node)
        {
            if (node is null)
                return;
            if (node is GpuParticles2D gp)
            {
                emitters.Add(gp);
            }
            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                {
                    CollectEmitters(childNode);
                }
            }
        }

        CollectEmitters(instance);

        if (emitters.Count == 0)
        {
            // Nothing to emit in the scene; free the instance.
            instance.QueueFree();
            return;
        }

        // Start all emitters and free the instance only after every emitter has finished
        var remaining = emitters.Count;
        foreach (var gp in emitters)
        {
            gp.Emitting = true;
            gp.Finished += () =>
            {
                remaining--;
                if (remaining <= 0)
                {
                    instance.QueueFree();
                }
            };
        }

        GD.Print($"WeaponManager: Spawned {emitters.Count} particle emitter(s) for muzzle flash at {spawnPos} (parent: {parentForFlash.Name}).");
    }

    /// <summary>
    /// Gets the current total accuracy percentage for the equipped weapon after recoil and spread.
    /// </summary>
    public float GetTotalAccuracyPercent()
    {
        var weapon = CurrentWeapon;
        if (weapon is null || weapon.IsMelee)
        {
            return 100f;
        }

        var totalSpreadDeg = GetTotalSpreadDegrees(weapon);
        var maxInaccuracyDeg = GetMaxInaccuracyDegrees(weapon);
        var accuracy = 100f - ((totalSpreadDeg / maxInaccuracyDeg) * 100f);
        return Mathf.Clamp(accuracy, 0f, 100f);
    }

    private float GetBaseAccuracyPercent(WeaponData weapon)
    {
        var accuracy = weapon.Accuracy;
        if (IsAimingDownSights)
        {
            accuracy *= weapon.AimDownSightsAccuracyMultiplier;
        }

        return Mathf.Clamp(accuracy, 0f, 100f);
    }

    private float GetStabilitySpreadDegrees(WeaponData weapon, float accuracyPercent)
    {
        var accuracyFactor = 1f - (accuracyPercent / 100f);
        return weapon.Stability * Mathf.Clamp(accuracyFactor, 0f, 1f);
    }

    private float GetTotalSpreadDegrees(WeaponData weapon)
    {
        var accuracyPercent = GetRecoilAdjustedAccuracyPercent(weapon);
        var stabilitySpreadDeg = GetStabilitySpreadDegrees(weapon, accuracyPercent);
        return stabilitySpreadDeg + weapon.SpreadAngle;
    }

    private static float GetMaxInaccuracyDegrees(WeaponData weapon)
    {
        var maxInaccuracy = weapon.Stability + weapon.SpreadAngle;
        return maxInaccuracy > 0f ? maxInaccuracy : 10f;
    }

    private float GetRecoilMultiplier()
    {
        var multiplier = RecoilMultiplier;
        if (IsAimingDownSights)
        {
            multiplier *= AimDownSightsRecoilMultiplier;
        }

        return multiplier;
    }

    private float GetRecoilAdjustedAccuracyPercent(WeaponData weapon)
    {
        var accuracy = GetBaseAccuracyPercent(weapon);
        return Mathf.Clamp(accuracy - CurrentRecoilPenalty, 0f, 100f);
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
