namespace DAIgame.Core.Items;

using DAIgame.Combat;
using Godot;

/// <summary>
/// An item that represents ammunition for ranged weapons.
/// </summary>
[GlobalClass]
public partial class AmmoItem : Item
{
    private const int DefaultMaxStack = 300;

    /// <summary>
    /// The type of ammo (Small, Rifle, Shotgun).
    /// </summary>
    [Export]
    public AmmoType AmmoType { get; set; } = AmmoType.None;

    public AmmoItem()
    {
        ItemType = ItemType.Ammo;
        MaxStack = DefaultMaxStack;
    }

    /// <inheritdoc/>
    public override bool CanStackWith(Item other)
    {
        if (!IsStackable || !other.IsStackable)
        {
            return false;
        }

        // Ammo stacks if same ammo type
        if (other is AmmoItem otherAmmo)
        {
            return AmmoType == otherAmmo.AmmoType && AmmoType != AmmoType.None;
        }

        return false;
    }

    /// <inheritdoc/>
    public override Item Clone()
    {
        return new AmmoItem
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
            AmmoType = AmmoType
        };
    }

    /// <summary>
    /// Creates an AmmoItem for the given ammo type and count.
    /// </summary>
    public static AmmoItem Create(AmmoType ammoType, int count, Texture2D? icon = null)
    {
        var (displayName, description) = GetAmmoInfo(ammoType);

        return new AmmoItem
        {
            ItemId = $"ammo_{ammoType.ToString().ToLowerInvariant()}",
            DisplayName = displayName,
            Description = description,
            Icon = icon,
            AmmoType = ammoType,
            StackCount = count,
            MaxStack = DefaultMaxStack
        };
    }

    private static (string displayName, string description) GetAmmoInfo(AmmoType type)
    {
        return type switch
        {
            AmmoType.Small => ("Ammo (Small)", "Small caliber ammunition for pistols and SMGs."),
            AmmoType.Rifle => ("Ammo (Rifle)", "Rifle ammunition for long-range weapons."),
            AmmoType.Shotgun => ("Ammo (Shotgun)", "Shotgun shells for close-range devastation."),
            AmmoType.None => throw new System.NotImplementedException(),
            _ => ("Unknown Ammo", "Unknown ammunition type.")
        };
    }
}
