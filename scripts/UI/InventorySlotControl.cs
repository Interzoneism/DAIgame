namespace DAIgame.UI;

using DAIgame.Core;
using DAIgame.Core.Items;
using DAIgame.Player;
using Godot;

public partial class InventorySlotControl : PanelContainer
{
    private TextureRect? _icon;
    private Label? _stackLabel;
    private ColorRect? _categoryBackground;
    private StyleBoxFlat? _style;
    private ColorRect? _dropOverlay;
    private bool _isDragging;
    private bool _dragStartedHere;

    // Category colors (weapon, usable, ammo/misc, wearable)
    private static readonly Color WeaponColor = new(0.6f, 0.2f, 0.2f, 0.7f);
    private static readonly Color UsableColor = new(0.2f, 0.5f, 0.2f, 0.7f);
    private static readonly Color AmmoColor = new(0.5f, 0.5f, 0.2f, 0.7f);
    private static readonly Color WearableColor = new(0.2f, 0.3f, 0.6f, 0.7f);
    private static readonly Color DefaultColor = new(0.3f, 0.3f, 0.3f, 0.5f);

    public PlayerInventory? Inventory { get; private set; }
    public InventorySlotType SlotType { get; private set; }
    public int SlotIndex { get; private set; } = -1;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(64, 64);

