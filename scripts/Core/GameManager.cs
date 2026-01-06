namespace DAIgame.Core;

using System;
using Godot;

/// <summary>
/// Central game manager that handles global state like slow motion, day/night, and temperature.
/// Autoload singleton - add to Project Settings > Autoload.
/// </summary>
public partial class GameManager : Node
{
    /// <summary>
    /// Singleton instance. May be null if not initialized or during testing.
    /// </summary>
    public static GameManager? Instance { get; private set; }

    /// <summary>
    /// Gets the GameManager instance, throwing if not available.
    /// Use this when the GameManager is required for the operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when GameManager is not initialized.</exception>
    public static GameManager GetRequiredInstance()
    {
        return Instance ?? throw new InvalidOperationException(
            "GameManager is not initialized. Ensure the GameManager node is in the scene tree.");
    }

    /// <summary>
    /// Attempts to get the GameManager instance.
    /// Use this for optional operations that can gracefully handle a missing GameManager.
    /// </summary>
    /// <param name="instance">The GameManager instance if available, or null otherwise.</param>
    /// <returns>True if the instance is available, false otherwise.</returns>
    public static bool TryGetInstance(out GameManager? instance)
    {
        instance = Instance;
        return Instance is not null;
    }

    #region Slow Motion

    /// <summary>
    /// Time scale when slow motion is active.
    /// </summary>
    [Export]
    public float SlowMoTimeScale { get; set; } = 0.3f;

    /// <summary>
    /// Whether slow motion is currently active.
    /// </summary>
    public bool IsSlowMoActive { get; private set; }

    #endregion

    #region Day/Night Cycle

    /// <summary>
    /// Duration of a full day cycle in seconds.
    /// </summary>
    [Export]
    public float DayCycleDuration { get; set; } = 120f;

    /// <summary>
    /// Current time of day normalized (0.0 = midnight, 0.5 = noon, 1.0 = midnight again).
    /// </summary>
    public float TimeOfDay { get; private set; } = 0.25f; // Start at 6 AM

    /// <summary>
    /// Whether it is currently night time (for gameplay effects).
    /// Night is roughly 0.75-1.0 and 0.0-0.25 (6 PM to 6 AM).
    /// </summary>
    public bool IsNight => TimeOfDay is < 0.25f or > 0.75f;

    /// <summary>
    /// Signal emitted when day/night state changes.
    /// </summary>
    [Signal]
    public delegate void DayNightChangedEventHandler(bool isNight);

    #endregion

    #region Temperature System

    /// <summary>
    /// Maximum cold exposure before player takes damage.
    /// </summary>
    [Export]
    public float MaxColdExposure { get; set; } = 100f;

    /// <summary>
    /// Rate of cold exposure increase per second when outdoors at night.
    /// </summary>
    [Export]
    public float ColdExposureRate { get; set; } = 5f;

    /// <summary>
    /// Rate of cold exposure decrease per second when indoors or during day.
    /// </summary>
    [Export]
    public float ColdRecoveryRate { get; set; } = 10f;

    /// <summary>
    /// Current cold exposure level (0 = warm, MaxColdExposure = freezing).
    /// </summary>
    public float ColdExposure { get; private set; }

    /// <summary>
    /// Whether the player is currently indoors (reduces cold).
    /// </summary>
    public bool IsPlayerIndoors { get; set; }

    /// <summary>
    /// Signal emitted when cold exposure changes significantly.
    /// </summary>
    [Signal]
    public delegate void ColdExposureChangedEventHandler(float exposure, float maxExposure);

    #endregion

