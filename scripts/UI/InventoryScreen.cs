namespace DAIgame.UI;

using System.Collections.Generic;
using DAIgame.Core;
using DAIgame.Player;
using Godot;

public partial class InventoryScreen : CanvasLayer
{
    private const int SlotSize = 48;
    private const int DefaultBackpackColumns = 6;
    private const int DefaultBackpackRows = 5;

    private Control? _root;
    private GridContainer? _backpackGrid;
    private PlayerInventory? _inventory;
    private readonly List<InventorySlotControl> _slots = [];
    private readonly List<InventorySlotControl> _backpackSlots = [];
    private int _backpackColumns = DefaultBackpackColumns;
    private int _backpackRows = DefaultBackpackRows;
    private bool _isOpen;
    private Font? _mainFont;
    private TextureRect? _heldIcon;
    private PlayerInventory? _heldInventory;
    private bool _isDragActive;

    public static InventoryScreen? Instance { get; private set; }

    public bool HasHeldItem => HeldItem is not null;

    public InventoryItem? HeldItem { get; private set; }

    public InventorySlotType HeldFromSlotType { get; private set; }

    public int HeldFromSlotIndex { get; private set; } = -1;

    public override void _Ready()
    {
        Instance = this;
        Layer = 10;
        CreateUI(DefaultBackpackColumns, DefaultBackpackRows);
        if (_root is not null)
        {
            _root.Visible = false;
        }
        _isOpen = false;
        UpdateInventoryReference();
    }

    public override void _Process(double delta)
    {
        UpdateInventoryReference();
        UpdateHeldIconPosition();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("OpenInventory"))
        {
            _isOpen = !_isOpen;
            if (_root is not null)
            {
                _root.Visible = _isOpen;
            }
            CursorManager.Instance?.SetInventoryOpen(_isOpen);

            // Clear hover and holding states when closing inventory
            if (!_isOpen)
            {
                if (HeldItem is not null && _inventory is not null)
                {
                    PlaceHeldItemInBackpack(_inventory);
                }
                CursorManager.Instance?.SetHoveringItem(false);
                CursorManager.Instance?.SetHoldingItem(false);
                SetDragActive(false);
            }

            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateInventoryReference()
    {
        if (_inventory is not null && IsInstanceValid(_inventory))
        {
            return;
        }

        var players = GetTree().GetNodesInGroup("player");
        if (players.Count == 0)
        {
            return;
        }

        if (players[0] is not Node player)
        {
            return;
        }

        var inventory = player.GetNodeOrNull<PlayerInventory>("PlayerInventory");
        if (inventory is null)
        {
            return;
        }

        BindInventory(inventory);
    }

    private void BindInventory(PlayerInventory inventory)
    {
        if (_inventory == inventory)
        {
            return;
        }

        if (_inventory is not null)
        {
            _inventory.InventoryChanged -= RefreshAllSlots;
        }

        _inventory = inventory;
        _inventory.InventoryChanged += RefreshAllSlots;

        if (_inventory.BackpackColumns != _backpackColumns || _inventory.BackpackRows != _backpackRows)
        {
            RebuildBackpackSlots(_inventory.BackpackColumns, _inventory.BackpackRows);
        }

        foreach (var slot in _slots)
        {
            slot.SetInventory(_inventory);
        }

        RefreshAllSlots();
    }

    private void CreateUI(int backpackColumns, int backpackRows)
    {
        _mainFont = GD.Load<Font>("res://assets/fonts/VCR_OSD_MONO.ttf");

        _root = new Control
        {
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        AddChild(_root);

        var dim = new ColorRect
        {
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.85f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _root.AddChild(dim);

        var panel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        _root.AddChild(panel);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.05f, 1f),
            BorderColor = new Color(0.6f, 0.6f, 0.6f, 1f),
            CornerDetail = 1
        };
        panelStyle.SetBorderWidthAll(2);
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);

        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 20);
        margin.AddChild(container);

        var title = new Label
        {
            Text = "INVENTORY",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (_mainFont is not null)
        {
            title.AddThemeFontOverride("font", _mainFont);
            title.AddThemeFontSizeOverride("font_size", 32);
        }
        container.AddChild(title);

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 32);
        container.AddChild(body);

        CreateEquipmentPanel(body);
        CreateBackpackPanel(body, backpackColumns, backpackRows);

