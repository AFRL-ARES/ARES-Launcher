using System;
using ARESLauncher.Models;

namespace ARESLauncher.Tools;

public static class ExitCodeToDbStatus
{
  public static DatabaseStatus GetDatabaseStatus(int exitCode)
  {
    return exitCode switch
    {
      0 => DatabaseStatus.UpToDate,
      10 => DatabaseStatus.Outdated,
      11 => DatabaseStatus.NonExistent,
      _ => throw new ArgumentOutOfRangeException($"Invalid exit code: {exitCode}. Can't map that to a database status. Something might be broken")
    };
  }
}