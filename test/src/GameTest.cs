namespace DAIgame;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class GameTest : TestClass
{
  public GameTest(Node testScene) : base(testScene) { }

  [Test]
  public void ProjectSetupTest()
  {
    // Basic test to verify test infrastructure is working
    // Replace with actual game tests as systems are implemented
    var projectName = ProjectSettings.GetSetting("application/config/name").AsString();
    projectName.ShouldBe("DAIgame");
  }
}
