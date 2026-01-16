namespace DAIgame.UI;

using Godot;

/// <summary>
/// Dynamic FPS-style reticule with four lines that spread based on weapon accuracy.
/// Lines are closer at >= 100 accuracy, spread apart when accuracy is lower.
/// </summary>
public partial class Reticule : Control
{
    /// <summary>
    /// Length of each reticule line in pixels.
    /// </summary>
    [Export]
    public float LineLength { get; set; } = 8f;

    /// <summary>
    /// Thickness of each reticule line in pixels.
    /// </summary>
    [Export]
    public float LineThickness { get; set; } = 2f;

    /// <summary>
    /// Gap from center at 100% accuracy (minimum spread).
    /// </summary>
    [Export]
    public float MinGap { get; set; } = 4f;

    /// <summary>
    /// Gap from center at 0% accuracy (maximum spread).
    /// </summary>
    [Export]
    public float MaxGap { get; set; } = 32f;

    /// <summary>
    /// Color of the reticule lines.
    /// </summary>
    [Export]
    public Color LineColor { get; set; } = new Color(1, 1, 1, 0.9f);

    /// <summary>
    /// Current accuracy percentage (0-100). Updated by WeaponManager.
    /// </summary>
    private float _currentAccuracy = 100f;

    /// <summary>
    /// Whether the reticule should be visible (only when weapon equipped).
    /// </summary>
    private bool _isVisible;

    public override void _Ready()
    {
        // Disable mouse filter so reticule doesn't block input
        MouseFilter = MouseFilterEnum.Ignore;

        // Start hidden
        ShowReticule(false);
    }

    public override void _Process(double delta)
    {
        if (!_isVisible)
        {
            return;
        }

        // Position at mouse cursor
        Position = GetViewport().GetMousePosition();

        // Queue redraw to update the reticule
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_isVisible)
        {
            return;
        }

        // Calculate gap based on accuracy
        // At 100% accuracy: MinGap
        // At 0% accuracy: MaxGap
        var accuracyFactor = Mathf.Clamp(_currentAccuracy / 100f, 0f, 1f);
        var gap = Mathf.Lerp(MaxGap, MinGap, accuracyFactor);

        // Draw four lines extending from center
        // Top line
        DrawLine(
            new Vector2(0, -(gap + LineLength)),
            new Vector2(0, -gap),
            LineColor,
            LineThickness
        );

        // Bottom line
        DrawLine(
            new Vector2(0, gap),
            new Vector2(0, gap + LineLength),
            LineColor,
            LineThickness
        );

        // Left line
        DrawLine(
            new Vector2(-(gap + LineLength), 0),
            new Vector2(-gap, 0),
            LineColor,
            LineThickness
        );

        // Right line
        DrawLine(
            new Vector2(gap, 0),
            new Vector2(gap + LineLength, 0),
            LineColor,
            LineThickness
        );
    }

    /// <summary>
    /// Updates the current accuracy percentage.
    /// </summary>
    public void SetAccuracy(float accuracyPercent) =>
        _currentAccuracy = Mathf.Clamp(accuracyPercent, 0f, 100f);

    /// <summary>
    /// Shows or hides the reticule.
    /// </summary>
    public void ShowReticule(bool visible)
    {
        _isVisible = visible;
        Visible = visible;

        if (visible)
        {
            QueueRedraw();
        }
    }
}
