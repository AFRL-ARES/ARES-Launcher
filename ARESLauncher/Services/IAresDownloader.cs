
using System;
using System.Threading.Tasks;
using ARESLauncher.Models;
using NuGet.Versioning;

namespace ARESLauncher.Services;

/// <summary>
/// This is in charge of acquiring ARES binaries from Git style repositories.
/// The interface can be implemented by any service that is capable
/// of downloading/acquiring the binaries from either GitHub or some other Git.
/// </summary>
public interface IAresDownloader
{
  Task<SemanticVersion[]> GetAvailableVersions(AresSource source, AresComponent component);
  Task Download(AresSource source, SemanticVersion version, AresComponent component, Uri destination);
  
  IObservable<DownloadStage> DownloadStage { get; }
  IObservable<double> StageProgress { get; }
  IObservable<double> TotalProgress { get; }
}
