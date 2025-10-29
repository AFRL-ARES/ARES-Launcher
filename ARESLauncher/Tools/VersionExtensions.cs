using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace ARESLauncher.Tools;
public static class VersionExtensions
{
  public static bool IsGreatest(this SemanticVersion version, IEnumerable<SemanticVersion> versionsToCheck)
  {
    var versions = versionsToCheck.ToArray();
    var latest = versions[0];
    for(var i = 1; i < versions.Length; i++)
      if(versions[i] > latest)
        latest = versions[i];
    return version > latest;
  }
}