        _heldIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(SlotSize, SlotSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            ZAsRelative = false,
            ZIndex = 100,
            Size = new Vector2(SlotSize, SlotSize)
        };
        _root.AddChild(_heldIcon);
    }

    private void CreateEquipmentPanel(HBoxContainer parent)
    {
        var equipBox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(240, 0)
        };
        equipBox.AddThemeConstantOverride("separation", 12);
        parent.AddChild(equipBox);

        var label = new Label { Text = "CHARACTER" };
        if (_mainFont is not null)
        {
            label.AddThemeFontOverride("font", _mainFont);
            label.AddThemeFontSizeOverride("font_size", 24);
            label.Modulate = new Color(0.7f, 0.7f, 0.7f);
        }
        equipBox.AddChild(label);

        var equipGrid = new GridContainer
        {
            Columns = 2
        };
        equipGrid.AddThemeConstantOverride("h_separation", 8);
        equipGrid.AddThemeConstantOverride("v_separation", 8);
        equipBox.AddChild(equipGrid);

        AddEquipmentSlot(equipGrid, "Headwear", InventorySlotType.Headwear);
        AddEquipmentSlot(equipGrid, "Outfit", InventorySlotType.Outfit);
        AddEquipmentSlot(equipGrid, "Shoes", InventorySlotType.Shoes);
        AddEquipmentSlot(equipGrid, "Left Hand", InventorySlotType.LeftHand);
        AddEquipmentSlot(equipGrid, "Right Hand", InventorySlotType.RightHand);
        AddEquipmentSlot(equipGrid, "Usable 1", InventorySlotType.Usable1);
        AddEquipmentSlot(equipGrid, "Usable 2", InventorySlotType.Usable2);
    }

    private void CreateBackpackPanel(HBoxContainer parent, int columns, int rows)
    {
        _backpackColumns = columns;
        _backpackRows = rows;

        var backpackBox = new VBoxContainer();
        backpackBox.AddThemeConstantOverride("separation", 12);
        parent.AddChild(backpackBox);

        var label = new Label { Text = "BACKPACK" };
        if (_mainFont is not null)
        {
            label.AddThemeFontOverride("font", _mainFont);
            label.AddThemeFontSizeOverride("font_size", 24);
            label.Modulate = new Color(0.7f, 0.7f, 0.7f);
        }
        backpackBox.AddChild(label);

        _backpackGrid = new GridContainer
        {
            Columns = columns
        };
        _backpackGrid.AddThemeConstantOverride("h_separation", 6);
        _backpackGrid.AddThemeConstantOverride("v_separation", 6);
        backpackBox.AddChild(_backpackGrid);

        BuildBackpackSlots(columns, rows);
    }

    private void AddEquipmentSlot(GridContainer grid, string labelText, InventorySlotType slotType)
    {
        var slotLabel = new Label
        {
            Text = labelText.ToUpper(System.Globalization.CultureInfo.CurrentCulture),
            CustomMinimumSize = new Vector2(100, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (_mainFont is not null)
        {
            slotLabel.AddThemeFontOverride("font", _mainFont);
            slotLabel.AddThemeFontSizeOverride("font_size", 16);
            slotLabel.Modulate = new Color(0.8f, 0.8f, 0.8f);
        }
        grid.AddChild(slotLabel);

        var slot = CreateSlot(slotType, -1);
        grid.AddChild(slot);
    }

    private void BuildBackpackSlots(int columns, int rows)
    {
        if (_backpackGrid is null)
        {
            return;
        }

        for (var i = 0; i < columns * rows; i++)
        {
            var slot = CreateSlot(InventorySlotType.Backpack, i);
            _backpackGrid.AddChild(slot);
            _backpackSlots.Add(slot);
        }
    }

    private InventorySlotControl CreateSlot(InventorySlotType slotType, int slotIndex)
    {
        var slot = new InventorySlotControl
        {
            CustomMinimumSize = new Vector2(SlotSize, SlotSize)
        };
        slot.Configure(slotType, slotIndex);
        _slots.Add(slot);
        if (_inventory is not null)
        {
            slot.SetInventory(_inventory);
        }

        return slot;
    }

    private void RebuildBackpackSlots(int columns, int rows)
    {
        _backpackColumns = columns;
        _backpackRows = rows;

        foreach (var slot in _backpackSlots)
        {
            _slots.Remove(slot);
            slot.QueueFree();
        }
        _backpackSlots.Clear();

        if (_backpackGrid is null)
        {
            return;
        }

        _backpackGrid.Columns = columns;
        BuildBackpackSlots(columns, rows);
    }

    private void RefreshAllSlots()
    {
        foreach (var slot in _slots)
        {
            slot.Refresh();
        }
    }

    public bool TryBeginHold(PlayerInventory inventory, InventorySlotType fromType, int fromIndex)
    {
        if (HeldItem is not null)
        {
            return false;
        }

        var item = inventory.GetItem(fromType, fromIndex);
        if (item is null)
        {
            return false;
        }

        if (!inventory.SetItem(fromType, fromIndex, null))
        {
            GD.PrintErr($"InventoryScreen: Failed to clear slot {fromType}[{fromIndex}] for hold.");
            return false;
        }

        HeldItem = item;
        HeldFromSlotType = fromType;
        HeldFromSlotIndex = fromIndex;
        _heldInventory = inventory;
        CursorManager.Instance?.SetHoldingItem(true);
        UpdateHeldIcon();
        GD.Print($"InventoryScreen: Holding '{item.DisplayName}' from {fromType}[{fromIndex}].");
        return true;
    }

    public bool TryPlaceHeldItem(PlayerInventory inventory, InventorySlotType toType, int toIndex)
    {
        if (HeldItem is null)
        {
            return false;
        }

        var targetItem = inventory.GetItem(toType, toIndex);
        if (toType == InventorySlotType.Backpack && targetItem is not null && HeldItem.CanStackWith(targetItem))
        {
            var space = targetItem.MaxStack - targetItem.StackCount;
            if (space > 0)
            {
                var toAdd = Mathf.Min(space, HeldItem.StackCount);
                targetItem.StackCount += toAdd;
                HeldItem.StackCount -= toAdd;
                inventory.NotifyInventoryChanged();

                if (HeldItem.StackCount <= 0)
                {
                    ClearHeldItem();
                }

                return true;
            }
        }

        if (!inventory.CanPlaceItem(HeldItem, toType))
        {
            GD.Print($"InventoryScreen: {toType}[{toIndex}] incompatible, sending to backpack.");
            return PlaceHeldItemInBackpack(inventory);
        }

        if (targetItem is not null && !inventory.CanPlaceItem(targetItem, HeldFromSlotType))
        {
            GD.Print($"InventoryScreen: Swap incompatible with {toType}[{toIndex}], sending to backpack.");
            return PlaceHeldItemInBackpack(inventory);
        }

        if (!inventory.SetItem(toType, toIndex, HeldItem))
        {
            GD.PrintErr($"InventoryScreen: Failed to place held item in {toType}[{toIndex}].");
            return false;
        }

        if (targetItem is not null)
        {
            if (!inventory.SetItem(HeldFromSlotType, HeldFromSlotIndex, targetItem))
            {
                if (!inventory.AddItemToBackpack(targetItem))
                {
                    inventory.SetItem(toType, toIndex, targetItem);
                    GD.PrintErr("InventoryScreen: Failed to swap items; backpack full.");
                    return TryReturnHeldToSource();
                }
            }
        }

        ClearHeldItem();
        return true;
    }

    public bool PlaceHeldItemInBackpack(PlayerInventory inventory)
    {
        if (HeldItem is null)
        {
            return false;
        }

        if (inventory.AddItemToBackpack(HeldItem))
        {
            GD.Print("InventoryScreen: Placed held item in backpack.");
            ClearHeldItem();
            return true;
        }

        GD.PrintErr("InventoryScreen: Backpack full, returning held item to original slot.");
        return TryReturnHeldToSource();
    }

    public void SetDragActive(bool active)
    {
        _isDragActive = active;
        UpdateHeldIcon();
    }

    private bool TryReturnHeldToSource()
    {
        if (HeldItem is null || _heldInventory is null)
        {
            return false;
        }

        if (HeldFromSlotIndex < 0)
        {
            return PlaceHeldItemInBackpack(_heldInventory);
        }

        if (!_heldInventory.SetItem(HeldFromSlotType, HeldFromSlotIndex, HeldItem))
        {
            GD.PrintErr("InventoryScreen: Failed to return held item to its original slot.");
            return false;
        }

        ClearHeldItem();
        return true;
    }

    private void ClearHeldItem()
    {
        HeldItem = null;
        _heldInventory = null;
        HeldFromSlotIndex = -1;
        CursorManager.Instance?.SetHoldingItem(false);
        UpdateHeldIcon();
    }

    public bool TrySplitStack(PlayerInventory inventory, InventorySlotType fromType, int fromIndex)
    {
        if (HeldItem is not null)
        {
            return false;
        }

        var item = inventory.GetItem(fromType, fromIndex);
        if (item is null || !item.IsStackable || item.StackCount <= 1)
        {
            return false;
        }

        var splitCount = item.StackCount / 2;
        if (splitCount <= 0)
        {
            return false;
        }

        item.StackCount -= splitCount;
        HeldItem = item.CreateStackCopy(splitCount);
        HeldFromSlotType = fromType;
        HeldFromSlotIndex = -1;
        _heldInventory = inventory;
        CursorManager.Instance?.SetHoldingItem(true);
        inventory.NotifyInventoryChanged();
        UpdateHeldIcon();
        GD.Print($"InventoryScreen: Split stack, holding {HeldItem.StackCount} of '{item.DisplayName}'.");
        return true;
    }

    private void UpdateHeldIcon()
    {
        if (_heldIcon is null)
        {
            return;
        }

        if (HeldItem is null || !_isOpen || _isDragActive)
        {
            _heldIcon.Visible = false;
            _heldIcon.Texture = null;
            return;
        }

        _heldIcon.Texture = HeldItem.Icon;
        _heldIcon.Visible = true;
    }

    private void UpdateHeldIconPosition()
    {
        if (_heldIcon is null || !_heldIcon.Visible)
        {
            return;
        }

        var mousePos = GetViewport().GetMousePosition();
        _heldIcon.Position = mousePos - (_heldIcon.Size / 2f);
    }

    public override void _ExitTree()
    {
        if (_inventory is not null)
        {
            _inventory.InventoryChanged -= RefreshAllSlots;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
