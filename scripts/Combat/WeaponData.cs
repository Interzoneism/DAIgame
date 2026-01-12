namespace DAIgame.Combat;

using Godot;

/// <summary>
/// Defines how a melee weapon registers hits.
/// </summary>
public enum MeleeHitType
{
    /// <summary>
    /// Hits register instantly in a cone in front of the player.
    /// </summary>
    Instant,

    /// <summary>
    /// Hits register over time during a swing arc (e.g., bat swings from +45° to -45°).
    /// </summary>
    Swing
}

/// <summary>
/// Defines the firing behavior of a weapon.
/// </summary>
public enum WeaponFireMode
{
    /// <summary>
    /// Semi-automatic: fires once per trigger press.
    /// </summary>
    SemiAuto,

    /// <summary>
    /// Automatic: fires continuously while trigger is held.
    /// </summary>
    Automatic
}

/// <summary>
/// Defines how a weapon reloads.
/// </summary>
public enum WeaponReloadMode
{
    /// <summary>
    /// Full magazine reload (pistol, uzi style).
    /// </summary>
    Magazine,

    /// <summary>
    /// Shell-by-shell reload, can be interrupted by firing (shotgun style).
    /// </summary>
    ShellByShell
}

/// <summary>
/// Resource that defines weapon properties. Designed to be inventory-compatible later.
/// Each weapon type (Pistol, Shotgun, Uzi) is an instance of this resource.
/// </summary>
[GlobalClass]
public partial class WeaponData : Resource
{
    /// <summary>
    /// Display name of the weapon.
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = "Weapon";

    /// <summary>
    /// Unique identifier for the weapon type.
    /// </summary>
    [Export]
    public string WeaponId { get; set; } = "unknown";

    /// <summary>
    /// Damage per projectile hit.
    /// </summary>
    [Export]
    public float Damage { get; set; } = 25f;

    /// <summary>
    /// Fire rate in shots per second.
    /// </summary>
    [Export]
    public float FireRate { get; set; } = 2f;

    /// <summary>
    /// Time between shots in seconds (derived from FireRate).
    /// </summary>
    public float TimeBetweenShots => FireRate > 0f ? 1f / FireRate : 1f;

    /// <summary>
    /// Number of projectiles fired per shot (1 for pistol/uzi, multiple for shotgun).
    /// </summary>
    [Export]
    public int PelletCount { get; set; } = 1;

    /// <summary>
    /// Spread angle in degrees (0 for perfect accuracy, 45 for shotgun-style spread).
    /// </summary>
    [Export]
    public float SpreadAngle { get; set; } = 0f;

    /// <summary>
    /// Firing behavior (semi-auto or automatic).
    /// </summary>
    [Export]
    public WeaponFireMode FireMode { get; set; } = WeaponFireMode.SemiAuto;

    /// <summary>
    /// Knockback force applied to the player when firing (recoil movement).
    /// </summary>
    [Export]
    public float KnockbackPlayer { get; set; } = 50f;

    /// <summary>
    /// Knockback force applied to enemies when hit.
    /// </summary>
    [Export]
    public float Knockback { get; set; } = 150f;

    /// <summary>
    /// Base stability in degrees - maximum spread when accuracy is at 0%.
    /// At 100% accuracy, bullets go straight. At 0% accuracy, bullets spread up to Stability degrees.
    /// </summary>
    [Export]
    public float Stability { get; set; } = 10f;

    /// <summary>
    /// Amount accuracy decreases per shot after warmup (percentage points).
    /// </summary>
    [Export]
    public float Recoil { get; set; } = 5f;

    /// <summary>
    /// Time in seconds after last shot before accuracy starts recovering.
    /// </summary>
    [Export]
    public float RecoilRecovery { get; set; } = 0.3f;

    /// <summary>
    /// Number of shots within RecoilRecovery time before recoil starts applying.
    /// First N shots have no accuracy penalty.
    /// </summary>
    [Export]
    public int RecoilWarmup { get; set; } = 1;

    /// <summary>
    /// Animation name suffix for this weapon (e.g., "pistol" for walk_pistol, attack_pistol).
    /// </summary>
    [Export]
    public string AnimationSuffix { get; set; } = "pistol";

    /// <summary>
    /// Maximum ammo capacity. Set to -1 for unlimited.
    /// </summary>
    [Export]
    public int MaxAmmo { get; set; } = -1;

    /// <summary>
    /// Magazine size. Set to -1 for no magazine (reloads instantly).
    /// </summary>
    [Export]
    public int MagazineSize { get; set; } = -1;

