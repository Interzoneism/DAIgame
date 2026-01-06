namespace DAIgame.Core;

using Chickensoft.GoDotTest;
using DAIgame.AI;
using DAIgame.Player;
using Godot;
using Shouldly;

/// <summary>
/// Tests for the IDamageable interface implementation.
/// </summary>
public class IDamageableTest : TestClass
{
    public IDamageableTest(Node testScene) : base(testScene) { }

    [Test]
    public void PlayerController_ImplementsIDamageable()
    {
        // Verify PlayerController implements IDamageable
        typeof(PlayerController).GetInterface(nameof(IDamageable)).ShouldNotBeNull();
    }

    [Test]
    public void ZombieController_ImplementsIDamageable()
    {
        // Verify ZombieController implements IDamageable
        typeof(ZombieController).GetInterface(nameof(IDamageable)).ShouldNotBeNull();
    }

    [Test]
    public void IDamageable_HasCorrectSignature()
    {
        // Verify the interface has the expected method signature
        var method = typeof(IDamageable).GetMethod(nameof(IDamageable.ApplyDamage));
        method.ShouldNotBeNull();

        var parameters = method.GetParameters();
        parameters.Length.ShouldBe(4);
        parameters[0].ParameterType.ShouldBe(typeof(float));
        parameters[0].Name.ShouldBe("amount");
        parameters[1].ParameterType.ShouldBe(typeof(Vector2));
        parameters[1].Name.ShouldBe("fromPos");
        parameters[2].ParameterType.ShouldBe(typeof(Vector2));
        parameters[2].Name.ShouldBe("hitPos");
        parameters[3].ParameterType.ShouldBe(typeof(Vector2));
        parameters[3].Name.ShouldBe("hitNormal");
    }
}
