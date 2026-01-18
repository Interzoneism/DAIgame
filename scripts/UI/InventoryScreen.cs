namespace DAIgame.UI;

using System.Collections.Generic;
using DAIgame.Core;
using DAIgame.Core.Items;
using DAIgame.Loot;
using DAIgame.Player;
using Godot;

public partial class InventoryScreen : CanvasLayer
{
    private const int SlotSize = 64;
    private const int DefaultBackpackColumns = 6;
    private const int DefaultBackpackRows = 5;
    private const int LootPanelColumns = 2;
    private const int TitleBarHeight = 32;

    private Control? _root;
    private GridContainer? _backpackGrid;
    private PlayerInventory? _inventory;
    private readonly List<InventorySlotControl> _slots = [];
    private readonly List<InventorySlotControl> _backpackSlots = [];
    private int _backpackColumns = DefaultBackpackColumns;
    private int _backpackRows = DefaultBackpackRows;
    private bool _isOpen;
    private Font? _mainFont;
    private PanelContainer? _heldPreview;
    private TextureRect? _heldIcon;
    private Label? _heldStackLabel;
    private TextureRect? _dragPointer;
    private PlayerInventory? _heldInventory;
    private bool _isDragActive;
    private Texture2D? _pointerCursor;

    // Loot window fields
    private HBoxContainer? _mainContainer;
    private readonly List<LootWindow> _lootWindows = [];
    private readonly List<ILootable> _activeLootables = [];
    private ILootable? _heldFromLootable;
    private LootWindow? _groundItemsWindow;
    private LootHighlighter? _lootHighlighter;
    private bool _isLootFocus;
    private ILootable? _focusedLootable;

    // Dragging fields
    private PanelContainer? _inventoryPanel;
    private bool _isDraggingPanel;
    private Vector2 _dragOffset;

    private static readonly Vector2 DragPointerHotspot = new(4, 2);

    public static InventoryScreen? Instance { get; private set; }

    public bool HasHeldItem => HeldItem is not null;

    public Item? HeldItem { get; private set; }

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

