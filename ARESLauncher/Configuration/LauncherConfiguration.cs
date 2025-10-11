using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using ARESLauncher.Models;

namespace ARESLauncher.Configuration;

public class LauncherConfiguration
{
  [JsonIgnore] public static string AppPath =
    Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) ?? Directory.GetCurrentDirectory();

  public AresSource DefaultAresRepo { get; set; } = new AresSource("AFRL-ARES", "ARES");

  public AresSource[] AvailableAresRepos { get; set; } = [new AresSource("AFRL-ARES", "ARES")];
  public string UiDataPath { get; set; } = Path.Join(AppPath, "Data", "UI");
  public string ServiceDataPath { get; set; } = Path.Join(AppPath, "Data", "Service");
  public string SqliteDatabasePath { get; set; } = Path.Join(AppPath, "Data", "ares_database.db");
  
}