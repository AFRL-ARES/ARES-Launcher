using NuGet.Versioning;
using System;
using System.IO;

namespace ARESLauncher.Tools;

public static class AresReleaseArchive
{
  public static bool TryGetVersion(string archivePath, out SemanticVersion? version)
  {
    version = null;
    var fileName = Path.GetFileName(archivePath);

    if(!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !IsCurrentPlatformBundle(fileName))
      return false;

    try
    {
      version = VersionExtensions.GetVersionFromZipName(fileName);
      return true;
    }
    catch(ArgumentException)
    {
      return false;
    }
  }

  private static bool IsCurrentPlatformBundle(string fileName)
  {
    var bundleName = OsBundleNameGetter.GetAresName();
    if(IsBundleAsset(fileName, bundleName))
      return true;

    return OperatingSystem.IsMacOS() && bundleName.StartsWith("macos-", StringComparison.Ordinal) &&
           IsBundleAsset(fileName, "macos");
  }

  private static bool IsBundleAsset(string fileName, string bundleName)
  {
    return fileName.EndsWith($"-{bundleName}.zip", StringComparison.OrdinalIgnoreCase) ||
           fileName.Equals($"{bundleName}.zip", StringComparison.OrdinalIgnoreCase);
  }
}
