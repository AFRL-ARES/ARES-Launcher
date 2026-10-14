using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ARESLauncher.Tools;

public static class VersionExtensions
{
  public static bool IsGreatest(this SemanticVersion version, IEnumerable<SemanticVersion> versionsToCheck)
  {
    var versions = versionsToCheck.ToArray();

    if(!versions.Any())
      return true;

    var latest = versions[0];

    for(var i = 1; i < versions.Length; i++)
      if(versions[i] > latest)
        latest = versions[i];

    return version >= latest;
  }

  public static bool IsGreatest(this SemanticVersion version, IEnumerable<ARESLauncher.Models.AresRelease> releasesToCheck)
    => version.IsGreatest(releasesToCheck.Select(r => r.Version));
  

  public static SemanticVersion GetVersionFromZipName(string fileName)
  {
    var match = AresVersionRegex.VersionRegex().Match(RemovePlatformBundleSuffix(fileName));

    if(match.Success && SemanticVersion.TryParse(match.Groups[1].Value, out var version))
      return version;

    throw new ArgumentException($"Invalid version in filename: {fileName}");
  }

  private static string RemovePlatformBundleSuffix(string fileName)
  {
    var name = Path.GetFileNameWithoutExtension(fileName);
    string[] bundleSuffixes = ["-macos-arm64", "-macos-x64", "-windows", "-linux", "-macos"];

    foreach(var suffix in bundleSuffixes)
    {
      if(name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        return name[..^suffix.Length];
    }

    return name;
  }
}
