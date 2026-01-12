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
    // Centered cursors for aim/hover - will be calculated from texture size
    private Vector2 _aimHotspot;
    private Vector2 _hoverHotspot;

    private Texture2D? _pointerCursor;
    private Texture2D? _aimCursor;
    private Texture2D? _hoverCursor;
    private Texture2D? _holdingCursor;
    private ImageTexture? _transparentCursor;

    private bool _inventoryOpen;
    private bool _weaponEquipped;
    private bool _hoveringItem;
    private bool _holdingItem;

    // Track current cursor to avoid redundant updates
    private Texture2D? _currentCursor;

    public static CursorManager? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        // Load cursor textures
        _pointerCursor = GD.Load<Texture2D>("res://assets/cursor/pointer.png");
        _aimCursor = GD.Load<Texture2D>("res://assets/cursor/aim.png");
        _hoverCursor = GD.Load<Texture2D>("res://assets/cursor/hover_item.png");
        _holdingCursor = GD.Load<Texture2D>("res://assets/cursor/holding_item.png");

        // Calculate centered hotspots for aim and hover cursors
        if (_aimCursor is not null)
        {
            _aimHotspot = new Vector2(_aimCursor.GetWidth() / 2f, _aimCursor.GetHeight() / 2f);
        }

        if (_hoverCursor is not null)
        {
            _hoverHotspot = new Vector2(_hoverCursor.GetWidth() / 2f, _hoverCursor.GetHeight() / 2f);
        }

        // Create transparent cursor for drag operations
        var transparentImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        transparentImage.Fill(new Color(0, 0, 0, 0));
        _transparentCursor = ImageTexture.CreateFromImage(transparentImage);

        UpdateCursor();
    }

    /// <summary>
    /// Sets whether the inventory is currently open.
    /// </summary>
    public void SetInventoryOpen(bool open)
    {
        _inventoryOpen = open;
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
        var wasHolding = _holdingItem;
        _holdingItem = holding;

        // If we were holding and now we're not, we need to restore the cursor
        if (wasHolding && !holding)
        {
            // Clear custom cursors on all shapes first to reset state
            for (var i = Input.CursorShape.Arrow; i <= Input.CursorShape.Help; i++)
            {
                Input.SetCustomMouseCursor(null, i);
            }
            _currentCursor = null; // Force re-application of cursor
        }

        UpdateCursor();
    }

    private void UpdateCursor()
    {
        Texture2D? cursorTexture;
        Vector2 hotspot;

        // Priority order: holding > hovering > inventory open > weapon equipped > default
        if (_holdingItem && _transparentCursor is not null)
        {
            // Use transparent cursor during drag - the drag preview shows the item+cursor
            // Set it on all cursor shapes to ensure no OS cursor shows
            for (var i = Input.CursorShape.Arrow; i <= Input.CursorShape.Help; i++)
            {
                Input.SetCustomMouseCursor(_transparentCursor, i, Vector2.Zero);
            }
            _currentCursor = _transparentCursor;
            GD.Print("CursorManager: Switching to transparent cursor (dragging)");
            return;
        }
        else if (_hoveringItem && _hoverCursor is not null)
        {
            cursorTexture = _hoverCursor;
            hotspot = _hoverHotspot;
        }
        else if (_inventoryOpen && _pointerCursor is not null)
        {
            cursorTexture = _pointerCursor;
            hotspot = PointerHotspot;
        }
        else if (_weaponEquipped && _aimCursor is not null)
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
