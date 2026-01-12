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

    public override void _Ready()
    {
        Layer = 10;
        CreateUI(DefaultBackpackColumns, DefaultBackpackRows);
        if (_root is not null)
        {
            _root.Visible = false;
        }
        _isOpen = false;
        UpdateInventoryReference();
    }

    public override void _Process(double delta) => UpdateInventoryReference();

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
                CursorManager.Instance?.SetHoveringItem(false);
                CursorManager.Instance?.SetHoldingItem(false);
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

    public override void _ExitTree()
    {
        if (_inventory is not null)
        {
            _inventory.InventoryChanged -= RefreshAllSlots;
        }
    }
}
