namespace DAIgame.Player;

using DAIgame.Core;
using Godot;

/// <summary>
/// Camera that follows the player and offsets slightly towards the mouse cursor.
/// Supports zoom via scroll wheel.
/// </summary>
public partial class PlayerCamera : Camera2D
{
    /// <summary>
    /// How much the camera offsets towards the mouse cursor (0 = none, 1 = full distance).
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float LookAheadFactor { get; set; } = 0.15f;

    /// <summary>
    /// Maximum distance the camera can offset towards the cursor in pixels.
    /// </summary>
    [Export]
    public float MaxLookAheadDistance { get; set; } = 60f;

    /// <summary>
    /// How smoothly the camera follows. Lower = snappier, higher = smoother.
    /// </summary>
    [Export]
    public float SmoothSpeed { get; set; } = 8f;

    /// <summary>
    /// Minimum zoom level (most zoomed in).
    /// </summary>
    [Export]
    public float MinZoom { get; set; } = 0.5f;

    /// <summary>
    /// Maximum zoom level (most zoomed out).
    /// </summary>
    [Export]
    public float MaxZoom { get; set; } = 3f;

    /// <summary>
    /// How much zoom changes per scroll wheel step.
    /// </summary>
    [Export]
    public float ZoomStep { get; set; } = 0.1f;

    /// <summary>
    /// How smoothly zoom transitions happen.
    /// </summary>
    [Export]
    public float ZoomSmoothSpeed { get; set; } = 10f;

    private float _targetZoom = 1f;
    private Vector2 _targetOffset = Vector2.Zero;

    public override void _Ready()
    {
        _targetZoom = Zoom.X;
        MakeCurrent();
    }

    public override void _Process(double delta)
    {
        UpdateLookAhead((float)delta);
        UpdateZoom((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    // Zoom in (increase zoom value = smaller view = zoomed in)
                    _targetZoom = Mathf.Clamp(_targetZoom + ZoomStep, MinZoom, MaxZoom);
                    GetViewport().SetInputAsHandled();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    // Zoom out (decrease zoom value = larger view = zoomed out)
                    _targetZoom = Mathf.Clamp(_targetZoom - ZoomStep, MinZoom, MaxZoom);
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    private void UpdateLookAhead(float delta)
    {
        // Disable look-ahead (mouse following) when inventory is open
        if (CursorManager.Instance?.IsInventoryOpen == true)
        {
            // Smoothly return to center when inventory is open
            _targetOffset = _targetOffset.Lerp(Vector2.Zero, SmoothSpeed * delta);
            Offset = _targetOffset;
            return;
        }

        // Get mouse position in world coordinates
        var mouseWorldPos = GetGlobalMousePosition();
        var playerPos = GetParent<Node2D>().GlobalPosition;

        // Calculate offset towards mouse
        var toMouse = mouseWorldPos - playerPos;
        var desiredOffset = toMouse * LookAheadFactor;

        // Clamp to max distance
        if (desiredOffset.Length() > MaxLookAheadDistance)
        {
            desiredOffset = desiredOffset.Normalized() * MaxLookAheadDistance;
        }

        // Smooth the offset
        _targetOffset = _targetOffset.Lerp(desiredOffset, SmoothSpeed * delta);

        // Apply look-ahead offset
        Offset = _targetOffset;
    }

    private void UpdateZoom(float delta)
    {
        var currentZoom = Zoom.X;
        var newZoom = Mathf.Lerp(currentZoom, _targetZoom, ZoomSmoothSpeed * delta);
        Zoom = new Vector2(newZoom, newZoom);
    }

    /// <summary>
    /// Set the zoom level directly (useful for testing or resetting).
    /// </summary>
    public void SetZoom(float zoomLevel)
    {
        _targetZoom = Mathf.Clamp(zoomLevel, MinZoom, MaxZoom);
        Zoom = new Vector2(_targetZoom, _targetZoom);
    }

    /// <summary>
    /// Get the current target zoom level.
    /// </summary>
    public float GetTargetZoom() => _targetZoom;
}
