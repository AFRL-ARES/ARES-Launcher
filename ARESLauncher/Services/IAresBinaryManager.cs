using System;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

/// <summary>
/// This service is in charge of the binaries for ARES.
/// It's responsible for managing the data inside the UI and Service directories,
/// as well as acquiring new versions when requested.
/// </summary>
public interface IAresBinaryManager
{
  /// <summary>
  /// Current version of ARES in the data directory.
  /// If null, that means there's no ARES installed.
  /// </summary>
  Version? CurrentVersion { get; }
  
  /// <summary>
  /// All available versions of ARES that the manager knows about.
  /// These are primarily the versions that can be acquired.
  /// </summary>
  Version[] AvailableVersions { get; }
  
  /// <summary>
  /// Whether the latest version of ARES is newer than the currently installed one.
  /// </summary>
  bool UpdateAvailable { get; }
  
  /// <summary>
  /// Refreshes its knowledge about the current and available versions of ARES.
  /// </summary>
  /// <returns></returns>
  Task Refresh();

  /// <summary>
  /// Sets a new path for where the UI binaries should be stored
  /// </summary>
  /// <param name="path"></param>
  void SetUiDataPath(Uri path);

  /// <summary>
  /// Sets a new path for where the service binaries should be stored
  /// </summary>
  /// <param name="uri"></param>
  void SetServiceDataPath(Uri uri);
}