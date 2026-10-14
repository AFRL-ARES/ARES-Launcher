using System;
using System.Runtime.InteropServices;

namespace ARESLauncher.Tools;

public static class OsBundleNameGetter
{
  public static string GetName()
  {
    if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "windows";

    if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      return "linux";

    if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "macos";

    return "unknown";
  }

  public static string GetAresName()
  {
    if(!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return GetName();

    return RuntimeInformation.OSArchitecture switch
    {
      Architecture.X64 => "macos-x64",
      Architecture.Arm64 => "macos-arm64",
      var architecture => throw new PlatformNotSupportedException(
        $"ARES does not provide a macOS package for {architecture}.")
    };
  }
}
