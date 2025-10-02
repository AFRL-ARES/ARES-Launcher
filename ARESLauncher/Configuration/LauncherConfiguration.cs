using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ARESLauncher.Configuration;

public class LauncherConfiguration
{
  [JsonIgnore] public static string AppPath =
    Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) ?? Directory.GetCurrentDirectory();

  public string UIDataPath { get; set; } = Path.Join(AppPath, "Data", "UI");
  public string ServiceDataPath { get; set; } = Path.Join(AppPath, "Data", "Service");
}