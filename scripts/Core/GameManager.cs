namespace DAIgame.Core;

using System;
using Godot;

/// <summary>
/// Global game manager singleton for managing game-wide state.
/// Handles slow motion and other global effects.
/// Access via GameManager.Instance after it's added to the scene tree.
/// Note: Instance is null until a GameManager node is added to the scene tree.
/// Use TryGetInstance() for safe access or GetRequiredInstance() when you expect it to exist.
/// </summary>
public partial class GameManager : Node
{
  /// <summary>
  /// Singleton instance. Set when the GameManager enters the tree.
  /// May be null if no GameManager has been added to the scene.
  /// </summary>
  public static GameManager? Instance { get; private set; }

  /// <summary>
  /// Gets the GameManager instance, throwing if not initialized.
  /// Use this when the GameManager is required and should always exist.
  /// </summary>
  /// <exception cref="InvalidOperationException">Thrown when GameManager is not initialized.</exception>
  public static GameManager GetRequiredInstance()
  {
    return Instance ?? throw new InvalidOperationException(
      "GameManager has not been initialized. Ensure a GameManager node is in the scene tree.");
  }

  /// <summary>
  /// Tries to get the GameManager instance safely.
  /// </summary>
  /// <param name="instance">The GameManager instance if available.</param>
  /// <returns>True if the instance exists, false otherwise.</returns>
  public static bool TryGetInstance(out GameManager? instance)
  {
    instance = Instance;
    return instance is not null;
  }

  /// <summary>
  /// Time scale used during slow motion.
  /// </summary>
  [Export]
  public float SlowMoTimeScale { get; set; } = 0.3f;

  /// <summary>
  /// Whether slow motion is currently active.
  /// </summary>
  public bool IsSlowMoActive { get; private set; }

  public override void _EnterTree()
  {
    Instance = this;
  }

  public override void _ExitTree()
  {
    if (Instance == this)
    {
      Instance = null;
    }
  }

  public override void _Process(double delta)
  {
    HandleSlowMoInput();
  }

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
    GD.Print($"Slow motion: {(IsSlowMoActive ? "ON" : "OFF")} (TimeScale: {Engine.TimeScale})");
  }

  /// <summary>
  /// Sets slow motion to a specific state.
  /// </summary>
  /// <param name="active">Whether slow motion should be active.</param>
  public void SetSlowMo(bool active)
  {
    if (IsSlowMoActive == active)
    {
      return;
    }

    ToggleSlowMo();
  }
}