    private bool _wasNight;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always; // Keep processing even when paused
        _wasNight = IsNight;
    }

    public override void _Process(double delta)
    {
        HandleSlowMoInput();
        UpdateDayNightCycle((float)delta);
        UpdateColdExposure((float)delta);
    }

    #region Slow Motion Methods

    private void HandleSlowMoInput()
    {
        if (Input.IsActionJustPressed("ToggleSlowMo"))
        {
            ToggleSlowMo();
        }
    }

    /// <summary>
    /// Toggles slow motion on/off.
    /// </summary>
    public void ToggleSlowMo()
    {
        IsSlowMoActive = !IsSlowMoActive;
        Engine.TimeScale = IsSlowMoActive ? SlowMoTimeScale : 1.0f;
        GD.Print($"Slow-mo: {(IsSlowMoActive ? "ON" : "OFF")} (TimeScale: {Engine.TimeScale})");
    }

    /// <summary>
    /// Sets slow motion to a specific state.
    /// </summary>
    public void SetSlowMo(bool enabled)
    {
        if (IsSlowMoActive == enabled)
        {
            return;
        }

        ToggleSlowMo();
    }

    #endregion

    #region Day/Night Methods

    private void UpdateDayNightCycle(float delta)
    {
        // Advance time (respects Engine.TimeScale automatically via delta)
        TimeOfDay += delta / DayCycleDuration;
        if (TimeOfDay >= 1.0f)
        {
            TimeOfDay -= 1.0f;
        }

        // Check for day/night transition
        var currentlyNight = IsNight;
        if (currentlyNight != _wasNight)
        {
            _wasNight = currentlyNight;
            EmitSignal(SignalName.DayNightChanged, currentlyNight);
            GD.Print($"Day/Night changed: {(currentlyNight ? "NIGHT" : "DAY")}");
        }
    }

    /// <summary>
    /// Sets time of day directly (0.0-1.0).
    /// </summary>
    public void SetTimeOfDay(float time) => TimeOfDay = Mathf.Clamp(time, 0f, 1f);

    /// <summary>
    /// Gets a color tint based on current time of day for lighting.
    /// </summary>
    public Color GetDayNightTint()
    {
        // Simple linear interpolation between day and night colors
        var nightIntensity = GetNightIntensity();
        var dayColor = new Color(1f, 1f, 1f, 1f);
        var nightColor = new Color(0.2f, 0.2f, 0.4f, 1f);
        return dayColor.Lerp(nightColor, nightIntensity);
    }

    /// <summary>
    /// Gets the current night intensity (0 = full day, 1 = full night).
    /// </summary>
    public float GetNightIntensity()
    {
        // Peak night at midnight (0.0), peak day at noon (0.5)
        // Smooth transition using sine wave
        var angle = TimeOfDay * Mathf.Pi * 2f;
        return (Mathf.Cos(angle) + 1f) / 2f;
    }

    #endregion

    #region Temperature Methods

    private void UpdateColdExposure(float delta)
    {
        var previousExposure = ColdExposure;

        if (IsNight && !IsPlayerIndoors)
        {
            // Increase cold exposure at night when outdoors
            ColdExposure = Mathf.Min(ColdExposure + (ColdExposureRate * delta), MaxColdExposure);
        }
        else
        {
            // Decrease cold exposure during day or when indoors
            ColdExposure = Mathf.Max(ColdExposure - (ColdRecoveryRate * delta), 0f);
        }

        // Emit signal if exposure changed significantly (every 5%)
        var previousPercent = Mathf.FloorToInt(previousExposure / MaxColdExposure * 20f);
        var currentPercent = Mathf.FloorToInt(ColdExposure / MaxColdExposure * 20f);
        if (previousPercent != currentPercent)
        {
            EmitSignal(SignalName.ColdExposureChanged, ColdExposure, MaxColdExposure);
        }
    }

    /// <summary>
    /// Gets cold exposure as a normalized value (0.0 - 1.0).
    /// </summary>
    public float GetColdExposureNormalized() => ColdExposure / MaxColdExposure;

    /// <summary>
    /// Resets cold exposure to zero.
    /// </summary>
    public void ResetColdExposure() => ColdExposure = 0f;

    #endregion
}
