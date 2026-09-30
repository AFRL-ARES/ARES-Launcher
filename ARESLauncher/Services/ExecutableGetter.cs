using System.IO;
using System.Runtime.InteropServices;
using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;

namespace ARESLauncher.Services;

public class ExecutableGetter(IAppConfigurationService _configurationService) : IExecutableGetter
{
  public string? GetUiExecutablePath()
  {
    return GetPath(_configurationService.Current.UiBinaryPath, "UI");
  }

  public string? GetServiceExecutablePath()
  {
    if(_configurationService.Current.InstalledAresLayout == AresReleaseLayout.UnifiedUiOnly)
    {
      return null;
    }

    return GetPath(_configurationService.Current.ServiceBinaryPath, "AresService");
  }

  public string? GetDemoServiceExecutablePath(string serviceName)
  {
    if(string.IsNullOrWhiteSpace(serviceName))
      return null;

    var demoServicePath = Path.Combine(_configurationService.Current.UiBinaryPath, "demo", serviceName);
    return GetPath(demoServicePath, serviceName);
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
