namespace DAIgame.Core.Items;

using System.Collections.Generic;
using DAIgame.Combat;
using Godot;

/// <summary>
/// Static database for item definitions and factory methods.
/// Provides a centralized place to create and look up items.
/// </summary>
public static class ItemDatabase
{
    // Weapon resource paths
    private const string WeaponPathPistol = "res://data/weapons/pistol.tres";
    private const string WeaponPathShotgun = "res://data/weapons/shotgun.tres";
    private const string WeaponPathUzi = "res://data/weapons/uzi.tres";
    private const string WeaponPathBat = "res://data/weapons/bat.tres";

    // Icon paths - weapons
    private const string IconPathPistol = "res://assets/sprites/icon_pistol.png";
    private const string IconPathShotgun = "res://assets/sprites/icon_shotgun.png";
    private const string IconPathUzi = "res://assets/sprites/icon_uzi.png";
    private const string IconPathBat = "res://assets/sprites/icon_uzi.png"; // TODO: Add bat icon

    // Icon paths - ammo
    private const string IconPathAmmoSmall = "res://assets/sprites/ammo/ammo_small.png";
    private const string IconPathAmmoRifle = "res://assets/sprites/ammo/ammo_rifle.png";
    private const string IconPathAmmoShotgun = "res://assets/sprites/ammo/ammo_shotgun.png";

    // Icon paths - wearables
    private const string IconPathClothes = "res://assets/sprites/items/clothes.png";
    private const string IconPathPants = "res://assets/sprites/items/pants.png";

    // Weapon definitions
    private static readonly Dictionary<string, (string resourcePath, string iconPath)> WeaponDefinitions = new()
    {
        { "pistol", (WeaponPathPistol, IconPathPistol) },
        { "shotgun", (WeaponPathShotgun, IconPathShotgun) },
        { "uzi", (WeaponPathUzi, IconPathUzi) },
        { "bat", (WeaponPathBat, IconPathBat) }
    };

    // Ammo definitions
    private static readonly Dictionary<AmmoType, string> AmmoIconPaths = new()
    {
        { AmmoType.Small, IconPathAmmoSmall },
        { AmmoType.Rifle, IconPathAmmoRifle },
        { AmmoType.Shotgun, IconPathAmmoShotgun }
    };

    // Cache for loaded resources
    private static readonly Dictionary<string, WeaponData> LoadedWeapons = [];
    private static readonly Dictionary<string, Texture2D> LoadedIcons = [];

    #region Weapon Creation

    /// <summary>
    /// Creates a weapon item by ID.
    /// </summary>
    public static WeaponItem? CreateWeapon(string weaponId)
    {
        if (!WeaponDefinitions.TryGetValue(weaponId, out var def))
        {
            GD.PrintErr($"ItemDatabase: Unknown weapon ID '{weaponId}'");
            return null;
        }

        var weaponData = LoadWeaponData(def.resourcePath);
        if (weaponData is null)
        {
            return null;
        }

        var icon = LoadIcon(def.iconPath);
        return WeaponItem.FromWeaponData(weaponData, icon);
    }

    /// <summary>
    /// Creates a random weapon item.
    /// </summary>
    public static WeaponItem? CreateRandomWeapon()
    {
        var weaponIds = new List<string>(WeaponDefinitions.Keys);
        var index = (int)(GD.Randi() % weaponIds.Count);
        return CreateWeapon(weaponIds[index]);
    }

    /// <summary>
    /// Creates a weapon item from a WeaponData resource.
    /// </summary>
    public static WeaponItem CreateWeaponFromData(WeaponData weaponData)
    {
        // Try to find icon for this weapon
        string? iconPath = null;
        if (WeaponDefinitions.TryGetValue(weaponData.WeaponId, out var def))
        {
            iconPath = def.iconPath;
        }

        var icon = iconPath is not null ? LoadIcon(iconPath) : null;
        return WeaponItem.FromWeaponData(weaponData, icon);
    }

    /// <summary>
    /// Gets all available weapon IDs.
    /// </summary>
    public static IEnumerable<string> GetAllWeaponIds() => WeaponDefinitions.Keys;

    #endregion

    #region Ammo Creation

    /// <summary>
    /// Creates an ammo item with the specified type and count.
    /// </summary>
    public static AmmoItem? CreateAmmo(AmmoType ammoType, int count)
    {
        if (ammoType == AmmoType.None)
        {
            GD.PrintErr("ItemDatabase: Cannot create ammo with type None");
            return null;
        }

        var icon = AmmoIconPaths.TryGetValue(ammoType, out var iconPath)
            ? LoadIcon(iconPath)
            : null;

        return AmmoItem.Create(ammoType, count, icon);
    }

    /// <summary>
    /// Creates a random ammo item with random count.
    /// </summary>
    public static AmmoItem? CreateRandomAmmo()
    {
        var ammoTypes = new[] { AmmoType.Small, AmmoType.Rifle, AmmoType.Shotgun };
        var index = (int)(GD.Randi() % ammoTypes.Length);
        var ammoType = ammoTypes[index];

        // Random count based on ammo type
        var (minAmount, maxAmount) = ammoType switch
        {
            AmmoType.Small => (10, 30),
            AmmoType.Rifle => (5, 20),
            AmmoType.Shotgun => (4, 12),
            AmmoType.None => throw new System.NotImplementedException(),
            _ => (5, 15)
        };

        var amount = (int)(GD.Randi() % (maxAmount - minAmount + 1)) + minAmount;
        return CreateAmmo(ammoType, amount);
    }

