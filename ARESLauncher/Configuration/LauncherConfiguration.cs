using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using ARESLauncher.Models;

namespace ARESLauncher.Configuration;

public class LauncherConfiguration
{
  [JsonIgnore] private static string _appPath =
    Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) ?? Directory.GetCurrentDirectory();

  public AresSource DefaultAresRepo { get; set; } = new AresSource("AFRL-ARES", "ARES");

  public AresSource[] AvailableAresRepos { get; set; } = [new AresSource("AFRL-ARES", "ARES")];
  public string UiDataPath { get; set; } = Path.Join(_appPath, "Data", "UI");
  public string ServiceDataPath { get; set; } = Path.Join(_appPath, "Data", "Service");
  public string SqliteDatabasePath { get; set; } = Path.Join(_appPath, "Data", "ares_database.db");
  public string GitToken { get; set; } = "";
}