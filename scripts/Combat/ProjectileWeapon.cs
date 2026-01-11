namespace DAIgame.Combat;

using Godot;

/// <summary>
/// Projectile-based weapon that spawns bullets instead of using hitscan.
/// </summary>
public partial class ProjectileWeapon : Node2D
{
    /// <summary>
    /// Bullet scene to instantiate when firing.
    /// </summary>
    [Export]
    public PackedScene? BulletScene { get; set; }

    /// <summary>
    /// Fires a projectile in the given direction from the given origin.
    /// </summary>
    public void Fire(Vector2 origin, Vector2 direction)
    {
        if (BulletScene is null)
        {
            GD.PrintErr("ProjectileWeapon: BulletScene is not set!");
            return;
        }

        var bullet = BulletScene.Instantiate<Projectile>();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = origin;
        bullet.Initialize(direction);
    }
}
