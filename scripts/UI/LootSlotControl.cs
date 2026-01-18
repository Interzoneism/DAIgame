namespace DAIgame.UI;

using DAIgame.Core;
using DAIgame.Core.Items;
using DAIgame.Loot;
using Godot;

/// <summary>
/// A slot control for loot containers. Similar to InventorySlotControl but works with ILootable.
/// </summary>
public partial class LootSlotControl : PanelContainer
{
    private TextureRect? _icon;
    private Label? _stackLabel;
    private ColorRect? _categoryBackground;
    private StyleBoxFlat? _style;

    private static readonly Color WeaponColor = new(0.6f, 0.2f, 0.2f, 0.7f);
    private static readonly Color UsableColor = new(0.2f, 0.5f, 0.2f, 0.7f);
    private static readonly Color AmmoColor = new(0.5f, 0.5f, 0.2f, 0.7f);
    private static readonly Color WearableColor = new(0.2f, 0.3f, 0.6f, 0.7f);
    private static readonly Color DefaultColor = new(0.3f, 0.3f, 0.3f, 0.5f);

    public ILootable? Lootable { get; private set; }
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

        if (Lootable?.GetItemAt(SlotIndex) is not null)
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
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton)
        {
            return;
        }

        if (Lootable is null)
        {
            return;
        }

        var inventoryScreen = InventoryScreen.Instance;
        if (inventoryScreen is null)
        {
            return;
        }

        // Right-click to split stack
        if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
        {
            if (inventoryScreen.TrySplitStackFromLootable(Lootable, SlotIndex))
            {
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        // Left-click release to pick up or place
        if (mouseButton.ButtonIndex != MouseButton.Left || mouseButton.Pressed)
        {
            return;
        }

        // If holding an item, try to place it in the loot container
        if (inventoryScreen.HasHeldItem)
        {
            inventoryScreen.TryPlaceHeldItemInLootable(Lootable, SlotIndex);
            GetViewport().SetInputAsHandled();
            return;
        }

        // Otherwise pick up item from loot container
        var item = Lootable.GetItemAt(SlotIndex);
        if (item is null)
        {
            return;
        }

        inventoryScreen.TryBeginHoldFromLootable(Lootable, SlotIndex);
        GetViewport().SetInputAsHandled();
    }

    public void Configure(ILootable lootable, int slotIndex)
    {
        Lootable = lootable;
        SlotIndex = slotIndex;
    }

    public void Refresh()
    {
        if (_icon is null)
        {
            return;
        }

        if (Lootable is null)
        {
            _icon.Texture = null;
            TooltipText = "";
            if (_stackLabel is not null)
            {
                _stackLabel.Text = "";
            }
            UpdateCategoryBackground(null);
            return;
        }

        var item = Lootable.GetItemAt(SlotIndex);
        _icon.Texture = item?.Icon;
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
}
