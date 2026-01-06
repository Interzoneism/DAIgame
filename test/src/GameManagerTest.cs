namespace DAIgame.Core;

using System;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
/// Tests for GameManager singleton access patterns.
/// </summary>
public class GameManagerTest : TestClass
{
    public GameManagerTest(Node testScene) : base(testScene) { }

    [Test]
    public void GameManager_GetRequiredInstance_ThrowsWhenNull()
    {
        // Temporarily clear the instance for testing
        var originalInstance = GameManager.Instance;

        // Use reflection to set the private setter
        var instanceProperty = typeof(GameManager).GetProperty(nameof(GameManager.Instance));
        instanceProperty!.SetValue(null, null);

        try
        {
            Should.Throw<InvalidOperationException>(() => GameManager.GetRequiredInstance());
        }
        finally
        {
            // Restore the original instance
            instanceProperty.SetValue(null, originalInstance);
        }
    }

    [Test]
    public void GameManager_TryGetInstance_ReturnsFalseWhenNull()
    {
        // Temporarily clear the instance for testing
        var originalInstance = GameManager.Instance;

        var instanceProperty = typeof(GameManager).GetProperty(nameof(GameManager.Instance));
        instanceProperty!.SetValue(null, null);

        try
        {
            var result = GameManager.TryGetInstance(out var instance);
            result.ShouldBeFalse();
        }
        finally
        {
            // Restore the original instance
            instanceProperty.SetValue(null, originalInstance);
        }
    }

    [Test]
    public void GameManager_TryGetInstance_ReturnsTrueWhenAvailable()
    {
        // This test assumes GameManager is available in the test scene
        if (GameManager.Instance is null)
        {
            // Skip test if GameManager not in scene
            return;
        }

        var result = GameManager.TryGetInstance(out var instance);
        result.ShouldBeTrue();
        instance.ShouldBe(GameManager.Instance);
    }

    [Test]
    public void GameManager_GetRequiredInstance_ReturnsInstanceWhenAvailable()
    {
        // This test assumes GameManager is available in the test scene
        if (GameManager.Instance is null)
        {
            // Skip test if GameManager not in scene
            return;
        }

        var instance = GameManager.GetRequiredInstance();
        instance.ShouldBe(GameManager.Instance);
    }
}
