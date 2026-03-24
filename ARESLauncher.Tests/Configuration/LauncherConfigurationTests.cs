using System.Text.Json;
using ARESLauncher.Configuration;
using ARESLauncher.Models;

namespace ARESLauncher.Tests.Configuration;

[TestFixture]
public class LauncherConfigurationTests
{
  [Test]
  public void LauncherConfiguration_SerializesAndDeserializes_InstalledLayout()
  {
    var configuration = new LauncherConfiguration
    {
      InstalledAresLayout = AresReleaseLayout.UnifiedUiOnly
    };

    var json = JsonSerializer.Serialize(configuration);
    var roundTripped = JsonSerializer.Deserialize<LauncherConfiguration>(json);

    Assert.That(json, Does.Contain("UnifiedUiOnly"));
    Assert.That(roundTripped, Is.Not.Null);
    Assert.That(roundTripped!.InstalledAresLayout, Is.EqualTo(AresReleaseLayout.UnifiedUiOnly));
  }
}
