namespace DAIgame.Combat;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Particle-based blood spatter system that spawns blood particles which settle into permanent sprites.
/// </summary>
public partial class BloodSpatter : Node2D
{
    [Export]
    public Vector2 SprayDirection { get; set; } = Vector2.Down;

    [Export]
    public int ParticleCount { get; set; } = 200;

    [Export]
    public float PoolVelocityMax { get; set; } = 120f;

    [Export]
    public float SprayVelocityMax { get; set; } = 200f;

    [Export]
    public float SprayRatio { get; set; } = 0.3f;

    // Store randomized values for this instance
    private Vector2 _randSprayDirection;
    private int _randParticleCount;
    private float _randPoolVelocityMax;
    private float _randSprayVelocityMax;
    private float _randSprayRatio;

    [Export]
    public Vector2 InheritedVelocity { get; set; } = Vector2.Zero;

    private static ImageTexture? _bloodTexture;
    private readonly List<BloodParticle> _activeParticles = [];

    private sealed class BloodParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public float Alpha;
        public bool Settled;
    }

    public override void _Ready()
    {
        base._Ready();
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        // Randomize values ±10%
        _randSprayDirection = SprayDirection.Rotated(rng.RandfRange(-0.1f, 0.1f));
        _randParticleCount = (int)(ParticleCount * rng.RandfRange(0.5f, 1.3f));
        _randPoolVelocityMax = PoolVelocityMax * rng.RandfRange(0.6f, 1.4f);
        _randSprayVelocityMax = SprayVelocityMax * rng.RandfRange(0.6f, 1.2f);
        _randSprayRatio = SprayRatio * rng.RandfRange(0.9f, 1.1f);

        SpawnParticles();
    }

    public override void _PhysicsProcess(double delta)
    {
        var deltaF = (float)delta;
        var allSettled = true;
        var spaceState = GetWorld2D().DirectSpaceState;
        var globalOrigin = GlobalPosition;

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        foreach (var particle in _activeParticles)
        {
            if (particle.Settled)
            {
                continue;
            }

            allSettled = false;

            // Apply friction (top-down view, no gravity)
            particle.Velocity *= rng.RandfRange(0.85f, 0.94f);

            // Calculate new position
            var from = globalOrigin + particle.Position;
            var to = from + (particle.Velocity * deltaF);

            // Raycast to check for wall collision (layer 1)
            var rayParams = new PhysicsRayQueryParameters2D
            {
                From = from,
                To = to,
                CollisionMask = 1
            };
            var result = spaceState.IntersectRay(rayParams);

            if (result.Count > 0)
            {
                // Hit a wall: settle at collision point
                particle.Position = ((Vector2)result["position"]) - globalOrigin;
                particle.Settled = true;
                CreatePermanentSprite(particle);
                continue;
            }

            // No collision, update position
            particle.Position += particle.Velocity * deltaF;

            // Settle when velocity is low enough
            if (particle.Velocity.LengthSquared() < 10f)
            {
                particle.Settled = true;
                CreatePermanentSprite(particle);
            }
        }

        if (allSettled)
        {
            SetPhysicsProcess(false);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var texture = GetSharedTexture();

        foreach (var particle in _activeParticles)
        {
            if (particle.Settled)
            {
                continue;
            }

            var color = new Color(0.8f, 0f, 0f, particle.Alpha);
            var scale = particle.Size / 4f;
            DrawTextureRect(texture, new Rect2(particle.Position - (Vector2.One * 2f * scale), Vector2.One * 4f * scale), false, color);
        }
    }

    private void SpawnParticles()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        var sprayCount = (int)(_randParticleCount * _randSprayRatio);

        for (var i = 0; i < _randParticleCount; i++)
        {
            var isSpray = i < sprayCount;
            var maxVelocity = isSpray ? _randSprayVelocityMax : _randPoolVelocityMax;

            var angle = _randSprayDirection.Angle() + rng.RandfRange(-0.4f, 0.4f);
            if (!isSpray)
            {
                angle = rng.RandfRange(0f, Mathf.Tau);
            }

            var speed = rng.RandfRange(maxVelocity * 0.3f, maxVelocity);
            var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            _activeParticles.Add(new BloodParticle
            {
                Position = Vector2.Zero,
                Velocity = velocity + InheritedVelocity,
                Size = rng.RandfRange(0.8f, 2.0f),
                Alpha = rng.RandfRange(0.5f, 1f),
                Settled = false
            });
        }
    }

    private void CreatePermanentSprite(BloodParticle particle)
    {
        var sprite = new Sprite2D
        {
            Texture = GetSharedTexture(),
            GlobalPosition = GlobalPosition + particle.Position,
            Scale = Vector2.One * (particle.Size / 4f),
            Modulate = new Color(0.8f, 0f, 0f, particle.Alpha),
            ZIndex = ZIndex
        };

        GetTree().Root.AddChild(sprite);
    }

    private static ImageTexture GetSharedTexture()
    {
        if (_bloodTexture is not null)
        {
            return _bloodTexture;
        }

        const int textureSize = 4;
        var image = Image.CreateEmpty(textureSize, textureSize, false, Image.Format.Rgba8);

        // Simple filled circle
        for (var y = 0; y < textureSize; y++)
        {
            for (var x = 0; x < textureSize; x++)
            {
                var center = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
                var distance = new Vector2(x, y).DistanceTo(center);

                if (distance <= textureSize * 0.5f)
                {
                    image.SetPixel(x, y, Colors.White);
                }
                else
                {
                    image.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                }
            }
        }

        _bloodTexture = ImageTexture.CreateFromImage(image);
        image.Dispose();

        return _bloodTexture;
    }
}
