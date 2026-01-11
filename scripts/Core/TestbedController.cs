namespace DAIgame.Core;

using Godot;

/// <summary>
/// Testbed controller that sets up the scene with all required systems.
/// Provides hotkey instructions and handles test spawning.
/// </summary>
public partial class TestbedController : Node2D
{
    /// <summary>
    /// Zombie scene to spawn for testing.
    /// </summary>
    [Export]
    public PackedScene? ZombieScene { get; set; }

    private Label? _instructionsLabel;

    public override void _Ready()
    {

    }

    public override void _Process(double delta) => HandleTestInputs();

    private void HandleTestInputs()
    {
        // Spawn zombie at mouse position
        if (Input.IsKeyPressed(Key.Z) && !Input.IsActionJustPressed("ui_accept"))
        {
            if (Input.IsPhysicalKeyPressed(Key.Z) && !_zKeyWasPressed)
            {
                SpawnZombieAtMouse();
            }
        }
        _zKeyWasPressed = Input.IsPhysicalKeyPressed(Key.Z);
    }

    private bool _zKeyWasPressed;

    private void SpawnZombieAtMouse()
    {
        if (ZombieScene is null)
        {
            GD.PrintErr("ZombieScene not assigned to TestbedController!");
            return;
        }

        var zombie = ZombieScene.Instantiate<Node2D>();
        zombie.GlobalPosition = GetGlobalMousePosition();
        AddChild(zombie);
        GD.Print($"Spawned zombie at {zombie.GlobalPosition}");
    }
}
