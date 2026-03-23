using System;
using System.Threading.Tasks;
using ARESLauncher.Configuration;
using ARESLauncher.Models;
using ARESLauncher.Models.AppSettings;
using ARESLauncher.Services;
using ARESLauncher.Services.Configuration;
using NuGet.Versioning;

namespace ARESLauncher.Tests;

internal sealed class RecordingAresDownloader(string archivePath) : IAresDownloader
{
  public int DownloadCallCount { get; private set; }
  public AresSource? LastSource { get; private set; }
  public SemanticVersion? LastVersion { get; private set; }
  public string? LastDestination { get; private set; }
  public string? LastAuthToken { get; private set; }

  public Task<SemanticVersion[]> GetAvailableVersions(AresSource source, string? authToken)
  {
    throw new NotSupportedException();
  }

  public Task<SemanticVersion[]> GetAvailableVersions(LauncherSource soruce)
  {
    throw new NotSupportedException();
  }

  public Task<string> Download(LauncherSource source, SemanticVersion version, string destination, string? authToken,
    IProgress<double>? progress = null)
  {
    throw new NotSupportedException();
  }

  public Task<string> Download(AresSource source, SemanticVersion version, string destination, string? authToken,
    IProgress<double>? progress = null)
  {
    DownloadCallCount++;
    LastSource = source;
    LastVersion = version;
    LastDestination = destination;
    LastAuthToken = authToken;
    progress?.Report(1);
    return Task.FromResult(archivePath);
  }
}

internal sealed class FakeAppConfigurationService(LauncherConfiguration current) : IAppConfigurationService
{
  public LauncherConfiguration Current { get; private set; } = current;

  public event EventHandler? ConfigUpdated;

  public void Update(Action<LauncherConfiguration> applyChanges)
  {
    applyChanges(Current);
    ConfigUpdated?.Invoke(this, EventArgs.Empty);
  }
}

internal sealed class FakeAppSettingsUpdater : IAppSettingsUpdater
{
  public int UpdateAllCallCount { get; private set; }

  public void Update(AresComponent component)
  {
  }

  public void UpdateAll()
  {
    UpdateAllCallCount++;
  }
}

internal sealed class FakeCertificateManager : ICertificateManager
{
  public int UpdateCallCount { get; private set; }

  public Task Update()
  {
    UpdateCallCount++;
    return Task.CompletedTask;
  }
}

internal sealed class FakeDatabaseManager : IDatabaseManager
{
  public DatabaseStatus DatabaseStatus => DatabaseStatus.UpToDate;
  public int RefreshCallCount { get; private set; }
  public int RunMigrationsCallCount { get; private set; }

  public Task RunMigrations()
  {
    RunMigrationsCallCount++;
    return Task.CompletedTask;
  }

  public Task Refresh()
  {
    RefreshCallCount++;
    return Task.CompletedTask;
  }
}

internal sealed class FakeAresBinaryManager : IAresBinaryManager
{
  public SemanticVersion? CurrentVersion => null;
  public AppSettingsService? ServiceSettings => null;
  public AppSettingsUi? UiSettings => null;
  public AresSource? CurrentSource => null;

  public Task Refresh()
  {
    return Task.CompletedTask;
  }

  public void SetUiDataPath(Uri path)
  {
  }

  public void SetServiceDataPath(Uri uri)
  {
  }
}
