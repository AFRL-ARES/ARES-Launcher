using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using ARESLauncher.Models;

namespace ARESLauncher.Configuration;

public class LauncherConfiguration
{
  [JsonIgnore] public static string AppPath =
    Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) ?? Directory.GetCurrentDirectory();

  public AresRepoDescription DefaultAresRepo { get; set; } = new AresRepoDescription("AFRL-ARES", "ARES");

  public AresRepoDescription[] AvailableAresRepos { get; set; } = [new AresRepoDescription("AFRL-ARES", "ARES")];
  public string UIDataPath { get; set; } = Path.Join(AppPath, "Data", "UI");
  public string ServiceDataPath { get; set; } = Path.Join(AppPath, "Data", "Service");
}