namespace DAIgame.Core;

using Godot;

/// <summary>
/// Reusable component that handles hit flash visual feedback for any entity.
/// Add as a child node and call TriggerFlash() when the entity takes damage.
/// </summary>
/// <remarks>
/// This component manages the flash effect on one or more CanvasItem nodes (sprites).
/// It handles the timer-based restoration of the original modulate color automatically.
/// </remarks>
public partial class HitFlashController : Node
{
    /// <summary>
    /// Duration of the hit flash effect in seconds.
    /// </summary>
    [Export]
    public float FlashDuration { get; set; } = 0.1f;

    /// <summary>
    /// Color to apply during the flash.
    /// Default is a red tint for damage.
    /// </summary>
    [Export]
    public Color FlashColor { get; set; } = new Color(2f, 0.5f, 0.5f, 1f);

    /// <summary>
    /// Color to restore after flash ends.
    /// </summary>
    [Export]
    public Color NormalColor { get; set; } = Colors.White;

    private float _flashTimer;
    private readonly System.Collections.Generic.List<CanvasItem> _targets = [];

    /// <summary>
    /// Returns true if currently flashing.
    /// </summary>
    public bool IsFlashing => _flashTimer > 0f;

    public override void _Process(double delta)
    {
        if (_flashTimer <= 0f)
        {
            return;
        }

        _flashTimer -= (float)delta;

        if (_flashTimer <= 0f)
        {
            RestoreNormalColor();
        }
    }

    /// <summary>
    /// Registers a CanvasItem (Sprite2D, AnimatedSprite2D, etc.) to receive flash effects.
    /// Call this in _Ready() for each sprite that should flash.
    /// </summary>
    /// <param name="target">The CanvasItem to flash.</param>
    public void RegisterTarget(CanvasItem target)
    {
        if (target is not null && !_targets.Contains(target))
        {
            _targets.Add(target);
        }
    }

    /// <summary>
    /// Unregisters a CanvasItem from receiving flash effects.
    /// </summary>
    /// <param name="target">The CanvasItem to remove.</param>
    public void UnregisterTarget(CanvasItem target)
    {
        _targets.Remove(target);
    }

    /// <summary>
    /// Clears all registered targets.
    /// </summary>
    public void ClearTargets()
    {
        _targets.Clear();
    }

    /// <summary>
    /// Triggers the hit flash effect on all registered targets.
    /// Can be called multiple times - each call resets the timer.
    /// </summary>
    public void TriggerFlash()
    {
        _flashTimer = FlashDuration;
        ApplyFlashColor();
    }

    /// <summary>
    /// Triggers flash with custom duration (overrides default).
    /// </summary>
    /// <param name="duration">Duration in seconds for this flash.</param>
    public void TriggerFlash(float duration)
    {
        _flashTimer = duration;
        ApplyFlashColor();
    }

    /// <summary>
    /// Triggers flash with custom color and duration.
    /// </summary>
    /// <param name="color">Color for this flash.</param>
    /// <param name="duration">Duration in seconds for this flash.</param>
    public void TriggerFlash(Color color, float duration)
    {
        _flashTimer = duration;
        foreach (var target in _targets)
        {
            if (IsInstanceValid(target))
            {
                target.Modulate = color;
            }
        }
    }

    private void ApplyFlashColor()
    {
        foreach (var target in _targets)
        {
            if (IsInstanceValid(target))
            {
                target.Modulate = FlashColor;
            }
        }
    }

    private void RestoreNormalColor()
    {
        foreach (var target in _targets)
        {
            if (IsInstanceValid(target))
            {
                target.Modulate = NormalColor;
            }
        }
    }

    /// <summary>
    /// Immediately ends the flash and restores normal color.
    /// </summary>
    public void CancelFlash()
    {
        _flashTimer = 0f;
        RestoreNormalColor();
    }
}
