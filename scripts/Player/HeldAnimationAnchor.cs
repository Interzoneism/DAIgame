namespace DAIgame.Player;

using Godot;

[GlobalClass]
public partial class HeldAnimationAnchor : Resource
{
    [Export]
    public string AnimationName { get; set; } = "";

    [Export]
    public Vector2 Offset { get; set; } = Vector2.Zero;

    [Export(PropertyHint.Range, "-180,180,1")]
    public float RotationDegrees { get; set; } = 0f;
}
