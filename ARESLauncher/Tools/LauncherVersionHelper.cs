using NuGet.Versioning;
using System.Reflection;

namespace ARESLauncher.Tools;

public static class LauncherVersionHelper
{
  public static string GetLauncherVersion() 
    => Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "";
}
