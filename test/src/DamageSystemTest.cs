namespace DAIgame.Core;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
/// Tests for the IDamageable interface and related damage system.
/// </summary>
public partial class DamageSystemTest : TestClass
{
  public DamageSystemTest(Node testScene) : base(testScene) { }

  /// <summary>
  /// Helper class to test the IDamageable interface.
  /// </summary>
  private sealed partial class TestDamageable : Node2D, IDamageable
  {
    public float TotalDamageReceived { get; private set; }
    public int DamageCount { get; private set; }
    public Vector2 LastFromPos { get; private set; }
    public Vector2 LastHitPos { get; private set; }
    public Vector2 LastHitNormal { get; private set; }

    public void ApplyDamage(float amount, Vector2 fromPos, Vector2 hitPos, Vector2 hitNormal)
    {
      TotalDamageReceived += amount;
      DamageCount++;
      LastFromPos = fromPos;
      LastHitPos = hitPos;
      LastHitNormal = hitNormal;
    }
  }

  [Test]
  public void IDamageable_ApplyDamage_AccumulatesDamage()
  {
    var target = new TestDamageable();

    target.ApplyDamage(10f, Vector2.Zero, Vector2.One, Vector2.Up);
    target.ApplyDamage(15f, Vector2.Zero, Vector2.One, Vector2.Up);

    target.TotalDamageReceived.ShouldBe(25f);
    target.DamageCount.ShouldBe(2);

    target.Free();
  }

  [Test]
  public void IDamageable_ApplyDamage_StoresPositionalData()
  {
    var target = new TestDamageable();
    var fromPos = new Vector2(100, 200);
    var hitPos = new Vector2(50, 75);
    var hitNormal = new Vector2(-1, 0);

    target.ApplyDamage(5f, fromPos, hitPos, hitNormal);

    target.LastFromPos.ShouldBe(fromPos);
    target.LastHitPos.ShouldBe(hitPos);
    target.LastHitNormal.ShouldBe(hitNormal);

    target.Free();
  }

  [Test]
  public void IDamageable_CanBeUsedPolymorphically()
  {
    IDamageable target = new TestDamageable();

    // Should be able to call through the interface
    target.ApplyDamage(20f, Vector2.Zero, Vector2.One, Vector2.Down);

    ((TestDamageable)target).TotalDamageReceived.ShouldBe(20f);

    ((Node)target).Free();
  }
}
