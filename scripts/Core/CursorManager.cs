namespace DAIgame.Core;

using DAIgame.UI;
using Godot;

/// <summary>
/// Manages custom cursors and reticule based on game state.
/// Handles cursor switching for inventory and shows dynamic reticule for combat.
/// </summary>
public partial class CursorManager : Node
{
    // Cursor hotspot positions (in pixels from top-left of cursor image)
    // Pointer cursor: tip of the arrow (typically top-left area, but adjust based on actual sprite)
    private static readonly Vector2 PointerHotspot = new(4, 2);

    private Texture2D? _pointerCursor;
    private bool _weaponEquipped;
    private bool _hoveringItem;
    private bool _holdingItem;

    // Track current cursor to avoid redundant updates
    private Texture2D? _currentCursor;

    // Dynamic reticule for weapon aiming
    private Reticule? _reticule;
    private CanvasLayer? _reticuleLayer;

    /// <summary>
    /// Whether the inventory screen is currently open.
    /// </summary>
    public bool IsInventoryOpen { get; private set; }

    public static CursorManager? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        // Load cursor textures
        _pointerCursor = GD.Load<Texture2D>("res://assets/cursor/pointer.png");

        // Create reticule and its dedicated canvas layer to ensure it's on top
        var reticuleScene = GD.Load<PackedScene>("res://scenes/UI/Reticule.tscn");
        if (reticuleScene is not null)
        {
            _reticuleLayer = new CanvasLayer();
            _reticuleLayer.Layer = 100; // Above most HUD elements
            GetTree().Root.AddChild(_reticuleLayer);

            _reticule = reticuleScene.Instantiate<Reticule>();
            _reticuleLayer.AddChild(_reticule);
            _reticule.ShowReticule(false);
            GD.Print("CursorManager: Reticule instantiated and added to CanvasLayer 100");
        }
        else
        {
            GD.PrintErr("CursorManager: Failed to load Reticule scene!");
        }

        UpdateCursor();
    }

    /// <summary>
    /// Sets whether the inventory is currently open.
    /// </summary>
    public void SetInventoryOpen(bool open)
    {
        IsInventoryOpen = open;
        UpdateCursor();
    }

    /// <summary>
    /// Sets whether a weapon is currently equipped.
    /// </summary>
    public void SetWeaponEquipped(bool equipped)
    {
        _weaponEquipped = equipped;
        UpdateCursor();
    }

    /// <summary>
    /// Sets whether the cursor is hovering over an inventory item.
    /// </summary>
    public void SetHoveringItem(bool hovering)
    {
        _hoveringItem = hovering;
        UpdateCursor();
    }

    /// <summary>
    /// Sets whether an item is being held/dragged.
    /// </summary>
    public void SetHoldingItem(bool holding)
    {
        _holdingItem = holding;

        UpdateCursor();
    }

    private void UpdateCursor()
    {
        // Show reticule when weapon is equipped and not in inventory
        var shouldShowReticule = _weaponEquipped && !IsInventoryOpen;

        if (_reticule != null)
        {
            _reticule.ShowReticule(shouldShowReticule);
        }
        else
        {
            GD.PrintErr("CursorManager: _reticule is NULL in UpdateCursor!");
        }

        // When reticule is active, hide the system cursor
        if (shouldShowReticule)
        {
            // Reset custom cursor
            Input.SetCustomMouseCursor(null);

            // Hide the system mouse cursor completely
            Input.MouseMode = Input.MouseModeEnum.Hidden;
            _currentCursor = null;
        }
        else
        {
            // Ensure system cursor is visible
            Input.MouseMode = Input.MouseModeEnum.Visible;

            // Show pointer cursor
            if (_pointerCursor is null)
            {
                GD.PrintErr("CursorManager: No pointer cursor texture loaded!");
                return;
            }

            // Avoid redundant cursor updates if already using this texture
            if (_currentCursor == _pointerCursor)
            {
                return;
            }

            _currentCursor = _pointerCursor;
            Input.SetCustomMouseCursor(_pointerCursor, Input.CursorShape.Arrow, PointerHotspot);
        }
    }

    /// <summary>
    /// Updates the reticule's accuracy value (0-100).
    /// Called by WeaponManager to reflect current weapon accuracy.
    /// </summary>
    public void SetReticuleAccuracy(float accuracyPercent)
    {
        _reticule?.SetAccuracy(accuracyPercent);
    }

    public override void _ExitTree()
    {
        if (_reticuleLayer is not null)
        {
            _reticuleLayer.QueueFree();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
