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
      _ => DatabaseStatus.Error
    };
  }
}
