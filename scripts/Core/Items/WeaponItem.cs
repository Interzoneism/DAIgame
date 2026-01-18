namespace DAIgame.Core.Items;

using DAIgame.Combat;
using Godot;

/// <summary>
/// An item that represents a weapon (melee or ranged).
/// Contains a reference to WeaponData for combat stats.
/// </summary>
[GlobalClass]
public partial class WeaponItem : Item
{
    /// <summary>
    /// The weapon data resource containing combat stats.
    /// </summary>
    [Export]
    public WeaponData? WeaponData { get; set; }

    /// <summary>
    /// Current ammo in magazine (for ranged weapons with magazines).
    /// -1 means not applicable (melee or unlimited).
    /// </summary>
    [Export]
    public int CurrentMagazineAmmo { get; set; } = -1;

    /// <summary>
    /// Current durability (for future degradation system).
    /// 100 = full condition, 0 = broken.
    /// </summary>
    [Export]
    public float Durability { get; set; } = 100f;

    /// <summary>
    /// Maximum durability for this weapon.
    /// </summary>
    [Export]
    public float MaxDurability { get; set; } = 100f;

    public WeaponItem()
    {
        ItemType = ItemType.Weapon;
        MaxStack = 1;
    }

    /// <summary>
    /// Whether this weapon is melee.
    /// </summary>
    public bool IsMelee => WeaponData?.IsMelee ?? false;

    /// <summary>
    /// Whether this weapon uses ammo.
    /// </summary>
    public bool UsesAmmo => WeaponData?.AmmoType != AmmoType.None;

    /// <summary>
    /// Gets the ammo type this weapon uses.
    /// </summary>
    public AmmoType AmmoType => WeaponData?.AmmoType ?? AmmoType.None;

    /// <inheritdoc/>
    public override bool CanStackWith(Item other) =>
        // Weapons never stack
        false;

    /// <inheritdoc/>
    public override Item Clone()
    {
        return new WeaponItem
        {
            ItemId = ItemId,
            DisplayName = DisplayName,
            Description = Description,
            Icon = Icon,
            ItemType = ItemType,
            Rarity = Rarity,
            StackCount = StackCount,
            MaxStack = MaxStack,
            BaseValue = BaseValue,
            Weight = Weight,
            WeaponData = WeaponData?.Clone(),
            CurrentMagazineAmmo = CurrentMagazineAmmo,
            Durability = Durability,
            MaxDurability = MaxDurability
        };
    }

    /// <summary>
    /// Creates a WeaponItem from a WeaponData resource.
    /// </summary>
    public static WeaponItem FromWeaponData(WeaponData weaponData, Texture2D? icon = null)
    {
        var item = new WeaponItem
        {
            ItemId = weaponData.WeaponId,
            DisplayName = weaponData.DisplayName,
            Description = GetWeaponDescription(weaponData),
            Icon = icon,
            WeaponData = weaponData,
            CurrentMagazineAmmo = weaponData.MagazineSize > 0 ? weaponData.MagazineSize : -1
        };

        return item;
    }

    private static string GetWeaponDescription(WeaponData data)
    {
        if (data.IsMelee)
        {
            return $"Melee weapon. Damage: {data.Damage}";
        }

        return $"Damage: {data.Damage}\nFire Rate: {data.FireRate}/s\nMagazine: {data.MagazineSize}";
    }
}
