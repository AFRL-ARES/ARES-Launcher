using System;
using System.IO;
using System.Threading.Tasks;
using ARESLauncher.Models;
using ARESLauncher.Tools;
using CliWrap;

namespace ARESLauncher.Services;

public class DatabaseManager(IExecutableGetter _executableGetter) : IDatabaseManager
{
  public DatabaseStatus DatabaseStatus { get; private set; } = DatabaseStatus.NonExistent;
  public async Task RunMigrations()
  {
    var serviceExe = _executableGetter.GetServiceExecutablePath();
    if (serviceExe is null)
    {
      return;
    }

    var workingDir = GetWorkingDir(serviceExe);
    await Cli.Wrap(serviceExe)
      .WithArguments(["--migrate"])
      .WithWorkingDirectory(workingDir)
      .ExecuteAsync();

    await Refresh();
  }

  public async Task Refresh()
  {
    var serviceExe = _executableGetter.GetServiceExecutablePath();
    if (serviceExe is null)
    {
      return;
    }

    var workingDir = GetWorkingDir(serviceExe);
    var checkResult = await Cli.Wrap(serviceExe)
      .WithArguments(["--check-database"])
      .WithValidation(CommandResultValidation.None)
      .WithWorkingDirectory(workingDir)
      .ExecuteAsync();
    
    DatabaseStatus = ExitCodeToDbStatus.GetDatabaseStatus(checkResult.ExitCode);
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