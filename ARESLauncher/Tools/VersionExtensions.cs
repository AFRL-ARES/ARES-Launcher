using NuGet.Versioning;
using System;
using System.Collections.Generic;
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

  public static SemanticVersion GetVersionFromZipName(string fileName)
  {
    var match = AresVersionRegex.VersionRegex().Match(fileName);

    if(match.Success && Version.TryParse(match.Groups[1].Value, out var v))
    {
      return new SemanticVersion(v.Major, v.Minor, v.Build);
    }

    throw new ArgumentException($"Invalid version in filename: {fileName}");
  }
}