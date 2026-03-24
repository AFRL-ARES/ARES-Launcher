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

  Task<SemanticVersion[]> GetAvailableVersions(LauncherSource soruce);

  Task<string> Download(LauncherSource source, SemanticVersion version, string destination, string? authToken,
    IProgress<double>? progress = null);

  /// <summary>
  ///   Downloads the combined ARES release package for the requested version.
  /// </summary>
  Task<string> Download(AresSource source, SemanticVersion version, string destination, string? authToken,
    IProgress<double>? progress = null);
}
