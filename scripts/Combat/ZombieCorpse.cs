namespace DAIgame.Combat;

using Godot;

/// <summary>
/// Simple static zombie corpse that remains after death.
/// </summary>
public partial class ZombieCorpse : Sprite2D
{
	public override void _Ready()
	{
		base._Ready();
		Centered = true;
		ZIndex = 3; // keeps the corpse below active characters
	}
}
