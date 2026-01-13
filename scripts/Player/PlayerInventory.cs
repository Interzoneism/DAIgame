namespace DAIgame.Player;

using System.Collections.Generic;
using DAIgame.Combat;
using Godot;

public partial class PlayerInventory : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler();

    private const int AmmoStackMax = 300;

    [Export]
    public int BackpackColumns { get; set; } = 6;

    [Export]
    public int BackpackRows { get; set; } = 5;

    private InventoryItem?[] _backpack = [];
    private readonly Dictionary<InventorySlotType, InventoryItem?> _equipment = new();
    private WeaponManager? _weaponManager;

    public override void _Ready()
    {
        InitializeSlots();
        _weaponManager = GetParent().GetNodeOrNull<WeaponManager>("WeaponManager");
        AddStarterItems();
        SyncWeapons();
    }

    public int BackpackSlotCount => _backpack.Length;

    public InventoryItem? GetItem(InventorySlotType slotType, int slotIndex = -1)
    {
        if (slotType == InventorySlotType.Backpack)
        {
            return IsBackpackIndexValid(slotIndex) ? _backpack[slotIndex] : null;
        }

        return _equipment.TryGetValue(slotType, out var item) ? item : null;
    }

    public bool SetItem(InventorySlotType slotType, int slotIndex, InventoryItem? item)
    {
        if (!CanPlaceItem(item, slotType))
        {
            return false;
        }

        if (!SetItemInternal(slotType, slotIndex, item))
        {
            return false;
        }

        EmitSignal(SignalName.InventoryChanged);
        if (IsWeaponSlot(slotType))
        {
            SyncWeapons();
        }
        return true;
    }

    public bool TryMoveItem(InventorySlotType fromType, int fromIndex, InventorySlotType toType, int toIndex)
    {
        var fromItem = GetItem(fromType, fromIndex);
        if (fromItem is null)
        {
            return false;
        }

        if (!CanPlaceItem(fromItem, toType))
        {
            return false;
        }

        var toItem = GetItem(toType, toIndex);
        if (toItem is not null && !CanPlaceItem(toItem, fromType))
        {
            return false;
        }

        if (!SetItemInternal(fromType, fromIndex, toItem))
        {
            return false;
        }

        if (!SetItemInternal(toType, toIndex, fromItem))
        {
            return false;
        }

        EmitSignal(SignalName.InventoryChanged);
        if (IsWeaponSlot(fromType) || IsWeaponSlot(toType))
        {
            SyncWeapons();
        }
        return true;
    }

    public bool AddItemToBackpack(InventoryItem item)
    {
        if (item.IsStackable && !CanFullyFitStack(item))
        {
            return false;
        }

        if (item.IsStackable)
        {
            var remaining = item.StackCount;
            for (var i = 0; i < _backpack.Length; i++)
            {
                var existing = _backpack[i];
                if (existing is null || !existing.CanStackWith(item))
                {
                    continue;
                }

                if (existing.StackCount >= existing.MaxStack)
                {
                    continue;
                }

                var space = existing.MaxStack - existing.StackCount;
                var toAdd = Mathf.Min(space, remaining);
                existing.StackCount += toAdd;
                remaining -= toAdd;
                if (remaining <= 0)
                {
                    EmitSignal(SignalName.InventoryChanged);
                    return true;
                }
            }

            if (remaining > 0)
            {
                for (var i = 0; i < _backpack.Length; i++)
                {
                    if (_backpack[i] is not null)
                    {
                        continue;
                    }

                    var stackItem = remaining == item.StackCount ? item : item.CreateStackCopy(remaining);
                    stackItem.StackCount = remaining;
                    _backpack[i] = stackItem;
                    EmitSignal(SignalName.InventoryChanged);
                    return true;
                }
            }

            return false;
        }

        for (var i = 0; i < _backpack.Length; i++)
        {
            if (_backpack[i] is null)
            {
                _backpack[i] = item;
                EmitSignal(SignalName.InventoryChanged);
                return true;
            }
        }

        return false;
    }

    public bool CanPlaceItem(InventoryItem? item, InventorySlotType slotType)
    {
        if (item is null)
        {
            return true;
        }

        if (slotType == InventorySlotType.Backpack)
        {
            return true;
        }

        return slotType switch
        {
            InventorySlotType.LeftHand or InventorySlotType.RightHand => item.ItemType == InventoryItemType.Weapon,
            InventorySlotType.Usable1 or InventorySlotType.Usable2 => item.ItemType == InventoryItemType.Usable,
            InventorySlotType.Outfit => item.ItemType == InventoryItemType.Outfit,
            InventorySlotType.Headwear => item.ItemType == InventoryItemType.Headwear,
            InventorySlotType.Shoes => item.ItemType == InventoryItemType.Shoes,
            _ => false
        };
    }

    private void InitializeSlots()
    {
        var columns = Mathf.Max(1, BackpackColumns);
        var rows = Mathf.Max(1, BackpackRows);
        _backpack = new InventoryItem?[columns * rows];

        _equipment.Clear();
        _equipment[InventorySlotType.Outfit] = null;
        _equipment[InventorySlotType.Headwear] = null;
        _equipment[InventorySlotType.Shoes] = null;
        _equipment[InventorySlotType.Usable1] = null;
        _equipment[InventorySlotType.Usable2] = null;
        _equipment[InventorySlotType.LeftHand] = null;
        _equipment[InventorySlotType.RightHand] = null;
    }

    private void AddStarterItems()
    {
        var weapon = ResourceLoader.Load<WeaponData>("res://data/weapons/uzi.tres");
        var icon = ResourceLoader.Load<Texture2D>("res://assets/sprites/icon_uzi.png");

        if (weapon is null)
        {
            GD.PrintErr("PlayerInventory: Failed to load uzi weapon data.");
            return;
        }

        if (icon is null)
        {
            GD.PrintErr("PlayerInventory: Failed to load uzi icon.");
            return;
        }

        var uziItem = new InventoryItem
        {
            ItemId = "uzi",
            DisplayName = "Uzi",
            ItemType = InventoryItemType.Weapon,
            Icon = icon,
            StackCount = 1,
            MaxStack = 1,
            WeaponData = weapon
        };

        if (!AddItemToBackpack(uziItem))
        {
            GD.PrintErr("PlayerInventory: Backpack full, could not add starter uzi.");
        }

        AddStarterAmmo(Combat.AmmoType.Small, "Ammo (Small)", "res://assets/sprites/ammo/ammo_small.png", 120);
        AddStarterAmmo(Combat.AmmoType.Rifle, "Ammo (Rifle)", "res://assets/sprites/ammo/ammo_rifle.png", 60);
        AddStarterAmmo(Combat.AmmoType.Shotgun, "Ammo (Shotgun)", "res://assets/sprites/ammo/ammo_shotgun.png", 30);
    }

    private void AddStarterAmmo(Combat.AmmoType ammoType, string displayName, string iconPath, int amount)
    {
        var icon = ResourceLoader.Load<Texture2D>(iconPath);
        if (icon is null)
        {
            GD.PrintErr($"PlayerInventory: Failed to load ammo icon {iconPath}.");
            return;
        }

        var ammoItem = new InventoryItem
        {
            ItemId = $"ammo_{ammoType.ToString().ToLowerInvariant()}",
            DisplayName = displayName,
            ItemType = InventoryItemType.Ammo,
            AmmoType = ammoType,
            Icon = icon,
            MaxStack = AmmoStackMax,
            StackCount = Mathf.Clamp(amount, 1, AmmoStackMax)
        };

        if (!AddItemToBackpack(ammoItem))
        {
            GD.PrintErr($"PlayerInventory: Backpack full, could not add starter ammo {displayName}.");
        }
    }

    public int CountAmmo(Combat.AmmoType ammoType)
    {
        var total = 0;
        foreach (var item in _backpack)
        {
            if (item is null || item.ItemType != InventoryItemType.Ammo || item.AmmoType != ammoType)
            {
                continue;
            }

            total += item.StackCount;
        }

        return total;
    }

    public int ConsumeAmmo(Combat.AmmoType ammoType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var remaining = amount;
        for (var i = 0; i < _backpack.Length; i++)
        {
            var item = _backpack[i];
            if (item is null || item.ItemType != InventoryItemType.Ammo || item.AmmoType != ammoType)
            {
                continue;
            }

            var take = Mathf.Min(item.StackCount, remaining);
            item.StackCount -= take;
            remaining -= take;

            if (item.StackCount <= 0)
            {
                _backpack[i] = null;
            }

            if (remaining <= 0)
            {
                break;
            }
        }

        if (remaining != amount)
        {
            EmitSignal(SignalName.InventoryChanged);
        }

        return amount - remaining;
    }

    public void NotifyInventoryChanged()
    {
        EmitSignal(SignalName.InventoryChanged);
    }

    private bool CanFullyFitStack(InventoryItem item)
    {
        var needed = item.StackCount;
        var capacity = 0;

        foreach (var slotItem in _backpack)
        {
            if (slotItem is null)
            {
                capacity += item.MaxStack;
                continue;
            }

            if (!slotItem.CanStackWith(item))
            {
                continue;
            }

            capacity += Mathf.Max(0, slotItem.MaxStack - slotItem.StackCount);
        }

        return capacity >= needed;
    }

    private bool SetItemInternal(InventorySlotType slotType, int slotIndex, InventoryItem? item)
    {
        if (slotType == InventorySlotType.Backpack)
        {
            if (!IsBackpackIndexValid(slotIndex))
            {
                return false;
            }

            _backpack[slotIndex] = item;
            return true;
        }

        if (!_equipment.ContainsKey(slotType))
        {
            return false;
        }

        _equipment[slotType] = item;
        return true;
    }

    private bool IsBackpackIndexValid(int index) => index >= 0 && index < _backpack.Length;

    private static bool IsWeaponSlot(InventorySlotType slotType)
        => slotType == InventorySlotType.LeftHand || slotType == InventorySlotType.RightHand;

    private void SyncWeapons()
    {
        if (_weaponManager is null)
        {
            return;
        }

        var weapons = new List<WeaponData>();
        var rightHand = GetItem(InventorySlotType.RightHand);
        if (rightHand?.WeaponData is not null)
        {
            weapons.Add(rightHand.WeaponData);
        }

        var leftHand = GetItem(InventorySlotType.LeftHand);
        if (leftHand?.WeaponData is not null)
        {
            weapons.Add(leftHand.WeaponData);
        }

        _weaponManager.SetWeapons(weapons, weapons.Count > 0 ? 0 : -1);
    }
}
