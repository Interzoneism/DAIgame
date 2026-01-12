namespace DAIgame.UI;

using DAIgame.Core;
using DAIgame.Player;
using Godot;

public partial class InventorySlotControl : PanelContainer
{
    private TextureRect? _icon;
    private StyleBoxFlat? _style;
    private ColorRect? _dropOverlay;

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
            TooltipText = "";
            return;
        }

        var item = Inventory.GetItem(SlotType, SlotIndex);
        _icon.Texture = item?.Icon;
        TooltipText = item?.DisplayName ?? "";
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

        var data = new Godot.Collections.Dictionary
        {
            { "from_slot", this }
        };

        // Create composite preview: cursor + item
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

        // Add item icon on top
        var itemRect = new TextureRect
        {
            Texture = item.Icon,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
        };
        itemRect.SetAnchorsPreset(LayoutPreset.FullRect);
        previewContainer.AddChild(itemRect);

        // Position so cursor center aligns with mouse position
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
            CursorManager.Instance?.SetHoldingItem(false);
            if (_dropOverlay is not null)
            {
                _dropOverlay.Visible = false;
            }
            GD.Print("InventorySlot: Drag ended (canceled or dropped outside)");
        }
    }
}
