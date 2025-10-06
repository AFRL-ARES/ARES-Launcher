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
  Task<Version[]> GetAvailableVersions(AresComponent component);
  Task Download(Version version, AresComponent component, Uri destination);
}