namespace DAIgame.UI;

using DAIgame.Core;
using DAIgame.Player;
using Godot;

public partial class InventorySlotControl : PanelContainer
{
    private TextureRect? _icon;
    private ColorRect? _categoryBackground;
    private StyleBoxFlat? _style;
    private ColorRect? _dropOverlay;
    private bool _isDragging;

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
        CustomMinimumSize = new Vector2(48, 48);

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
            UpdateCategoryBackground(null);
            return;
        }

        var item = Inventory.GetItem(SlotType, SlotIndex);
        _icon.Texture = item?.Icon;
        _icon.Modulate = _isDragging ? new Color(1f, 1f, 1f, 0.5f) : Colors.White;
        TooltipText = item?.DisplayName ?? "";
        UpdateCategoryBackground(item);
    }

    private void UpdateCategoryBackground(InventoryItem? item)
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
            InventoryItemType.Weapon => WeaponColor,
            InventoryItemType.Usable => UsableColor,
            InventoryItemType.Outfit or InventoryItemType.Headwear or InventoryItemType.Shoes => WearableColor,
            InventoryItemType.Misc => throw new System.NotImplementedException(),
            _ => AmmoColor // Misc and any others
        };
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Inventory is null)
        {
            return default;
        }

        var item = Inventory.GetItem(SlotType, SlotIndex);
        if (item is null)
        {
            return default;
        }

        GD.Print($"InventorySlot: Starting drag of item '{item.DisplayName}' from {SlotType}[{SlotIndex}]");

        // Set dragging state and reduce icon opacity
        _isDragging = true;
        if (_icon is not null)
        {
            _icon.Modulate = new Color(1f, 1f, 1f, 0.5f);
        }

        var data = new Godot.Collections.Dictionary
        {
            { "from_slot", this }
        };

        // Create composite preview: cursor + item centered at 48x48
        const float iconSize = 48f;
        var holdingCursor = GD.Load<Texture2D>("res://assets/cursor/holding_item.png");
        var cursorSize = new Vector2(holdingCursor.GetWidth(), holdingCursor.GetHeight());

        var previewContainer = new Control
        {
            CustomMinimumSize = cursorSize
        };

        // Add cursor background
        var cursorRect = new TextureRect
        {
            Texture = holdingCursor,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered
        };
        cursorRect.SetAnchorsPreset(LayoutPreset.FullRect);
        previewContainer.AddChild(cursorRect);

        // Add item icon on top, centered within the cursor area
        var itemRect = new TextureRect
        {
            Texture = item.Icon,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            // Center the item icon within the preview
            Position = new Vector2((cursorSize.X - iconSize) / 2f, (cursorSize.Y - iconSize) / 2f),
            Size = new Vector2(iconSize, iconSize)
        };
        previewContainer.AddChild(itemRect);

        // Position so the center of the 48x48 icon aligns with mouse position
        previewContainer.Position = new Vector2(-cursorSize.X / 2f, -cursorSize.Y / 2f);
        SetDragPreview(previewContainer);

        CursorManager.Instance?.SetHoldingItem(true);

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

        if (fromSlot == this || fromSlot.Inventory is null)
        {
            return false;
        }

        var item = fromSlot.Inventory.GetItem(fromSlot.SlotType, fromSlot.SlotIndex);
        if (item is null)
        {
            return false;
        }

        var canPlace = Inventory.CanPlaceItem(item, SlotType);
        if (!canPlace)
        {
            // Show red overlay for invalid drop
            if (_dropOverlay is not null)
            {
                _dropOverlay.Visible = true;
            }
            GD.Print($"InventorySlot: Cannot place '{item.DisplayName}' in {SlotType}[{SlotIndex}] - showing red overlay");
            return false;
        }

        var targetItem = Inventory.GetItem(SlotType, SlotIndex);
        if (targetItem is not null && !Inventory.CanPlaceItem(targetItem, fromSlot.SlotType))
        {
            // Show red overlay for invalid swap
            if (_dropOverlay is not null)
            {
                _dropOverlay.Visible = true;
            }
            GD.Print($"InventorySlot: Cannot swap '{item.DisplayName}' with '{targetItem.DisplayName}' - showing red overlay");
            return false;
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

        // Reset dragging state on source slot
        fromSlot._isDragging = false;
        if (fromSlot._icon is not null)
        {
            fromSlot._icon.Modulate = Colors.White;
        }

        fromSlot.Inventory.TryMoveItem(fromSlot.SlotType, fromSlot.SlotIndex, SlotType, SlotIndex);
        CursorManager.Instance?.SetHoldingItem(false);
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
            if (_icon is not null)
            {
                _icon.Modulate = Colors.White;
            }

            CursorManager.Instance?.SetHoldingItem(false);
            if (_dropOverlay is not null)
            {
                _dropOverlay.Visible = false;
            }
            GD.Print("InventorySlot: Drag ended (canceled or dropped outside)");
        }
    }
}
