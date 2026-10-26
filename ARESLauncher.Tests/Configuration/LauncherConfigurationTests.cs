using System.Text.Json;
using ARESLauncher.Configuration;
using ARESLauncher.Models;

namespace ARESLauncher.Tests.Configuration;

[TestFixture]
public class LauncherConfigurationTests
{
  [Test]
  public void LauncherConfiguration_SerializesAndDeserializes_DemoMode()
  {
    var configuration = new LauncherConfiguration
    {
      DemoMode = true
    };

    var json = JsonSerializer.Serialize(configuration);
    var roundTripped = JsonSerializer.Deserialize<LauncherConfiguration>(json);

    Assert.That(json, Does.Contain("DemoMode"));
    Assert.That(roundTripped, Is.Not.Null);
    Assert.That(roundTripped.DemoMode, Is.True);
  }
}