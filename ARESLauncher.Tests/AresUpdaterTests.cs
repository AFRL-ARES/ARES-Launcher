using System.IO;
using System.Threading.Tasks;
using ARESLauncher.Configuration;
using ARESLauncher.Models;
using ARESLauncher.Services;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Versioning;

namespace ARESLauncher.Tests;

[TestFixture]
public class AresUpdaterTests
{
  [Test]
  public async Task Update_DownloadsAndUnpacksSinglePackage()
  {
    var tempRoot = TestPaths.CreateTempDirectory();
    var uiDir = Path.Combine(tempRoot, "ui");
    var serviceDir = Path.Combine(tempRoot, "service");
    Directory.CreateDirectory(uiDir);
    Directory.CreateDirectory(serviceDir);
    File.WriteAllText(Path.Combine(uiDir, "stale.txt"), "old");
    File.WriteAllText(Path.Combine(serviceDir, "stale.txt"), "old");

    try
    {
      var archivePath = TestArchives.CreateArchive(tempRoot, "combined.zip", ("app.bin", "payload"));
      var source = new AresSource("AFRL-ARES", "ARES");
      var version = new SemanticVersion(1, 2, 3);
      var downloader = new RecordingAresDownloader(archivePath);
      var configuration = new FakeAppConfigurationService(new LauncherConfiguration
      {
        CurrentAresRepo = source,
        UiBinaryPath = uiDir,
        ServiceBinaryPath = serviceDir,
        GitToken = "token"
      });
      var appSettingsUpdater = new FakeAppSettingsUpdater();
      var certificateManager = new FakeCertificateManager();
      var databaseManager = new FakeDatabaseManager();
      var binaryManager = new FakeAresBinaryManager();

      var updater = new AresUpdater(
        downloader,
        configuration,
        appSettingsUpdater,
        certificateManager,
        databaseManager,
        binaryManager,
        NullLogger<AresUpdater>.Instance);

      await updater.Update(version);

      Assert.That(downloader.DownloadCallCount, Is.EqualTo(1));
      Assert.That(downloader.LastSource, Is.EqualTo(source));
      Assert.That(downloader.LastVersion, Is.EqualTo(version));
      Assert.That(downloader.LastDestination, Is.EqualTo(Path.GetTempPath()));
      Assert.That(downloader.LastAuthToken, Is.EqualTo("token"));

      Assert.That(File.Exists(Path.Combine(uiDir, "app.bin")), Is.True);
      Assert.That(File.Exists(Path.Combine(uiDir, "stale.txt")), Is.False);
      Assert.That(Directory.Exists(serviceDir), Is.False);

      var metadata = BinaryMetadataHelper.ReadMetadata(uiDir);
      Assert.That(metadata, Is.Not.Null);
      Assert.That(metadata!.Source, Is.EqualTo(source));
      Assert.That(metadata.Version, Is.EqualTo(version.ToNormalizedString()));

      Assert.That(appSettingsUpdater.UpdateAllCallCount, Is.EqualTo(1));
      Assert.That(certificateManager.UpdateCallCount, Is.EqualTo(1));
      Assert.That(databaseManager.RefreshCallCount, Is.EqualTo(1));
      Assert.That(databaseManager.RunMigrationsCallCount, Is.EqualTo(0));
    }
    finally
    {
      TestPaths.DeleteDirectoryIfExists(tempRoot);
    }
  }
}
