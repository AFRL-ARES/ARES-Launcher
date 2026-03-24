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
    if (executable is null)
    {
      return;
    }

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
    if (executable is null)
    {
      return;
    }

    var workingDir = GetWorkingDir(executable);
    var checkResult = await Cli.Wrap(executable)
      .WithArguments(["--check-database"])
      .WithValidation(CommandResultValidation.None)
      .WithWorkingDirectory(workingDir)
      .ExecuteAsync();
    
    DatabaseStatus = ExitCodeToDbStatus.GetDatabaseStatus(checkResult.ExitCode);
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
    if (workingDir is null)
    {
      workingDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      workingDir = Path.Combine(workingDir, "ARES");
      Directory.CreateDirectory(workingDir);
    }

    return workingDir;
  }
}
