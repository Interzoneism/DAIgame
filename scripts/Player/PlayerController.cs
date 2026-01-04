using Godot;

namespace DAIgame.Player;

/// <summary>
/// Hotline Miami-style player controller with snappy WASD movement and mouse aim.
/// Attach to a CharacterBody2D node.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
    /// <summary>
    /// Movement speed in pixels per second.
    /// </summary>
    [Export]
    public float MoveSpeed { get; set; } = 200f;

    private Node2D? _aimNode;

    public override void _Ready()
    {
        // Add to player group for easy lookup
        AddToGroup("player");

        // Cache the Aim node for rotation (if it exists)
        _aimNode = GetNodeOrNull<Node2D>("Aim");
    }

    public override void _Process(double delta)
    {
        RotateTowardsMouse();
    }

    public override void _PhysicsProcess(double delta)
    {
        HandleMovement();
    }

    /// <summary>
    /// Handles WASD input for immediate, snappy movement.
    /// No acceleration - instant velocity change based on input.
    /// </summary>
    private void HandleMovement()
    {
        // Get raw input direction (no smoothing)
        var inputDir = Vector2.Zero;

        if (Input.IsActionPressed("MoveUp"))
        {
            inputDir.Y -= 1;
        }

        if (Input.IsActionPressed("MoveDown"))
        {
            inputDir.Y += 1;
        }

        if (Input.IsActionPressed("MoveLeft"))
        {
            inputDir.X -= 1;
        }

        if (Input.IsActionPressed("MoveRight"))
        {
            inputDir.X += 1;
        }

        // Normalize to prevent faster diagonal movement
        if (inputDir.LengthSquared() > 0)
        {
            inputDir = inputDir.Normalized();
        }

        // Apply velocity directly - snappy, no acceleration
        // MoveAndSlide uses delta internally and respects Engine.TimeScale
        Velocity = inputDir * MoveSpeed;
        MoveAndSlide();
    }

    /// <summary>
    /// Rotates the player (or aim node) to face the mouse cursor.
    /// Updates every frame for responsive aiming.
    /// </summary>
    private void RotateTowardsMouse()
    {
        var mousePos = GetGlobalMousePosition();
        var direction = mousePos - GlobalPosition;
        var targetAngle = direction.Angle();

        // If we have a dedicated Aim node, rotate that instead of the whole body
        if (_aimNode != null)
        {
            _aimNode.Rotation = targetAngle;
        }
        else
        {
            // Fallback: rotate the entire player
            Rotation = targetAngle;
        }
    }
}

