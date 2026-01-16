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

        // Create reticule
        var reticuleScene = GD.Load<PackedScene>("res://scenes/UI/Reticule.tscn");
        if (reticuleScene is not null)
        {
            _reticule = reticuleScene.Instantiate<Reticule>();
            // Add to tree with higher z-index to ensure it draws on top
            _reticule.ZIndex = 100;
            GetTree().Root.AddChild(_reticule);
            _reticule.ShowReticule(false);
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
        _reticule?.ShowReticule(shouldShowReticule);

        // When reticule is active, hide the system cursor
        if (shouldShowReticule)
        {
            // Hide the cursor by setting it to an empty texture
            Input.SetCustomMouseCursor(null);
            _currentCursor = null;
            GD.Print("CursorManager: Hiding cursor, showing reticule");
        }
        else
        {
            // Show pointer cursor
            if (_pointerCursor is null)
            {
                GD.PrintErr("CursorManager: No cursor texture available!");
                return;
            }

            // Avoid redundant cursor updates
            if (_currentCursor == _pointerCursor)
            {
                return;
            }

            _currentCursor = _pointerCursor;
            Input.SetCustomMouseCursor(_pointerCursor, Input.CursorShape.Arrow, PointerHotspot);
            GD.Print("CursorManager: Showing pointer cursor");
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
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
