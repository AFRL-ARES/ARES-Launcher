using ARESLauncher.Configuration;
using ARESLauncher.Services;

namespace ARESLauncher.Tests;

[TestFixture]
public class ExecutableGetterTests
{
  [Test]
  public void GetDemoServiceExecutablePath_UsesTheDemoServiceDirectory()
  {
    var configuration = new LauncherConfiguration
    {
      UiBinaryPath = Path.Combine("binaries", "current")
    };
    var executableGetter = new ExecutableGetter(new FakeAppConfigurationService(configuration));
    var expectedFileName = OperatingSystem.IsWindows()
      ? "DemoRemotePlanner.exe"
      : "DemoRemotePlanner";

    var path = executableGetter.GetDemoServiceExecutablePath("DemoRemotePlanner");

    Assert.That(path, Is.EqualTo(Path.Combine(
      configuration.UiBinaryPath,
      "demo",
      "DemoRemotePlanner",
      expectedFileName)));
  }
}
