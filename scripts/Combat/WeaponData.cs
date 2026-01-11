namespace DAIgame.Combat;

using Godot;

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
    /// Knockback force applied to the player when firing.
    /// </summary>
    [Export]
    public float KnockbackStrength { get; set; } = 50f;

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
            KnockbackStrength = KnockbackStrength,
            AnimationSuffix = AnimationSuffix,
            MaxAmmo = MaxAmmo,
            MagazineSize = MagazineSize,
            ReloadTime = ReloadTime,
            ProjectileSpeed = ProjectileSpeed,
            SpawnOffsetX = SpawnOffsetX,
            SpawnOffsetY = SpawnOffsetY,
            ReloadMode = ReloadMode
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
