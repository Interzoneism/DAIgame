namespace DAIgame.Core;

using Godot;

/// <summary>
/// Manages custom cursors based on game state.
/// Handles cursor switching for inventory, combat, and drag-drop states.
/// Uses Godot's native cursor system with consistent hotspot positioning.
/// </summary>
public partial class CursorManager : Node
{
    // Cursor hotspot positions (in pixels from top-left of cursor image)
    // Pointer cursor: tip of the arrow (typically top-left area, but adjust based on actual sprite)
    private static readonly Vector2 PointerHotspot = new(4, 2);
    // Centered cursor for aim - calculated from texture size
    private Vector2 _aimHotspot;

    private Texture2D? _pointerCursor;
    private Texture2D? _aimCursor;
    private bool _weaponEquipped;
    private bool _hoveringItem;
    private bool _holdingItem;

    // Track current cursor to avoid redundant updates
    private Texture2D? _currentCursor;

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
        _aimCursor = GD.Load<Texture2D>("res://assets/cursor/aim.png");
        // Calculate centered hotspot for aim cursor
        if (_aimCursor is not null)
        {
            _aimHotspot = new Vector2(_aimCursor.GetWidth() / 2f, _aimCursor.GetHeight() / 2f);
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
        Texture2D? cursorTexture;
        Vector2 hotspot;

        // Priority order: weapon equipped > default pointer
        if (_weaponEquipped && !IsInventoryOpen && _aimCursor is not null)
        {
            cursorTexture = _aimCursor;
            hotspot = _aimHotspot;
        }
        else if (_pointerCursor is not null)
        {
            cursorTexture = _pointerCursor;
            hotspot = PointerHotspot;
        }
        else
        {
            GD.PrintErr("CursorManager: No cursor texture available!");
            return;
        }

        // Avoid redundant cursor updates
        if (_currentCursor == cursorTexture)
        {
            return;
        }

        _currentCursor = cursorTexture;
        Input.SetCustomMouseCursor(cursorTexture, Input.CursorShape.Arrow, hotspot);
        GD.Print($"CursorManager: Switching to cursor with hotspot {hotspot}");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
