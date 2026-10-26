using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;
using CliWrap;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public class DatabaseManager : IDatabaseManager
{
  private readonly IExecutableGetter _executableGetter;
  private readonly IAppConfigurationService _configurationService;
  private readonly ILogger<DatabaseManager> _logger;
  private readonly SemaphoreSlim _refreshLock = new(1, 1);

  public DatabaseManager(IExecutableGetter executableGetter, IAppConfigurationService configurationService, ILogger<DatabaseManager> logger)
  {
    _executableGetter = executableGetter;
    _configurationService = configurationService;
    _logger = logger;
  }

  public async Task RunMigrations()
  {
    var executable = GetDatabaseExecutablePath();
    if(executable is null)
    {
      _logger.LogError("Database Executable Path was NULL! This will likely cause install errors!");
      return;
    }

    _logger.LogInformation("Determined Database Executable Path: {path}", executable);

    var workingDir = GetWorkingDir(executable);

    try
    {
      await Cli.Wrap(executable)
      .WithArguments(["--migrate"])
      .WithWorkingDirectory(workingDir)
      .ExecuteAsync();
    }

    catch(Exception ex)
    {
      _logger.LogError(ex, "Error Encountered while performing database migration!");
    }

    await Refresh();
  }

  public async Task Refresh()
  {
    // Prevent overlapping refresh calls if one is already running
    if(!await _refreshLock.WaitAsync(0))
    {
      _logger.LogInformation("Refresh is already in progress. Skipping duplicate call.");
      return;
    }

    try
    {
      var executable = GetDatabaseExecutablePath();
      if(executable is null)
        return;

      var workingDir = GetWorkingDir(executable);

      using var cts = new CancellationTokenSource();
      cts.CancelAfter(TimeSpan.FromSeconds(10));

      var checkResult = await Cli.Wrap(executable)
          .WithArguments(["--check-database"])
          .WithValidation(CommandResultValidation.None)
          .WithWorkingDirectory(workingDir)
          .ExecuteAsync(cts.Token);

      DatabaseStatus = ExitCodeToDbStatus.GetDatabaseStatus(checkResult.ExitCode);
      _logger.LogInformation("Database Status Code Determined: {status}", DatabaseStatus);
    }
    catch(OperationCanceledException)
    {
      _logger.LogError("The database check process timed out and was terminated.");
      DatabaseStatus = DatabaseStatus.Error;
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Encountered an error trying to check the database!");
      DatabaseStatus = DatabaseStatus.Error;
    }
    finally
    {
      _refreshLock.Release();
      _logger.LogDebug("Releasing the database refresh lock");
    }
  }

  public async Task CreateSnapshot(NuGet.Versioning.SemanticVersion version)
  {
    if(_configurationService.Current.DatabaseProvider != DatabaseProvider.Sqlite)
      return;

    var dbPath = _configurationService.Current.SqliteDatabasePath;
    if(!File.Exists(dbPath))
      return;

    var snapshotPath = GetSnapshotPath(version);
    var snapshotDir = Path.GetDirectoryName(snapshotPath);

    await Task.Run(() =>
    {
      if(snapshotDir is not null)
        Directory.CreateDirectory(snapshotDir);

      File.Copy(dbPath, snapshotPath, true);
    });
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
      _logger.LogError(e, "Encountered an error when trying to restore a snapshot!");
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
    => _executableGetter.GetUiExecutablePath();

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

  public DatabaseStatus DatabaseStatus { get; private set; } = DatabaseStatus.NonExistent;
}