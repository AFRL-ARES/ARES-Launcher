using System.IO;
using System.Runtime.InteropServices;
using ARESLauncher.Configuration;

namespace ARESLauncher.Services;

public class ExecutableGetter(LauncherConfiguration _configuration) : IExecutableGetter
{
  public string? GetUiExecutablePath()
  {
    return GetPath(_configuration.UiDataPath, "UI");
  }

  public string? GetServiceExecutablePath()
  {
    return GetPath(_configuration.ServiceDataPath, "AresService");
  }

  private string? GetPath(string dataPath, string name)
  {
    if (string.IsNullOrEmpty(dataPath))
      return null;
    
    var executablePath = Path.Combine(dataPath, name);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      executablePath = Path.ChangeExtension(executablePath, "exe");
    }

    return executablePath;
  }
}