    /// <summary>
    /// Reload time in seconds.
    /// </summary>
    [Export]
    public float ReloadTime { get; set; } = 1f;

    /// <summary>
    /// Speed of projectiles fired by this weapon.
    /// </summary>
    [Export]
    public float ProjectileSpeed { get; set; } = 800f;

    /// <summary>
    /// Horizontal offset for projectile spawn (positive = forward from player center).
    /// </summary>
    [Export]
    public float SpawnOffsetX { get; set; } = 10f;

    /// <summary>
    /// Vertical offset for projectile spawn (positive = up/left of aim direction).
    /// </summary>
    [Export]
    public float SpawnOffsetY { get; set; } = 0f;

    /// <summary>
    /// How the weapon reloads (magazine or shell-by-shell).
    /// </summary>
    [Export]
    public WeaponReloadMode ReloadMode { get; set; } = WeaponReloadMode.Magazine;

    // ========== MELEE WEAPON PROPERTIES ==========

    /// <summary>
    /// If true, this weapon is a melee weapon and uses melee attack logic.
    /// </summary>
    [Export]
    public bool IsMelee { get; set; } = false;

    /// <summary>
    /// How melee hits are registered (instant cone or swing arc over time).
    /// </summary>
    [Export]
    public MeleeHitType MeleeHitType { get; set; } = MeleeHitType.Instant;

    /// <summary>
    /// Range of the melee attack in pixels.
    /// </summary>
    [Export]
    public float MeleeRange { get; set; } = 40f;

    /// <summary>
    /// For Instant type: cone angle in degrees (total spread, centered on aim direction).
    /// For Swing type: not used directly (use SwingStartAngle/SwingEndAngle instead).
    /// </summary>
    [Export]
    public float MeleeSpreadAngle { get; set; } = 90f;

    /// <summary>
    /// For Swing type: angle offset (in degrees) from aim direction where swing starts.
    /// Positive = clockwise from aim direction.
    /// </summary>
    [Export]
    public float SwingStartAngle { get; set; } = 45f;

    /// <summary>
    /// For Swing type: angle offset (in degrees) from aim direction where swing ends.
    /// Negative = counter-clockwise from aim direction.
    /// </summary>
    [Export]
    public float SwingEndAngle { get; set; } = -45f;

    /// <summary>
    /// Delay in seconds before damage is applied after attack starts (for melee weapons).
    /// Similar to zombie attack windup. 0 = instant damage at attack start.
    /// </summary>
    [Export]
    public float DamageDelay { get; set; } = 0f;

    /// <summary>
    /// Stamina cost per attack (for melee weapons). 0 = no stamina cost.
    /// </summary>
    [Export]
    public float StaminaCost { get; set; } = 0f;

    /// <summary>
    /// Creates a deep copy of this weapon data for inventory use.
    /// </summary>
    public WeaponData Clone()
    {
        return new WeaponData
        {
            DisplayName = DisplayName,
            WeaponId = WeaponId,
            Damage = Damage,
            FireRate = FireRate,
            PelletCount = PelletCount,
            SpreadAngle = SpreadAngle,
            FireMode = FireMode,
            KnockbackPlayer = KnockbackPlayer,
            Knockback = Knockback,
            Stability = Stability,
            Recoil = Recoil,
            RecoilRecovery = RecoilRecovery,
            RecoilWarmup = RecoilWarmup,
            AnimationSuffix = AnimationSuffix,
            MaxAmmo = MaxAmmo,
            MagazineSize = MagazineSize,
            ReloadTime = ReloadTime,
            ProjectileSpeed = ProjectileSpeed,
            SpawnOffsetX = SpawnOffsetX,
            SpawnOffsetY = SpawnOffsetY,
            ReloadMode = ReloadMode,
            IsMelee = IsMelee,
            MeleeHitType = MeleeHitType,
            MeleeRange = MeleeRange,
            MeleeSpreadAngle = MeleeSpreadAngle,
            SwingStartAngle = SwingStartAngle,
            SwingEndAngle = SwingEndAngle,
            DamageDelay = DamageDelay,
            StaminaCost = StaminaCost
        };
    }

    /// <summary>
    /// Gets the walk animation name for this weapon.
    /// </summary>
    public string GetWalkAnimation() => $"walk_{AnimationSuffix}";

    /// <summary>
    /// Gets the attack animation name for this weapon.
    /// </summary>
    public string GetAttackAnimation() => $"attack_{AnimationSuffix}";
}
