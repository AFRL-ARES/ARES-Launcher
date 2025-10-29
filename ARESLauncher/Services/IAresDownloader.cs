using System;
using System.Threading.Tasks;
using ARESLauncher.Models;
using NuGet.Versioning;

namespace ARESLauncher.Services;

/// <summary>
///   This is in charge of acquiring ARES releases from Git style repositories.
///   The interface can be implemented by any service that is capable
///   of downloading/acquiring the binaries from either GitHub or some other Git.
/// </summary>
public interface IAresDownloader
{
  Task<SemanticVersion[]> GetAvailableVersions(AresSource source, string? authToken);

  /// <summary>
  /// </summary>
  /// <param name="source"></param>
  /// <param name="version"></param>
  /// <param name="component"></param>
  /// <param name="destination"></param>
  /// <param name="progress"></param>
  /// <returns>The file path of the newly downloaded item</returns>
  Task<string> Download(AresSource source, SemanticVersion version, AresComponent component, string destination, string? authToken,
    IProgress<double>? progress = null);
}