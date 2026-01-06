namespace DAIgame.World;

using DAIgame.Core;
using Godot;

/// <summary>
/// Controls the visual day/night cycle using a CanvasModulate node.
/// Attach this script to a CanvasModulate node in the scene.
/// </summary>
public partial class DayNightController : CanvasModulate
{
    /// <summary>
    /// Whether to allow manual night toggle for testing.
    /// </summary>
    [Export]
    public bool AllowManualToggle { get; set; } = true;

    /// <summary>
    /// Day time color tint.
    /// </summary>
    [Export]
    public Color DayColor { get; set; } = new(1f, 1f, 1f, 1f);

    /// <summary>
    /// Night time color tint.
    /// </summary>
    [Export]
    public Color NightColor { get; set; } = new(0.15f, 0.15f, 0.35f, 1f);

    private bool _manualNightOverride;
    private bool _useManualOverride;

    public override void _Ready()
    {
        // Subscribe to GameManager day/night changes if available
        var gm = GameManager.Instance;
        if (gm is not null)
        {
            gm.DayNightChanged += OnDayNightChanged;
        }

        UpdateTint();
    }

    public override void _Process(double delta)
    {
        HandleManualToggle();
        UpdateTint();
    }

    private void HandleManualToggle()
    {
        if (!AllowManualToggle)
        {
            return;
        }

        if (Input.IsActionJustPressed("ToggleNight"))
        {
            _useManualOverride = true;
            _manualNightOverride = !_manualNightOverride;
            GD.Print($"Manual night toggle: {(_manualNightOverride ? "NIGHT" : "DAY")}");
        }
    }

    private void UpdateTint()
    {
        float nightIntensity;

        if (_useManualOverride)
        {
            nightIntensity = _manualNightOverride ? 1f : 0f;
        }
        else
        {
            var gm = GameManager.Instance;
            nightIntensity = gm?.GetNightIntensity() ?? 0f;
        }

        Color = DayColor.Lerp(NightColor, nightIntensity);
    }

    private void OnDayNightChanged(bool isNight)
    {
        // Reset manual override when automatic day/night changes
        if (!_useManualOverride)
        {
            return;
        }

        // Keep manual override active until explicitly toggled again
    }

    public override void _ExitTree()
    {
        var gm = GameManager.Instance;
        if (gm is not null)
        {
            gm.DayNightChanged -= OnDayNightChanged;
        }
    }
}
