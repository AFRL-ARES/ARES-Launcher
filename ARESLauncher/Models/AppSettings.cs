using System.Collections.Generic;

namespace ARESLauncher.Models;

public class AppSettings
{
  public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.None;
  public Dictionary<DatabaseProvider, string> ConnectionStrings { get; set; } = new();
}