        _style = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
            BorderColor = new Color(0.3f, 0.3f, 0.3f, 1f),
            CornerDetail = 1
        };
        _style.SetBorderWidthAll(2);
        AddThemeStyleboxOverride("panel", _style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 3);
        margin.AddThemeConstantOverride("margin_top", 3);
        margin.AddThemeConstantOverride("margin_right", 3);
        margin.AddThemeConstantOverride("margin_bottom", 3);
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(margin);

        // Category background (drawn behind the icon)
        _categoryBackground = new ColorRect
        {
            Color = DefaultColor,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };
        _categoryBackground.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddChild(_categoryBackground);

        _icon = new TextureRect
        {
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddChild(_icon);

        _stackLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(0.95f, 0.95f, 0.95f)
        };
        _stackLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _stackLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _stackLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _stackLabel.AddThemeConstantOverride("margin_right", 3);
        _stackLabel.AddThemeConstantOverride("margin_bottom", 3);
        margin.AddChild(_stackLabel);

        _dropOverlay = new ColorRect
        {
            Color = new Color(1f, 0f, 0f, 0.3f),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };
        _dropOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_dropOverlay);

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        Refresh();
    }

    private void OnMouseEntered()
    {
        if (_style is null)
        {
            return;
        }

        _style.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        _style.BorderColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        // Notify cursor manager if hovering over an item
        if (Inventory is not null && Inventory.GetItem(SlotType, SlotIndex) is not null)
        {
            CursorManager.Instance?.SetHoveringItem(true);
        }
    }

    private void OnMouseExited()
    {
        if (_style is null)
        {
            return;
        }

        _style.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        _style.BorderColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        CursorManager.Instance?.SetHoveringItem(false);
        if (_dropOverlay is not null)
        {
            _dropOverlay.Visible = false;
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton)
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
        {
            if (Inventory is null)
            {
                return;
            }

            var rightClickInventoryScreen = InventoryScreen.Instance;
            if (rightClickInventoryScreen is null)
            {
                return;
            }

            if (rightClickInventoryScreen.TrySplitStack(Inventory, SlotType, SlotIndex))
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left || mouseButton.Pressed)
        {
            return;
        }

        if (Inventory is null)
        {
            return;
        }

        if (_dragStartedHere)
        {
            return;
        }

        var inventoryScreen = InventoryScreen.Instance;
        if (inventoryScreen is null)
        {
            return;
        }

        if (inventoryScreen.HasHeldItem)
        {
            inventoryScreen.TryPlaceHeldItem(Inventory, SlotType, SlotIndex);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Inventory.GetItem(SlotType, SlotIndex) is null)
        {
            return;
        }

        inventoryScreen.TryBeginHold(Inventory, SlotType, SlotIndex);
        GetViewport().SetInputAsHandled();
    }

    public void Configure(InventorySlotType slotType, int slotIndex)
    {
        SlotType = slotType;
        SlotIndex = slotIndex;
    }

    public void SetInventory(PlayerInventory? inventory)
    {
        Inventory = inventory;
        Refresh();
    }

    public void Refresh()
    {
        if (_icon is null)
        {
            return;
        }

        if (Inventory is null)
        {
            _icon.Texture = null;
            _icon.Modulate = Colors.White;
            TooltipText = "";
            if (_stackLabel is not null)
            {
                _stackLabel.Text = "";
            }
            UpdateCategoryBackground(null);
            return;
        }

        var item = Inventory.GetItem(SlotType, SlotIndex);
        _icon.Texture = item?.Icon;
        _icon.Modulate = _isDragging ? new Color(1f, 1f, 1f, 0.5f) : Colors.White;
        TooltipText = item?.DisplayName ?? "";
        if (_stackLabel is not null)
        {
            _stackLabel.Text = item is { StackCount: > 1 } ? item.StackCount.ToString() : "";
        }
        UpdateCategoryBackground(item);
    }

    private void UpdateCategoryBackground(Item? item)
    {
        if (_categoryBackground is null)
        {
            return;
        }

        if (item is null)
        {
            _categoryBackground.Visible = false;
            return;
        }

        _categoryBackground.Visible = true;
        _categoryBackground.Color = item.ItemType switch
        {
            ItemType.Weapon => WeaponColor,
            ItemType.Usable => UsableColor,
            ItemType.Ammo => AmmoColor,
            ItemType.Outfit or ItemType.Headwear or ItemType.Shoes => WearableColor,
            ItemType.Misc => throw new System.NotImplementedException(),
            _ => DefaultColor
        };
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Inventory is null)
        {
            return default;
        }

        var inventoryScreen = InventoryScreen.Instance;
        if (inventoryScreen is null || inventoryScreen.HasHeldItem)
        {
            return default;
        }

        var item = Inventory.GetItem(SlotType, SlotIndex);
        if (item is null)
        {
            return default;
        }

        if (!inventoryScreen.TryBeginHold(Inventory, SlotType, SlotIndex))
        {
            return default;
        }

        GD.Print($"InventorySlot: Starting drag of item '{item.DisplayName}' from {SlotType}[{SlotIndex}]");

        // Set dragging state and reduce icon opacity
        _isDragging = true;
        _dragStartedHere = true;
        if (_icon is not null)
        {
            _icon.Modulate = new Color(1f, 1f, 1f, 0.5f);
        }

        var data = new Godot.Collections.Dictionary
        {
            { "from_slot", this }
        };

        // Hide Godot's built-in drag preview; the inventory screen draws the held icon centered on the cursor.
        var emptyPreview = new Control
        {
            CustomMinimumSize = Vector2.One,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetDragPreview(emptyPreview);

        inventoryScreen.SetDragActive(true);

        return data;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (Inventory is null || data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var dict = (Godot.Collections.Dictionary)data;
        if (!dict.ContainsKey("from_slot"))
        {
            return false;
        }

        var fromSlotVariant = dict["from_slot"];
        if (fromSlotVariant.VariantType != Variant.Type.Object)
        {
            return false;
        }

        if (fromSlotVariant.AsGodotObject() is not InventorySlotControl fromSlot)
        {
            return false;
        }

        if (fromSlot.Inventory is null)
        {
            return false;
        }

        var inventoryScreen = InventoryScreen.Instance;
        if (inventoryScreen is null || !inventoryScreen.HasHeldItem || inventoryScreen.HeldItem is null)
        {
            return false;
        }

        var item = inventoryScreen.HeldItem;
        var canPlace = Inventory.CanPlaceItem(item, SlotType);
        var targetItem = Inventory.GetItem(SlotType, SlotIndex);
        var canSwap = targetItem is null || Inventory.CanPlaceItem(targetItem, inventoryScreen.HeldFromSlotType);
        if (!canPlace || !canSwap)
        {
            // Show red overlay for invalid swap
            if (_dropOverlay is not null)
            {
                _dropOverlay.Visible = true;
            }
            GD.Print($"InventorySlot: Incompatible drop for '{item.DisplayName}' at {SlotType}[{SlotIndex}] - sending to backpack");
            return true;
        }

        // Valid drop - hide overlay
        if (_dropOverlay is not null)
        {
            _dropOverlay.Visible = false;
        }

        return true;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (Inventory is null || data.VariantType != Variant.Type.Dictionary)
        {
            CursorManager.Instance?.SetHoldingItem(false);
            return;
        }

        var dict = (Godot.Collections.Dictionary)data;
        var fromSlotVariant = dict["from_slot"];
        if (fromSlotVariant.VariantType != Variant.Type.Object)
        {
            CursorManager.Instance?.SetHoldingItem(false);
            return;
        }

        if (fromSlotVariant.AsGodotObject() is not InventorySlotControl fromSlot)
        {
            CursorManager.Instance?.SetHoldingItem(false);
            return;
        }

        if (fromSlot.Inventory is null)
        {
            CursorManager.Instance?.SetHoldingItem(false);
            return;
        }

        var inventoryScreen = InventoryScreen.Instance;
        if (inventoryScreen is null)
        {
            CursorManager.Instance?.SetHoldingItem(false);
            return;
        }

        // Reset dragging state on source slot
        fromSlot._isDragging = false;
        fromSlot._dragStartedHere = false;
        if (fromSlot._icon is not null)
        {
            fromSlot._icon.Modulate = Colors.White;
        }

        inventoryScreen.TryPlaceHeldItem(Inventory, SlotType, SlotIndex);
        inventoryScreen.SetDragActive(false);
        GD.Print($"InventorySlot: Dropped item into {SlotType}[{SlotIndex}]");

        // Hide drop overlay after drop
        if (_dropOverlay is not null)
        {
            _dropOverlay.Visible = false;
        }
    }

    public override void _Notification(int what)
    {
        // Handle drag cancel (e.g., pressing ESC or dropping outside valid area)
        if (what == NotificationDragEnd)
        {
            // Reset dragging state and restore icon opacity
            _isDragging = false;
            _dragStartedHere = false;
            if (_icon is not null)
            {
                _icon.Modulate = Colors.White;
            }

            var inventoryScreen = InventoryScreen.Instance;
            inventoryScreen?.SetDragActive(false);
            if (inventoryScreen is not null && inventoryScreen.HasHeldItem && Inventory is not null)
            {
                inventoryScreen.PlaceHeldItemInBackpack(Inventory);
            }

            if (_dropOverlay is not null)
            {
                _dropOverlay.Visible = false;
            }
            GD.Print("InventorySlot: Drag ended (canceled or dropped outside)");
        }
    }
}
