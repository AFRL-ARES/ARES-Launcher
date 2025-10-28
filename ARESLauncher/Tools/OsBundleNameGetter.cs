using System.Runtime.InteropServices;

namespace ARESLauncher.Tools;

public static class OsBundleNameGetter
{
  public static string GetName()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "windows";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      return "linux";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "macos";

    return "unknown";
  }
}