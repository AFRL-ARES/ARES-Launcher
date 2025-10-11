using System;
using System.Threading.Tasks;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

/// <summary>
/// This is in charge of acquiring ARES binaries.
/// The interface can be implemented by any service that is capable
/// of downloading/acquiring the binaries from either GitHub or some other location.
/// </summary>
public interface IAresDownloader
{
  Task<Version[]> GetAvailableVersions(AresSource source, AresComponent component);
  Task Download(AresSource source, Version version, AresComponent component, Uri destination);
  
  IObservable<DownloadStage> DownloadStage { get; }
  IObservable<double> StageProgress { get; }
  IObservable<double> TotalProgress { get; }
}