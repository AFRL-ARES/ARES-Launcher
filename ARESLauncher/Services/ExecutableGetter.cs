using System.IO;
using System.Runtime.InteropServices;
using ARESLauncher.Services.Configuration;

namespace ARESLauncher.Services;

public class ExecutableGetter(IAppConfigurationService _configurationService) : IExecutableGetter
{
  public string? GetUiExecutablePath()
  {
    return GetPath(_configurationService.Current.UiDataPath, "UI");
  }

  public string? GetServiceExecutablePath()
  {
    return GetPath(_configurationService.Current.ServiceDataPath, "AresService");
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