        // Defer connecting to LootHighlighter in case it's not ready yet
        CallDeferred(MethodName.ConnectToLootHighlighter);
    }

    private void ConnectToLootHighlighter()
    {
        _lootHighlighter = LootHighlighter.Instance;
        if (_lootHighlighter is not null)
        {
            _lootHighlighter.ViewLootToggled += OnViewLootToggled;
        }
    }

    private void OnViewLootToggled(bool isActive)
    {
        if (!_isOpen)
        {
            return;
        }

        if (_isLootFocus)
        {
            GD.Print("InventoryScreen: ViewLoot toggled while focused; keeping focused loot window.");
            return;
        }

        if (isActive)
        {
            ShowLootPanels();
        }
        else
        {
            ClearLootPanels();
        }
    }

    public override void _Process(double delta)
    {
        UpdateInventoryReference();
        UpdateHeldIconPosition();
        UpdateDragPointerPosition();
        if (_isOpen)
        {
            UpdateLootPanels();
        }
    }

    /// <summary>
    /// Opens the inventory screen with a specific lootable container displayed.
    /// </summary>
    public void OpenWithLootable(ILootable lootable)
    {
        if (_isOpen)
        {
            // Already open, just show the new lootable
            ClearLootPanels();
            ShowSingleLootable(lootable);
            return;
        }

        _isOpen = true;
        if (_root is not null)
        {
            _root.Visible = true;
        }
        CursorManager.Instance?.SetInventoryOpen(true);
        UpdateDragPointerVisibility();

        // Show only the targeted lootable
        ShowSingleLootable(lootable);
        if (LootHighlighter.Instance?.IsViewLootActive == true)
        {
            GD.Print("InventoryScreen: Loot focus active; suppressing ViewLoot panels.");
        }
    }

    private void ShowSingleLootable(ILootable lootable)
    {
        ClearLootPanels();
        _isLootFocus = true;
        _focusedLootable = lootable;



        // Calculate position for the loot window (to the right of inventory)
        float inventoryRight;

        float inventoryTop;
        if (_inventoryPanel is not null)
        {
            inventoryRight = _inventoryPanel.Position.X + _inventoryPanel.Size.X + 20f;
            inventoryTop = _inventoryPanel.Position.Y;
        }
        else
        {
            var viewport = GetViewport();
            inventoryRight = (viewport.GetVisibleRect().Size.X / 2f) + 300f;
            inventoryTop = 100f;
        }

        var window = CreateLootWindow(lootable);
        window.Position = new Vector2(inventoryRight, inventoryTop);
        _activeLootables.Add(lootable);

        GD.Print($"InventoryScreen: Opened with lootable '{lootable.LootDisplayName}'");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("OpenInventory") || (@event.IsActionPressed("ui_cancel") && _isOpen))
        {
            _isOpen = !_isOpen;
            if (_root is not null)
            {
                _root.Visible = _isOpen;
            }
            CursorManager.Instance?.SetInventoryOpen(_isOpen);
            UpdateDragPointerVisibility();

            if (_isOpen)
            {
                // Check if ViewLoot is active and show nearby lootables
                if (LootHighlighter.Instance?.IsViewLootActive == true)
                {
                    ShowLootPanels();
                }
            }
            else
            {
                // Clear hover and holding states when closing inventory
                if (HeldItem is not null && _inventory is not null)
                {
                    PlaceHeldItemInBackpack(_inventory);
                }
                CursorManager.Instance?.SetHoveringItem(false);
                CursorManager.Instance?.SetHoldingItem(false);
                SetDragActive(false);
                ClearLootPanels();
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
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_root);

        _pointerCursor = GD.Load<Texture2D>("res://assets/cursor/pointer.png");

        _inventoryPanel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        _root.AddChild(_inventoryPanel);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.05f, 0.95f),
            BorderColor = new Color(0.6f, 0.6f, 0.6f, 1f),
            CornerDetail = 1
        };
        panelStyle.SetBorderWidthAll(2);
        _inventoryPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var outerContainer = new VBoxContainer();
        outerContainer.AddThemeConstantOverride("separation", 0);
        _inventoryPanel.AddChild(outerContainer);

        // Draggable title bar
        var titleBar = new Panel
        {
            CustomMinimumSize = new Vector2(0, TitleBarHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        var titleBarStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.15f, 1f)
        };
        titleBar.AddThemeStyleboxOverride("panel", titleBarStyle);
        outerContainer.AddChild(titleBar);

        var title = new Label
        {
            Text = "INVENTORY",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        if (_mainFont is not null)
        {
            title.AddThemeFontOverride("font", _mainFont);
            title.AddThemeFontSizeOverride("font_size", 24);
        }
        titleBar.AddChild(title);

        // Connect title bar mouse events for dragging
        titleBar.GuiInput += OnTitleBarInput;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        outerContainer.AddChild(margin);

        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 20);
        margin.AddChild(container);

        _mainContainer = new HBoxContainer();
        _mainContainer.AddThemeConstantOverride("separation", 32);
        container.AddChild(_mainContainer);

        CreateEquipmentPanel(_mainContainer);
        CreateBackpackPanel(_mainContainer, backpackColumns, backpackRows);

        _heldPreview = new PanelContainer
        {
            CustomMinimumSize = new Vector2(SlotSize, SlotSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            ZAsRelative = false,
            ZIndex = 100,
            Size = new Vector2(SlotSize, SlotSize)
        };
        var heldStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
            BorderColor = new Color(0.7f, 0.7f, 0.7f, 1f),
            CornerDetail = 1
        };
        heldStyle.SetBorderWidthAll(2);
        _heldPreview.AddThemeStyleboxOverride("panel", heldStyle);
        _root.AddChild(_heldPreview);

        var heldMargin = new MarginContainer();
        heldMargin.AddThemeConstantOverride("margin_left", 3);
        heldMargin.AddThemeConstantOverride("margin_top", 3);
        heldMargin.AddThemeConstantOverride("margin_right", 3);
        heldMargin.AddThemeConstantOverride("margin_bottom", 3);
        heldMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        heldMargin.MouseFilter = Control.MouseFilterEnum.Ignore;
        _heldPreview.AddChild(heldMargin);

        _heldIcon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        heldMargin.AddChild(_heldIcon);

        _heldStackLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(0.95f, 0.95f, 0.95f)
        };
        _heldStackLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _heldStackLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _heldStackLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _heldStackLabel.AddThemeConstantOverride("margin_right", 3);
        _heldStackLabel.AddThemeConstantOverride("margin_bottom", 3);
        heldMargin.AddChild(_heldStackLabel);

        _dragPointer = new TextureRect
        {
            Texture = _pointerCursor,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            ZAsRelative = false,
            ZIndex = 200
        };
        _root.AddChild(_dragPointer);
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

        // Check if this is a swap where the held item came from a lootable
        var fromLootable = _heldFromLootable is not null;

        if (targetItem is not null && !fromLootable && !inventory.CanPlaceItem(targetItem, HeldFromSlotType))
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
            // If picked from lootable, put swapped item back in lootable
            if (fromLootable && _heldFromLootable is not null)
            {
                if (!_heldFromLootable.SetItemAt(HeldFromSlotIndex, targetItem))
                {
                    // Failed to put in lootable, try player backpack
                    if (!inventory.AddItemToBackpack(targetItem))
                    {
                        inventory.SetItem(toType, toIndex, targetItem);
                        GD.PrintErr("InventoryScreen: Failed to swap items; both lootable and backpack full.");
                        return false;
                    }
                }
            }
            else if (_heldInventory is not null && !inventory.SetItem(HeldFromSlotType, HeldFromSlotIndex, targetItem))
            {
                if (!inventory.AddItemToBackpack(targetItem))
                {
                    inventory.SetItem(toType, toIndex, targetItem);
                    GD.PrintErr("InventoryScreen: Failed to swap items; backpack full.");
                    return TryReturnHeldToSource();
                }
            }
            else if (_heldInventory is null && !fromLootable)
            {
                // Edge case: no source inventory, put in backpack
                if (!inventory.AddItemToBackpack(targetItem))
                {
                    inventory.SetItem(toType, toIndex, targetItem);
                    GD.PrintErr("InventoryScreen: Failed to place swapped item; backpack full.");
                    return false;
                }
            }
        }

        ClearHeldItem();
        RefreshLootSlots();
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
        Input.MouseMode = active ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
        CursorManager.Instance?.SetHoldingItem(active);
        UpdateDragPointerVisibility();
        UpdateDragPointerPosition();
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
        _heldFromLootable = null;
        HeldFromSlotIndex = -1;
        CursorManager.Instance?.SetHoldingItem(false);
        UpdateDragPointerVisibility();
        UpdateHeldIcon();
    }

    public bool TryQuickEquip(PlayerInventory inventory, InventorySlotType fromType, int fromIndex)
    {
        if (fromType != InventorySlotType.Backpack)
        {
            return false;
        }

        var item = inventory.GetItem(fromType, fromIndex);
        if (item is null)
        {
            return false;
        }

        var targetSlot = GetPreferredEquipSlot(inventory, item);
        if (targetSlot is null)
        {
            return false;
        }

        if (!inventory.CanPlaceItem(item, targetSlot.Value))
        {
            return false;
        }

        if (!inventory.TryMoveItem(fromType, fromIndex, targetSlot.Value, -1))
        {
            GD.Print($"InventoryScreen: Quick-equip failed for '{item.DisplayName}' into {targetSlot.Value}.");
            return false;
        }

        return true;
    }

    public bool TryQuickLoot(ILootable lootable, int slotIndex)
    {
        if (_inventory is null)
        {
            GD.PrintErr("InventoryScreen: Quick-loot failed; inventory missing.");
            return false;
        }

        var item = lootable.TakeItemAt(slotIndex);
        if (item is null)
        {
            return false;
        }

        if (!_inventory.AddItemToBackpack(item))
        {
            if (!lootable.SetItemAt(slotIndex, item))
            {
                GD.PrintErr("InventoryScreen: Quick-loot failed; could not return item to lootable.");
            }
            return false;
        }

        RefreshLootSlots();
        return true;
    }

    private static InventorySlotType? GetPreferredEquipSlot(PlayerInventory inventory, Item item)
    {
        return item.ItemType switch
        {
            ItemType.Weapon => InventorySlotType.RightHand,
            ItemType.Usable => inventory.GetItem(InventorySlotType.Usable1) is null
                ? InventorySlotType.Usable1
                : inventory.GetItem(InventorySlotType.Usable2) is null
                    ? InventorySlotType.Usable2
                    : InventorySlotType.Usable1,
            ItemType.Outfit => InventorySlotType.Outfit,
            ItemType.Headwear => InventorySlotType.Headwear,
            ItemType.Shoes => InventorySlotType.Shoes,
            _ => null
        };
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
        if (_heldPreview is null || _heldIcon is null)
        {
            return;
        }

        if (HeldItem is null || !_isOpen)
        {
            _heldPreview.Visible = false;
            _heldIcon.Texture = null;
            if (_heldStackLabel is not null)
            {
                _heldStackLabel.Text = "";
            }
            return;
        }

        _heldIcon.Texture = HeldItem.Icon;
        if (_heldStackLabel is not null)
        {
            _heldStackLabel.Text = HeldItem.StackCount > 1 ? HeldItem.StackCount.ToString() : "";
        }
        _heldPreview.Visible = true;
    }

    private void UpdateHeldIconPosition()
    {
        if (_heldPreview is null || !_heldPreview.Visible)
        {
            return;
        }

        var mousePos = GetViewport().GetMousePosition();
        _heldPreview.Position = mousePos - (_heldPreview.Size / 2f);
    }

    private void UpdateDragPointerVisibility()
    {
        if (_dragPointer is null)
        {
            return;
        }

        _dragPointer.Visible = _isOpen && _isDragActive && _pointerCursor is not null;
    }

    private void UpdateDragPointerPosition()
    {
        if (_dragPointer is null || !_dragPointer.Visible)
        {
            return;
        }

        var mousePos = GetViewport().GetMousePosition();
        _dragPointer.Position = mousePos - DragPointerHotspot;
    }

    private void OnTitleBarInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    _isDraggingPanel = true;
                    if (_inventoryPanel is not null)
                    {
                        _dragOffset = _inventoryPanel.GlobalPosition - mouseButton.GlobalPosition;
                    }
                }
                else
                {
                    _isDraggingPanel = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDraggingPanel && _inventoryPanel is not null)
        {
            // Reset anchors so we can position freely
            _inventoryPanel.AnchorLeft = 0f;
            _inventoryPanel.AnchorTop = 0f;
            _inventoryPanel.AnchorRight = 0f;
            _inventoryPanel.AnchorBottom = 0f;
            _inventoryPanel.GlobalPosition = mouseMotion.GlobalPosition + _dragOffset;
        }
    }

    public override void _ExitTree()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (_inventory is not null)
        {
            _inventory.InventoryChanged -= RefreshAllSlots;
        }

        if (_lootHighlighter is not null)
        {
            _lootHighlighter.ViewLootToggled -= OnViewLootToggled;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region Loot Window Methods

    private void ShowLootPanels()
    {
        ClearLootPanels();

        var highlighter = LootHighlighter.Instance;
        if (highlighter is null)
        {
            return;
        }

        var lootables = highlighter.GetLootablesInRange();
        if (lootables.Count == 0)
        {
            GD.Print("InventoryScreen: No lootables in range");
            return;
        }

        // Separate ground items from containers
        var groundItems = new List<ILootable>();
        var containers = new List<ILootable>();

        foreach (var lootable in lootables)
        {
            if (lootable.LootDisplayName == "On Ground")
            {
                groundItems.Add(lootable);
            }
            else
            {
                containers.Add(lootable);
            }
        }



        // Calculate starting position for loot windows (to the right of inventory)
        float inventoryRight;

        float inventoryTop;

        float inventoryBottom;
        if (_inventoryPanel is not null)
        {
            // Get the panel's actual position and size
            inventoryRight = _inventoryPanel.Position.X + _inventoryPanel.Size.X + 20f;
            inventoryTop = _inventoryPanel.Position.Y;
            inventoryBottom = _inventoryPanel.Position.Y + _inventoryPanel.Size.Y;
        }
        else
        {
            // Fallback to screen center
            var viewport = GetViewport();
            inventoryRight = (viewport.GetVisibleRect().Size.X / 2f) + 300f;
            inventoryTop = 100f;
            inventoryBottom = viewport.GetVisibleRect().Size.Y - 100f;
        }

        var currentX = inventoryRight;
        var currentY = inventoryTop;
        var columnWidth = 0f;
        const float WindowSpacing = 10f;
        const float EstimatedWindowHeight = 180f;

        // Create windows for containers
        foreach (var container in containers)
        {
            var window = CreateLootWindow(container);

            // Check if we need to start a new column
            if (currentY + EstimatedWindowHeight > inventoryBottom && currentY > inventoryTop)
            {
                currentX += columnWidth + WindowSpacing;
                currentY = inventoryTop;
                columnWidth = 0f;
            }

            window.Position = new Vector2(currentX, currentY);
            currentY += EstimatedWindowHeight + WindowSpacing;

            // Track the widest window in this column
            if (window.Size.X > columnWidth)
            {
                columnWidth = window.Size.X;
            }

            _activeLootables.Add(container);
        }

        // Create ground items window if there are any
        if (groundItems.Count > 0)
        {
            // Check if we need to start a new column for ground items
            if (currentY + EstimatedWindowHeight > inventoryBottom && currentY > inventoryTop)
            {
                currentX += columnWidth + WindowSpacing;
                currentY = inventoryTop;
            }

            CreateGroundItemsWindow(groundItems, new Vector2(currentX, currentY));
            _activeLootables.AddRange(groundItems);
        }

        GD.Print($"InventoryScreen: Showing {containers.Count} container window(s) and {groundItems.Count} ground item(s)");
    }

    private LootWindow CreateLootWindow(ILootable lootable)
    {
        var window = new LootWindow();
        _root?.AddChild(window);
        window.Configure(lootable, LootPanelColumns);
        window.WindowClosed += OnLootWindowClosed;
        _lootWindows.Add(window);
        return window;
    }

    private void CreateGroundItemsWindow(List<ILootable> groundItems, Vector2 position)
    {
        _groundItemsWindow = new LootWindow();
        _root?.AddChild(_groundItemsWindow);
        _groundItemsWindow.ConfigureForGroundItems(groundItems, LootPanelColumns);
        _groundItemsWindow.Position = position;
        _groundItemsWindow.WindowClosed += OnLootWindowClosed;
        _lootWindows.Add(_groundItemsWindow);
    }

    private void OnLootWindowClosed(LootWindow window)
    {
        _lootWindows.Remove(window);
        if (window == _groundItemsWindow)
        {
            _groundItemsWindow = null;
        }

        if (_isLootFocus && window.Lootable == _focusedLootable)
        {
            _isLootFocus = false;
            _focusedLootable = null;
        }
    }

    private void ClearLootPanels()
    {
        foreach (var window in _lootWindows)
        {
            window.WindowClosed -= OnLootWindowClosed;
            window.QueueFree();
        }
        _lootWindows.Clear();
        _groundItemsWindow = null;
        _activeLootables.Clear();
        _isLootFocus = false;
        _focusedLootable = null;
    }

    private void UpdateLootPanels()
    {
        // Check if any lootable was removed or went out of range
        var highlighter = LootHighlighter.Instance;
        if (!_isLootFocus && (highlighter is null || !highlighter.IsViewLootActive))
        {
            if (_activeLootables.Count > 0)
            {
                ClearLootPanels();
            }
            return;
        }

        // Refresh all loot windows
        foreach (var window in _lootWindows)
        {
            window.RefreshSlots();
        }

        // Check for empty lootables that should be removed
        for (var i = _activeLootables.Count - 1; i >= 0; i--)
        {
            var lootable = _activeLootables[i];
            var isEmpty = true;
            for (var j = 0; j < lootable.SlotCount; j++)
            {
                if (lootable.GetItemAt(j) is not null)
                {
                    isEmpty = false;
                    break;
                }
            }

            if (isEmpty && lootable.RemoveWhenEmpty)
            {
                lootable.OnBecameEmpty();
            }
        }
    }

    public bool TryBeginHoldFromLootable(ILootable lootable, int slotIndex)
    {
        if (HeldItem is not null)
        {
            return false;
        }

        var item = lootable.TakeItemAt(slotIndex);
        if (item is null)
        {
            return false;
        }

        HeldItem = item;
        HeldFromSlotType = InventorySlotType.Backpack; // Default, not really used for lootables
        HeldFromSlotIndex = slotIndex;
        _heldFromLootable = lootable;
        _heldInventory = null;
        CursorManager.Instance?.SetHoldingItem(true);
        UpdateHeldIcon();
        GD.Print($"InventoryScreen: Holding '{item.DisplayName}' from lootable '{lootable.LootDisplayName}'");
        return true;
    }

    public bool TryPlaceHeldItemInLootable(ILootable lootable, int slotIndex)
    {
        if (HeldItem is null)
        {
            return false;
        }

        var targetItem = lootable.GetItemAt(slotIndex);

        // Try stacking
        if (targetItem is not null && HeldItem.CanStackWith(targetItem))
        {
            var space = targetItem.MaxStack - targetItem.StackCount;
            if (space > 0)
            {
                var toAdd = Mathf.Min(space, HeldItem.StackCount);
                targetItem.StackCount += toAdd;
                HeldItem.StackCount -= toAdd;

                if (HeldItem.StackCount <= 0)
                {
                    ClearHeldItem();
                }

                RefreshLootSlots();
                return true;
            }
        }

        // Swap items
        if (!lootable.SetItemAt(slotIndex, HeldItem))
        {
            GD.PrintErr($"InventoryScreen: Failed to place held item in lootable at slot {slotIndex}");
            return false;
        }

        if (targetItem is not null)
        {
            HeldItem = targetItem;
            UpdateHeldIcon();
        }
        else
        {
            ClearHeldItem();
        }

        RefreshLootSlots();
        return true;
    }

    public bool TrySplitStackFromLootable(ILootable lootable, int slotIndex)
    {
        if (HeldItem is not null)
        {
            return false;
        }

        var item = lootable.GetItemAt(slotIndex);
        if (item is null || !item.IsStackable || item.StackCount <= 1)
        {
            return false;
        }

        // Split in half (ceiling to leave the smaller half in original)
        var splitAmount = Mathf.CeilToInt(item.StackCount / 2.0f);
        item.StackCount -= splitAmount;

        // Create a new item for the held portion
        var splitItem = item.CreateStackCopy(splitAmount);

        HeldItem = splitItem;
        HeldFromSlotType = InventorySlotType.Backpack;
        HeldFromSlotIndex = slotIndex;
        _heldFromLootable = lootable;
        _heldInventory = null;
        UpdateHeldIcon();

        RefreshLootSlots();
        return true;
    }

    private void RefreshLootSlots()
    {
        foreach (var window in _lootWindows)
        {
            window.RefreshSlots();
        }
    }

    #endregion
}
