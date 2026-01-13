namespace DAIgame.UI;

using System.Collections.Generic;
using DAIgame.Loot;
using Godot;

/// <summary>
/// A standalone, draggable window for displaying a lootable container's contents.
/// </summary>
public partial class LootWindow : PanelContainer
{
    private const int SlotSize = 64;
    private const int TitleBarHeight = 28;
    private const int DefaultColumns = 2;

    private ILootable? _lootable;
    private readonly List<LootSlotControl> _slots = [];
    private GridContainer? _grid;
    private Label? _titleLabel;
    private Panel? _titleBar;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Font? _mainFont;
    private int _columns = DefaultColumns;

    /// <summary>
    /// The lootable this window is displaying.
    /// </summary>
    public ILootable? Lootable => _lootable;

    /// <summary>
    /// Event fired when the window is closed.
    /// </summary>
    [Signal]
    public delegate void WindowClosedEventHandler(LootWindow window);

    public override void _Ready()
    {
        _mainFont = GD.Load<Font>("res://assets/fonts/VCR_OSD_MONO.ttf");

        MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.05f, 0.95f),
            BorderColor = new Color(0.5f, 0.5f, 0.4f, 1f),
            CornerDetail = 1
        };
        style.SetBorderWidthAll(2);
        AddThemeStyleboxOverride("panel", style);

        var outerContainer = new VBoxContainer();
        outerContainer.AddThemeConstantOverride("separation", 0);
        AddChild(outerContainer);

        // Title bar for dragging
        _titleBar = new Panel
        {
            CustomMinimumSize = new Vector2(0, TitleBarHeight),
            MouseFilter = MouseFilterEnum.Stop
        };
        var titleBarStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.18f, 0.12f, 1f)
        };
        _titleBar.AddThemeStyleboxOverride("panel", titleBarStyle);
        outerContainer.AddChild(_titleBar);

        var titleHBox = new HBoxContainer();
        titleHBox.SetAnchorsPreset(LayoutPreset.FullRect);
        titleHBox.AddThemeConstantOverride("separation", 8);
        _titleBar.AddChild(titleHBox);

        _titleLabel = new Label
        {
            Text = "CONTAINER",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        if (_mainFont is not null)
        {
            _titleLabel.AddThemeFontOverride("font", _mainFont);
            _titleLabel.AddThemeFontSizeOverride("font_size", 16);
        }
        titleHBox.AddChild(_titleLabel);

        // Close button
        var closeButton = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(TitleBarHeight - 4, TitleBarHeight - 4),
            FocusMode = FocusModeEnum.None
        };
        closeButton.Pressed += OnClosePressed;
        titleHBox.AddChild(closeButton);

        _titleBar.GuiInput += OnTitleBarInput;

        // Content margin
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        outerContainer.AddChild(margin);

        _grid = new GridContainer { Columns = _columns };
        _grid.AddThemeConstantOverride("h_separation", 4);
        _grid.AddThemeConstantOverride("v_separation", 4);
        margin.AddChild(_grid);
    }

    /// <summary>
    /// Configures this window to display the given lootable.
    /// </summary>
    public void Configure(ILootable lootable, int columns = DefaultColumns)
    {
        _lootable = lootable;
        _columns = columns;

        if (_titleLabel is not null)
        {
            _titleLabel.Text = lootable.LootDisplayName.ToUpperInvariant();
        }

        if (_grid is not null)
        {
            _grid.Columns = columns;
        }

        RebuildSlots();
    }

    /// <summary>
    /// Configures this window for ground items (multiple lootables, one slot each).
    /// </summary>
    public void ConfigureForGroundItems(List<ILootable> groundItems, int columns = DefaultColumns)
    {
        _lootable = null;
        _columns = columns;

        if (_titleLabel is not null)
        {
            _titleLabel.Text = "ON GROUND";
        }

        if (_grid is not null)
        {
            _grid.Columns = columns;
        }

        RebuildSlotsForGroundItems(groundItems);
    }

    private void RebuildSlots()
    {
        ClearSlots();

        if (_grid is null || _lootable is null)
        {
            return;
        }

        for (var i = 0; i < _lootable.SlotCount; i++)
        {
            var slot = new LootSlotControl
            {
                CustomMinimumSize = new Vector2(SlotSize, SlotSize)
            };
            slot.Configure(_lootable, i);
            _grid.AddChild(slot);
            _slots.Add(slot);
        }
    }

    private void RebuildSlotsForGroundItems(List<ILootable> groundItems)
    {
        ClearSlots();

        if (_grid is null)
        {
            return;
        }

        foreach (var groundItem in groundItems)
        {
            var slot = new LootSlotControl
            {
                CustomMinimumSize = new Vector2(SlotSize, SlotSize)
            };
            slot.Configure(groundItem, 0);
            _grid.AddChild(slot);
            _slots.Add(slot);
        }
    }

    private void ClearSlots()
    {
        foreach (var slot in _slots)
        {
            slot.QueueFree();
        }
        _slots.Clear();
    }

    /// <summary>
    /// Refreshes all slot displays.
    /// </summary>
    public void RefreshSlots()
    {
        foreach (var slot in _slots)
        {
            slot.Refresh();
        }
    }

    /// <summary>
    /// Gets all slots in this window.
    /// </summary>
    public IReadOnlyList<LootSlotControl> GetSlots() => _slots;

    /// <summary>
    /// Adds a ground item slot dynamically.
    /// </summary>
    public void AddGroundItemSlot(ILootable groundItem)
    {
        if (_grid is null)
        {
            return;
        }

        var slot = new LootSlotControl
        {
            CustomMinimumSize = new Vector2(SlotSize, SlotSize)
        };
        slot.Configure(groundItem, 0);
        _grid.AddChild(slot);
        _slots.Add(slot);
    }

    /// <summary>
    /// Removes a slot for the given lootable.
    /// </summary>
    public bool RemoveSlotForLootable(ILootable lootable)
    {
        for (var i = _slots.Count - 1; i >= 0; i--)
        {
            if (_slots[i].Lootable == lootable)
            {
                _slots[i].QueueFree();
                _slots.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    private void OnTitleBarInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    _isDragging = true;
                    _dragOffset = GlobalPosition - mouseButton.GlobalPosition;
                    // Bring to front
                    var parent = GetParent();
                    if (parent is not null)
                    {
                        parent.MoveChild(this, -1);
                    }
                }
                else
                {
                    _isDragging = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            GlobalPosition = mouseMotion.GlobalPosition + _dragOffset;
        }
    }

    private void OnClosePressed()
    {
        EmitSignal(SignalName.WindowClosed, this);
        QueueFree();
    }

    public override void _ExitTree()
    {
        ClearSlots();
    }
}
