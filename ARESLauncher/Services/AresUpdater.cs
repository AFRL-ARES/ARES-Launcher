using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ARESLauncher.Configuration;
using ARESLauncher.Models;
using ARESLauncher.Tools;
using NuGet.Versioning;

namespace ARESLauncher.Services;

public class AresUpdater : IAresUpdater
{
  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly IAppSettingsUpdater _appSettingsUpdater;
  private readonly LauncherConfiguration _configuration;
  private readonly IAresDownloader _downloader;
  private readonly ISubject<double> _updateProgressSubject = new BehaviorSubject<double>(0);
  private readonly ISubject<string> _updateStepSubject = new BehaviorSubject<string>("");

  public AresUpdater(IAresDownloader downloader, LauncherConfiguration configuration,
    IAresBinaryManager aresBinaryManager, IAppSettingsUpdater appSettingsUpdater)
  {
    _downloader = downloader;
    _configuration = configuration;
    _aresBinaryManager = aresBinaryManager;
    _appSettingsUpdater = appSettingsUpdater;

    UpdateStep = _updateStepSubject.AsObservable();
    UpdateProgress = _updateProgressSubject.AsObservable();
  }

  public Task<SemanticVersion[]> GetAvailableVersions()
  {
    var source = _configuration.DefaultAresRepo;
    return _downloader.GetAvailableVersions(source);
  }

  public async Task Update(SemanticVersion version)
  {
    if (version == _aresBinaryManager.CurrentVersion &&
        _configuration.DefaultAresRepo == _aresBinaryManager.CurrentSource)
      // We must be up to date
      return;

    var uiDir = _configuration.UiDataPath;
    var serviceDir = _configuration.ServiceDataPath;

    _updateStepSubject.OnNext("Cleaning up the previous version.");
    Directory.Delete(uiDir, true);
    Directory.Delete(serviceDir, true);

    var source = _configuration.DefaultAresRepo;
    var tempPath = Path.GetTempPath();
    _updateStepSubject.OnNext("Acquiring the UI.");
    var uiDest = await _downloader.Download(source, version, AresComponent.Ui, tempPath,
      new Progress<double>(pg => _updateProgressSubject.OnNext(pg / 2)));
    _updateStepSubject.OnNext("Acquiring the Service.");
    var serviceDest = await _downloader.Download(source, version, AresComponent.Service, tempPath,
      new Progress<double>(pg => _updateProgressSubject.OnNext(.5 + pg / 2)));

    _updateStepSubject.OnNext("Unpacking");
    await Unpacker.Unpack(uiDest, uiDir);
    await Unpacker.Unpack(serviceDest, serviceDir);

    await _aresBinaryManager.Refresh();
    _appSettingsUpdater.UpdateAll();
    var tempDir = Path.GetTempPath();
  }

  public IObservable<string> UpdateStep { get; }
  public IObservable<double> UpdateProgress { get; }
}