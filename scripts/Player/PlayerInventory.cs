namespace DAIgame.Player;

using System.Collections.Generic;
using DAIgame.Combat;
using DAIgame.Core.Items;
using Godot;

public partial class PlayerInventory : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Export]
    public int BackpackColumns { get; set; } = 6;

    [Export]
    public int BackpackRows { get; set; } = 5;

    private Item?[] _backpack = [];
    private readonly Dictionary<InventorySlotType, Item?> _equipment = [];
    private WeaponManager? _weaponManager;

    public override void _Ready()
    {
        InitializeSlots();
        _weaponManager = GetParent().GetNodeOrNull<WeaponManager>("WeaponManager");
        AddStarterItems();
        SyncWeapons();
    }

    public int BackpackSlotCount => _backpack.Length;

    public Item? GetItem(InventorySlotType slotType, int slotIndex = -1)
    {
        if (slotType == InventorySlotType.Backpack)
        {
            return IsBackpackIndexValid(slotIndex) ? _backpack[slotIndex] : null;
        }

        return _equipment.TryGetValue(slotType, out var item) ? item : null;
    }

    public bool SetItem(InventorySlotType slotType, int slotIndex, Item? item)
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
        if (slotType.IsWeaponSlot())
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
        if (fromType.IsWeaponSlot() || toType.IsWeaponSlot())
        {
            SyncWeapons();
        }
        return true;
    }

    public bool AddItemToBackpack(Item item)
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

    public bool CanPlaceItem(Item? item, InventorySlotType slotType) => slotType.CanAcceptItem(item);

    private void InitializeSlots()
    {
        var columns = Mathf.Max(1, BackpackColumns);
        var rows = Mathf.Max(1, BackpackRows);
        _backpack = new Item?[columns * rows];

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
        // Use ItemDatabase to create starter weapon
        var uziItem = ItemDatabase.CreateWeapon("uzi");
        if (uziItem is null)
        {
            GD.PrintErr("PlayerInventory: Failed to create uzi weapon.");
            return;
        }

        if (!AddItemToBackpack(uziItem))
        {
            GD.PrintErr("PlayerInventory: Backpack full, could not add starter uzi.");
        }

        var pistolItem = ItemDatabase.CreateWeapon("pistol");
        if (pistolItem is null)
        {
            GD.PrintErr("PlayerInventory: Failed to create pistol weapon.");
            return;
        }

        if (!AddItemToBackpack(pistolItem))
        {
            GD.PrintErr("PlayerInventory: Backpack full, could not add starter pistol.");
        }

        var shotgunItem = ItemDatabase.CreateWeapon("shotgun");
        if (shotgunItem is null)
        {
            GD.PrintErr("PlayerInventory: Failed to create shotgun weapon.");
            return;
        }

        if (!AddItemToBackpack(shotgunItem))
        {
            GD.PrintErr("PlayerInventory: Backpack full, could not add starter shotgun.");
        }

        var batItem = ItemDatabase.CreateWeapon("bat");
        if (batItem is null)
        {
            GD.PrintErr("PlayerInventory: Failed to create bat weapon.");
            return;
        }

        if (!AddItemToBackpack(batItem))
        {
            GD.PrintErr("PlayerInventory: Backpack full, could not add starter bat.");
        }

        AddStarterAmmo(AmmoType.Small, 120);
        AddStarterAmmo(AmmoType.Rifle, 60);
        AddStarterAmmo(AmmoType.Shotgun, 30);
    }

    private void AddStarterAmmo(AmmoType ammoType, int amount)
    {
        var ammoItem = ItemDatabase.CreateAmmo(ammoType, amount);
        if (ammoItem is null)
        {
            GD.PrintErr($"PlayerInventory: Failed to create ammo {ammoType}.");
            return;
        }

        if (!AddItemToBackpack(ammoItem))
        {
            GD.PrintErr($"PlayerInventory: Backpack full, could not add starter ammo {ammoType}.");
        }
    }

    public int CountAmmo(AmmoType ammoType)
    {
        var total = 0;
        foreach (var item in _backpack)
        {
            if (item is not AmmoItem ammoItem || ammoItem.AmmoType != ammoType)
            {
                continue;
            }

            total += ammoItem.StackCount;
        }

        return total;
    }

    public int ConsumeAmmo(AmmoType ammoType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var remaining = amount;
        for (var i = 0; i < _backpack.Length; i++)
        {
            var item = _backpack[i];
            if (item is not AmmoItem ammoItem || ammoItem.AmmoType != ammoType)
            {
                continue;
            }

            var take = Mathf.Min(ammoItem.StackCount, remaining);
            ammoItem.StackCount -= take;
            remaining -= take;

            if (ammoItem.StackCount <= 0)
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

    public void NotifyInventoryChanged() => EmitSignal(SignalName.InventoryChanged);

    private bool CanFullyFitStack(Item item)
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

    private bool SetItemInternal(InventorySlotType slotType, int slotIndex, Item? item)
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

    private void SyncWeapons()
    {
        if (_weaponManager is null)
        {
            return;
        }

        var weapons = new List<WeaponData>();
        if (GetItem(InventorySlotType.RightHand) is WeaponItem rightWeapon && rightWeapon.WeaponData is not null)
        {
            weapons.Add(rightWeapon.WeaponData);
        }

        if (GetItem(InventorySlotType.LeftHand) is WeaponItem leftWeapon && leftWeapon.WeaponData is not null)
        {
            weapons.Add(leftWeapon.WeaponData);
        }

        _weaponManager.SetWeapons(weapons, weapons.Count > 0 ? 0 : -1);
    }
}
