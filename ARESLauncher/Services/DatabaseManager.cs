using System;
using System.IO;
using System.Threading.Tasks;
using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;
using CliWrap;

namespace ARESLauncher.Services;

public class DatabaseManager(IExecutableGetter _executableGetter, IAppConfigurationService _configurationService) : IDatabaseManager
{
  public DatabaseStatus DatabaseStatus { get; private set; } = DatabaseStatus.NonExistent;
  public async Task RunMigrations()
  {
    var executable = GetDatabaseExecutablePath();
    if(executable is null)
      return;

    var workingDir = GetWorkingDir(executable);
    await Cli.Wrap(executable)
      .WithArguments(["--migrate"])
      .WithWorkingDirectory(workingDir)
      .ExecuteAsync();

    await Refresh();
  }

  public async Task Refresh()
  {
    var executable = GetDatabaseExecutablePath();
    if(executable is null)
      return;

    var workingDir = GetWorkingDir(executable);
    var checkResult = await Cli.Wrap(executable)
      .WithArguments(["--check-database"])
      .WithValidation(CommandResultValidation.None)
      .WithWorkingDirectory(workingDir)
      .ExecuteAsync();
    
    DatabaseStatus = ExitCodeToDbStatus.GetDatabaseStatus(checkResult.ExitCode);
  }

  public Task CreateSnapshot(NuGet.Versioning.SemanticVersion version)
  {
    if(_configurationService.Current.DatabaseProvider != DatabaseProvider.Sqlite)
      return Task.CompletedTask;

    var dbPath = _configurationService.Current.SqliteDatabasePath;
    if(!File.Exists(dbPath)) return Task.CompletedTask;

    var snapshotPath = GetSnapshotPath(version);
    var snapshotDir = Path.GetDirectoryName(snapshotPath);
    if(snapshotDir is not null) Directory.CreateDirectory(snapshotDir);

    File.Copy(dbPath, snapshotPath, true);
    return Task.CompletedTask;
  }

  public Task<bool> HasSnapshot(NuGet.Versioning.SemanticVersion version)
  {
    if(_configurationService.Current.DatabaseProvider != DatabaseProvider.Sqlite)
      return Task.FromResult(false);

    var snapshotPath = GetSnapshotPath(version);
    return Task.FromResult(File.Exists(snapshotPath));
  }

  public async Task RestoreSnapshot(NuGet.Versioning.SemanticVersion version)
  {
    try
    {
      if(_configurationService.Current.DatabaseProvider != DatabaseProvider.Sqlite)
        return;

      var snapshotPath = GetSnapshotPath(version);
      if(!File.Exists(snapshotPath))
        return;

      var dbPath = _configurationService.Current.SqliteDatabasePath;
      var dbDir = Path.GetDirectoryName(dbPath);
      if(dbDir is not null)
        Directory.CreateDirectory(dbDir);

      File.Copy(snapshotPath, dbPath, true);
      await Refresh();
    }

    catch(Exception e)
    {
      Console.WriteLine($"Failed to restore snapshot! {e.Message}");
    }

  }

  public Task Reset()
  {
    if(_configurationService.Current.DatabaseProvider == DatabaseProvider.Sqlite)
    {
      var dbPath = _configurationService.Current.SqliteDatabasePath;
      if(File.Exists(dbPath))
        File.Delete(dbPath);
    }

    DatabaseStatus = DatabaseStatus.NonExistent;
    return Task.CompletedTask;
  }

  private string GetSnapshotPath(NuGet.Versioning.SemanticVersion version)
  {
    var dbPath = _configurationService.Current.SqliteDatabasePath;
    var dbDir = Path.GetDirectoryName(dbPath) ?? "";
    var fileName = Path.GetFileNameWithoutExtension(dbPath);
    var extension = Path.GetExtension(dbPath);
    return Path.Combine(dbDir, "Snapshots", $"{fileName}_v{version.ToNormalizedString()}{extension}.bak");
  }

  private string? GetDatabaseExecutablePath()
  {
    return _configurationService.Current.InstalledAresLayout == AresReleaseLayout.UnifiedUiOnly
      ? _executableGetter.GetUiExecutablePath()
      : _executableGetter.GetServiceExecutablePath();
  }

  private static string GetWorkingDir(string path)
  {
    var workingDir = Path.GetDirectoryName(path);
    if(workingDir is null)
    {
      workingDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      workingDir = Path.Combine(workingDir, "ARES");
      Directory.CreateDirectory(workingDir);
    }

    return workingDir;
  }
}
