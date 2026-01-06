namespace DAIgame.Core;

using Godot;

/// <summary>
/// Global game manager singleton for managing game-wide state.
/// Handles slow motion and other global effects.
/// Access via GameManager.Instance after it's added to the scene tree.
/// </summary>
public partial class GameManager : Node
{
  /// <summary>
  /// Singleton instance. Set when the GameManager enters the tree.
  /// </summary>
  public static GameManager? Instance { get; private set; }

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
