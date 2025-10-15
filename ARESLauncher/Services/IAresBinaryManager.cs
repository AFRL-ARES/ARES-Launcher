using System;
using System.Threading.Tasks;
using ARESLauncher.Models;
using ARESLauncher.Models.AppSettings;
using NuGet.Versioning;

namespace ARESLauncher.Services;

/// <summary>
///   This service is in charge of the binaries for ARES.
///   It's responsible for managing the data inside the UI and Service directories,
///   as well as acquiring new versions when requested.
/// </summary>
public interface IAresBinaryManager
{
  /// <summary>
  ///   The version of the currently installed ARES in the data directory.
  ///   If null, that means there's no ARES installed.
  /// </summary>
  SemanticVersion? CurrentVersion { get; }

  /// <summary>
  ///   The appsettings for the service
  /// </summary>
  AppSettingsService? ServiceSettings { get; }

  /// <summary>
  ///   The appsettings for the UI
  /// </summary>
  AppSettingsUi? UiSettings { get; }

  /// <summary>
  ///   What's the source of the current ARES binaries.
  ///   Null if there are no binaries or source unknown.
  /// </summary>
  AresSource? CurrentSource { get; }

  /// <summary>
  ///   All available versions of ARES that the manager knows about.
  ///   These are the versions that can be acquired.
  /// </summary>
  SemanticVersion[] AvailableVersions { get; }

  /// <summary>
  ///   Whether the latest version of ARES is newer than the currently installed one.
  /// </summary>
  bool UpdateAvailable { get; }

  /// <summary>
  ///   Refreshes its knowledge about the current and available versions of ARES.
  /// </summary>
  /// <returns></returns>
  Task Refresh();

  /// <summary>
  ///   Sets a new path for where the UI binaries should be stored.
  ///   Does not move the binaries if present in current path
  /// </summary>
  /// <param name="path"></param>
  void SetUiDataPath(Uri path);

  /// <summary>
  ///   Sets a new path for where the service binaries should be stored
  ///   Does not move the binaries if present in current path
  /// </summary>
  /// <param name="uri"></param>
  void SetServiceDataPath(Uri uri);
}