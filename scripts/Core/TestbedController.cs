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
        CreateInstructionsLabel();
        GD.Print("=== TESTBED CONTROLS ===");
        GD.Print("WASD - Move");
        GD.Print("Mouse - Aim");
        GD.Print("Left Click - Shoot");
        GD.Print("H - Use healing item");
        GD.Print("Q - Toggle slow-motion");
        GD.Print("N - Toggle night");
        GD.Print("Z - Spawn zombie at mouse");
        GD.Print("========================");
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

    private void CreateInstructionsLabel()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        _instructionsLabel = new Label
        {
            Text = "Controls: WASD=Move | Mouse=Aim | LMB=Shoot | H=Heal | Q=SlowMo | N=Night | Z=Spawn Zombie",
            AnchorLeft = 0.5f,
            AnchorTop = 1f,
            AnchorRight = 0.5f,
            AnchorBottom = 1f,
            OffsetLeft = -300f,
            OffsetTop = -30f,
            OffsetRight = 300f,
            OffsetBottom = -10f,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0.7f)
        };
        canvas.AddChild(_instructionsLabel);
    }
}
