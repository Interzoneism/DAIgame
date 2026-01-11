namespace DAIgame.Combat;

using Godot;

/// <summary>
/// Zombie corpse that can move and collide with walls, then optimizes to static sprite when settled.
/// </summary>
public partial class ZombieCorpse : CharacterBody2D
{
	private const float Friction = 600f; // pixels/sec^2
	private const float SettleThreshold = 5f; // velocity below this converts to static
	private AnimatedSprite2D? _sprite;
	private bool _hasSettled;

	/// <summary>
	/// Sets the initial velocity for the corpse.
	/// </summary>
	public new void SetVelocity(Vector2 velocity) => Velocity = velocity;

	public override void _Ready()
	{
		base._Ready();
		ZIndex = 3; // keeps the corpse below active characters

		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (_sprite is not null)
		{
			_sprite.Centered = true;
			_sprite.FlipH = true;

			// Ensure we're using the corpse animation then pick a random frame and freeze it.
            _sprite.Animation = "corpse";
            if (_sprite.SpriteFrames is not null && _sprite.SpriteFrames.HasAnimation("corpse"))
            {
                var frameCount = _sprite.SpriteFrames.GetFrameCount("corpse");
                if (frameCount > 0)
                {
                    _sprite.Frame = (int)GD.Randi() % frameCount;
                    _sprite.Pause();
                }

                // Set CapsuleShape2D to match sprite frame size
                var frameTexture = _sprite.SpriteFrames.GetFrameTexture("corpse", 0);
                if (frameTexture is not null)
                {
                    var size = frameTexture.GetSize();
                    var collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                    if (collisionShape?.Shape is RectangleShape2D capsule)
                    {
                        capsule.Size = new Vector2(size.X * 0.9f, size.Y * 0.4f);
                    }
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hasSettled)
        {
            return;
        }

        if (Velocity.LengthSquared() > SettleThreshold)
        {
            // Apply friction
            Velocity = Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
            // Move and slide to handle wall collisions
            MoveAndSlide();
        }
        else
        {
            // Velocity is near zero, convert to static sprite
            ConvertToStatic();
        }
    }

    private void ConvertToStatic()
    {
        _hasSettled = true;

        if (_sprite is null)
        {
            QueueFree();
            return;
        }

        // Create a static Node2D to replace this physics body
        var staticCorpse = new Node2D
        {
            GlobalPosition = GlobalPosition,
            GlobalRotation = GlobalRotation,
            ZIndex = ZIndex
        };

        // Clone the sprite and add it to the static node
        var clonedSprite = new AnimatedSprite2D
        {
            SpriteFrames = _sprite.SpriteFrames,
            Animation = _sprite.Animation,
            Frame = _sprite.Frame,
            Centered = _sprite.Centered,
            FlipH = _sprite.FlipH,
            Position = _sprite.Position
        };
        clonedSprite.Pause();

        staticCorpse.AddChild(clonedSprite);

        // Add to scene tree at same parent
        var parent = GetParent();
        parent?.AddChild(staticCorpse);

        // Remove this physics body
        QueueFree();
    }
}