    #endregion

    #region Wearable Creation

    /// <summary>
    /// Creates a basic outfit item.
    /// </summary>
    public static WearableItem CreateOutfit(
        string itemId,
        string displayName,
        float armor = 0f,
        float coldResistance = 0f)
    {
        var icon = LoadIcon(IconPathClothes);
        return WearableItem.Create(itemId, displayName, WearableSlot.Outfit, icon, armor, coldResistance);
    }

    /// <summary>
    /// Creates a basic pants/shoes item.
    /// </summary>
    public static WearableItem CreateShoes(
        string itemId,
        string displayName,
        float speedModifier = 1f)
    {
        var icon = LoadIcon(IconPathPants);
        var item = WearableItem.Create(itemId, displayName, WearableSlot.Shoes, icon);
        item.SpeedModifier = speedModifier;
        return item;
    }

    /// <summary>
    /// Creates a random wearable item.
    /// </summary>
    public static WearableItem CreateRandomWearable()
    {
        var rand = GD.Randi() % 3;
        return rand switch
        {
            0 => CreateOutfit("outfit_basic", "Basic Outfit", armor: 5f, coldResistance: 10f),
            1 => CreateOutfit("outfit_warm", "Warm Jacket", armor: 2f, coldResistance: 25f),
            _ => CreateShoes("shoes_running", "Running Shoes", speedModifier: 1.1f)
        };
    }

    #endregion

    #region Usable Creation

    /// <summary>
    /// Creates a medkit healing item.
    /// </summary>
    public static UsableItem CreateMedkit(int healAmount = 50)
    {
        return UsableItem.CreateHealingItem(
            "medkit",
            "Medkit",
            healAmount,
            useTime: 2f);
    }

    /// <summary>
    /// Creates a bandage healing item.
    /// </summary>
    public static UsableItem CreateBandage(int healAmount = 20)
    {
        return UsableItem.CreateHealingItem(
            "bandage",
            "Bandage",
            healAmount,
            useTime: 1f);
    }

    /// <summary>
    /// Creates a heat pack warmth item.
    /// </summary>
    public static UsableItem CreateHeatPack(float duration = 60f)
    {
        return UsableItem.CreateWarmthItem(
            "heat_pack",
            "Heat Pack",
            warmthValue: 50f,
            duration: duration);
    }

    #endregion

    #region Misc Creation

    /// <summary>
    /// Creates a generic misc item.
    /// </summary>
    public static MiscItem CreateMisc(string itemId, string displayName, string description = "") => MiscItem.Create(itemId, displayName, description);

    #endregion

    #region Generic Item Creation

    /// <summary>
    /// Creates an item by its ID. Tries to determine type from ID prefix.
    /// </summary>
    public static Item? CreateItem(string itemId, int count = 1)
    {
        // Check if it's a weapon
        if (WeaponDefinitions.ContainsKey(itemId))
        {
            return CreateWeapon(itemId);
        }

        // Check if it's ammo
        if (itemId.StartsWith("ammo_", System.StringComparison.Ordinal))
        {
            var ammoTypeName = itemId.Replace("ammo_", "", System.StringComparison.Ordinal);
            var ammoType = ammoTypeName.ToLowerInvariant() switch
            {
                "small" => AmmoType.Small,
                "rifle" => AmmoType.Rifle,
                "shotgun" => AmmoType.Shotgun,
                _ => AmmoType.None
            };

            if (ammoType != AmmoType.None)
            {
                return CreateAmmo(ammoType, count);
            }
        }

        // Check common item IDs
        return itemId switch
        {
            "medkit" => CreateMedkit(),
            "bandage" => CreateBandage(),
            "heat_pack" => CreateHeatPack(),
            _ => null
        };
    }

    /// <summary>
    /// Creates a random item of any type for loot generation.
    /// </summary>
    public static Item? CreateRandomLootItem()
    {
        var rand = GD.Randi() % 100;

        // 30% chance weapon, 40% chance ammo, 20% chance usable, 10% chance wearable
        if (rand < 30)
        {
            return CreateRandomWeapon();
        }
        else if (rand < 70)
        {
            return CreateRandomAmmo();
        }
        else if (rand < 90)
        {
            return GD.Randi() % 2 == 0 ? CreateMedkit() : CreateBandage();
        }
        else
        {
            return CreateRandomWearable();
        }
    }

    #endregion

    #region Resource Loading

    private static WeaponData? LoadWeaponData(string path)
    {
        if (LoadedWeapons.TryGetValue(path, out var cached))
        {
            // Clone to avoid shared state
            return cached.Clone();
        }

        var weapon = ResourceLoader.Load<WeaponData>(path);
        if (weapon is null)
        {
            GD.PrintErr($"ItemDatabase: Failed to load weapon at '{path}'");
            return null;
        }

        LoadedWeapons[path] = weapon;
        return weapon.Clone();
    }

    private static Texture2D? LoadIcon(string path)
    {
        if (LoadedIcons.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var icon = ResourceLoader.Load<Texture2D>(path);
        if (icon is null)
        {
            GD.PrintErr($"ItemDatabase: Failed to load icon at '{path}'");
            return null;
        }

        LoadedIcons[path] = icon;
        return icon;
    }

    /// <summary>
    /// Clears the resource cache. Call when unloading/reloading resources.
    /// </summary>
    public static void ClearCache()
    {
        LoadedWeapons.Clear();
        LoadedIcons.Clear();
    }

    #endregion
}
