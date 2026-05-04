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
      Assert.That(metadata.Layout, Is.EqualTo(AresReleaseLayout.UnifiedUiOnly));
      Assert.That(configuration.Current.InstalledAresLayout, Is.EqualTo(AresReleaseLayout.UnifiedUiOnly));

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

  [Test]
  public async Task GetAvailableVersions_IncludesDebugFolderPackages()
  {
    var debugPath = Path.Combine(AppContext.BaseDirectory, "Debug");
    Directory.CreateDirectory(debugPath);
    var version = new SemanticVersion(8, 8, 8);
    var archiveName = $"ARES-v{version.ToNormalizedString()}.zip";
    var archivePath = TestArchives.CreateArchive(debugPath, archiveName, ("empty", ""));

    try
    {
      var downloader = new RecordingAresDownloader(null);
      downloader.AvailableReleases = [new AresRelease { Version = new SemanticVersion(1, 0, 0), IsBeta = false }];

      var updater = new AresUpdater(
        downloader,
        new FakeAppConfigurationService(new LauncherConfiguration()),
        new FakeAppSettingsUpdater(),
        new FakeCertificateManager(),
        new FakeDatabaseManager(),
        new FakeAresBinaryManager(),
        NullLogger<AresUpdater>.Instance);

      var releases = await updater.GetAvailableVersions();

      Assert.That(releases.Select(r => r.Version), Does.Contain(version));
      Assert.That(releases.Select(r => r.Version), Does.Contain(new SemanticVersion(1, 0, 0)));
    }
    finally
    {
      if(File.Exists(archivePath)) File.Delete(archivePath);
    }
  }

  [Test]
  public async Task GetAvailableVersions_FiltersBetaReleases_WhenNotOptedIn()
  {
      var downloader = new RecordingAresDownloader(null);
      downloader.AvailableReleases = [
          new AresRelease { Version = new SemanticVersion(1, 0, 0), IsBeta = false },
          new AresRelease { Version = new SemanticVersion(1, 1, 0), IsBeta = true }
      ];

      var updater = new AresUpdater(
        downloader,
        new FakeAppConfigurationService(new LauncherConfiguration { IncludeBeta = false }),
        new FakeAppSettingsUpdater(),
        new FakeCertificateManager(),
        new FakeDatabaseManager(),
        new FakeAresBinaryManager(),
        NullLogger<AresUpdater>.Instance);

      var releases = await updater.GetAvailableVersions();

      Assert.That(releases.Select(r => r.Version), Does.Contain(new SemanticVersion(1, 0, 0)));
      Assert.That(releases.Select(r => r.Version), Does.Not.Contain(new SemanticVersion(1, 1, 0)));
  }

  [Test]
  public async Task GetAvailableVersions_IncludesBetaReleases_WhenOptedIn()
  {
      var downloader = new RecordingAresDownloader(null);
      downloader.AvailableReleases = [
          new AresRelease { Version = new SemanticVersion(1, 0, 0), IsBeta = false },
          new AresRelease { Version = new SemanticVersion(1, 1, 0), IsBeta = true }
      ];

      var updater = new AresUpdater(
        downloader,
        new FakeAppConfigurationService(new LauncherConfiguration { IncludeBeta = true }),
        new FakeAppSettingsUpdater(),
        new FakeCertificateManager(),
        new FakeDatabaseManager(),
        new FakeAresBinaryManager(),
        NullLogger<AresUpdater>.Instance);

      var releases = await updater.GetAvailableVersions();

      Assert.That(releases.Select(r => r.Version), Does.Contain(new SemanticVersion(1, 0, 0)));
      Assert.That(releases.Select(r => r.Version), Does.Contain(new SemanticVersion(1, 1, 0)));
  }

  [Test]
  public async Task Update_UsesDebugFolderPackage_WhenAvailable()
  {
    var debugPath = Path.Combine(AppContext.BaseDirectory, "Debug");
    Directory.CreateDirectory(debugPath);
    var tempRoot = TestPaths.CreateTempDirectory();
    var uiDir = Path.Combine(tempRoot, "ui");
    Directory.CreateDirectory(uiDir);

    var version = new SemanticVersion(9, 9, 9);
    var archiveName = $"ARES-v{version.ToNormalizedString()}.zip";
    var archivePath = TestArchives.CreateArchive(debugPath, archiveName, ("debug.bin", "content"));

    try
    {
      var downloader = new RecordingAresDownloader(null); // Should not be called
      var configuration = new FakeAppConfigurationService(new LauncherConfiguration
      {
        UiBinaryPath = uiDir,
        ServiceBinaryPath = uiDir
      });

      var updater = new AresUpdater(
        downloader,
        configuration,
        new FakeAppSettingsUpdater(),
        new FakeCertificateManager(),
        new FakeDatabaseManager(),
        new FakeAresBinaryManager(),
        NullLogger<AresUpdater>.Instance);

      await updater.Update(version);

      Assert.That(downloader.DownloadCallCount, Is.EqualTo(0));
      Assert.That(File.Exists(Path.Combine(uiDir, "debug.bin")), Is.True);

      var metadata = BinaryMetadataHelper.ReadMetadata(uiDir);
      Assert.That(metadata!.Version, Is.EqualTo(version.ToNormalizedString()));
    }
    finally
    {
      if(File.Exists(archivePath)) File.Delete(archivePath);
      TestPaths.DeleteDirectoryIfExists(tempRoot);
    }
  }

  [Test]
  public async Task Update_PersistsSplitLayout_WhenServiceExecutableExists()
  {
    var tempRoot = TestPaths.CreateTempDirectory();
    var uiDir = Path.Combine(tempRoot, "ui");

    try
    {
      var serviceName = OperatingSystem.IsWindows() ? "AresService.exe" : "AresService";
      var archivePath = TestArchives.CreateArchive(tempRoot, "combined.zip", ("UI", "ui"), (serviceName, "svc"));
      var source = new AresSource("AFRL-ARES", "ARES");
      var version = new SemanticVersion(2, 0, 0);
      var downloader = new RecordingAresDownloader(archivePath);
      var configuration = new FakeAppConfigurationService(new LauncherConfiguration
      {
        CurrentAresRepo = source,
        UiBinaryPath = uiDir,
        ServiceBinaryPath = uiDir
      });

      var updater = new AresUpdater(
        downloader,
        configuration,
        new FakeAppSettingsUpdater(),
        new FakeCertificateManager(),
        new FakeDatabaseManager(),
        new FakeAresBinaryManager(),
        NullLogger<AresUpdater>.Instance);

      await updater.Update(version);

      var metadata = BinaryMetadataHelper.ReadMetadata(uiDir);
      Assert.That(metadata, Is.Not.Null);
      Assert.That(metadata!.Layout, Is.EqualTo(AresReleaseLayout.SplitUiAndService));
      Assert.That(configuration.Current.InstalledAresLayout, Is.EqualTo(AresReleaseLayout.SplitUiAndService));
    }
    finally
    {
      TestPaths.DeleteDirectoryIfExists(tempRoot);
    }
  }

  [Test]
  public void InvalidateCache_DelegatesToDownloader()
  {
    var downloader = new RecordingAresDownloader(null);
    var updater = new AresUpdater(
      downloader,
      new FakeAppConfigurationService(new LauncherConfiguration()),
      new FakeAppSettingsUpdater(),
      new FakeCertificateManager(),
      new FakeDatabaseManager(),
      new FakeAresBinaryManager(),
      NullLogger<AresUpdater>.Instance);

    updater.InvalidateCache();

    Assert.That(downloader.InvalidateCacheCalled, Is.True);
  }

  [Test]
  public async Task CreateSnapshot_CallsDatabaseManagerCreateSnapshot()
  {
    var dbManager = new FakeDatabaseManager();
    var updater = new AresUpdater(
      new RecordingAresDownloader(null),
      new FakeAppConfigurationService(new LauncherConfiguration()),
      new FakeAppSettingsUpdater(),
      new FakeCertificateManager(),
      dbManager,
      new FakeAresBinaryManager(),
      NullLogger<AresUpdater>.Instance);

    var version = new SemanticVersion(1, 0, 0);
    await updater.CreateSnapshot(version);

    Assert.That(dbManager.CreateSnapshotCallCount, Is.EqualTo(1));
    Assert.That(dbManager.LastSnapshotVersion, Is.EqualTo(version));
  }

  [Test]
  public async Task RestoreSnapshot_CallsDatabaseManagerRestoreSnapshot()
  {
    var dbManager = new FakeDatabaseManager();
    var updater = new AresUpdater(
      new RecordingAresDownloader(null),
      new FakeAppConfigurationService(new LauncherConfiguration()),
      new FakeAppSettingsUpdater(),
      new FakeCertificateManager(),
      dbManager,
      new FakeAresBinaryManager(),
      NullLogger<AresUpdater>.Instance);

    var version = new SemanticVersion(1, 0, 0);
    await updater.RestoreSnapshot(version);

    Assert.That(dbManager.RestoreSnapshotCallCount, Is.EqualTo(1));
    Assert.That(dbManager.LastRestoreVersion, Is.EqualTo(version));
  }

  [Test]
  public async Task ResetDatabase_CallsDatabaseManagerReset()
  {
    var dbManager = new FakeDatabaseManager();
    var updater = new AresUpdater(
      new RecordingAresDownloader(null),
      new FakeAppConfigurationService(new LauncherConfiguration()),
      new FakeAppSettingsUpdater(),
      new FakeCertificateManager(),
      dbManager,
      new FakeAresBinaryManager(),
      NullLogger<AresUpdater>.Instance);

    await updater.ResetDatabase();

    Assert.That(dbManager.DatabaseStatus, Is.EqualTo(DatabaseStatus.NonExistent));
  }
}
