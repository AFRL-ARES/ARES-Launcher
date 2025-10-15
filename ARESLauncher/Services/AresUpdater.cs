using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace ARESLauncher.Services;

public class AresUpdater : IAresUpdater
{
  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly IAppSettingsUpdater _appSettingsUpdater;
  private readonly ICertificateManager _certificateManager;
  private readonly ILogger<AresUpdater> _logger;
  private readonly IAppConfigurationService _configurationService;
  private readonly IAresDownloader _downloader;
  private readonly ISubject<double> _updateProgressSubject = new BehaviorSubject<double>(0);
  private readonly ISubject<string> _updateStepSubject = new BehaviorSubject<string>("");

  public AresUpdater(IAresDownloader downloader, IAppConfigurationService configurationService,
    IAresBinaryManager aresBinaryManager, IAppSettingsUpdater appSettingsUpdater, ICertificateManager certificateManager, ILogger<AresUpdater> logger)
  {
    _downloader = downloader;
    _configurationService = configurationService;
    _aresBinaryManager = aresBinaryManager;
    _appSettingsUpdater = appSettingsUpdater;
    _certificateManager = certificateManager;
    _logger = logger;
    UpdateStep = _updateStepSubject.AsObservable();
    UpdateProgress = _updateProgressSubject.AsObservable();
  }

  public Task<SemanticVersion[]> GetAvailableVersions()
  {
    var source = _configurationService.Current.CurrentAresRepo;
    return _downloader.GetAvailableVersions(source);
  }

  public async Task Update(SemanticVersion version)
  {
    if(version == _aresBinaryManager.CurrentVersion &&
        _configurationService.Current.CurrentAresRepo == _aresBinaryManager.CurrentSource)
      // We must be up to date
      return;

    var uiDir = _configurationService.Current.UiBinaryPath;
    var serviceDir = _configurationService.Current.ServiceBinaryPath;

    _updateStepSubject.OnNext("Cleaning up the previous version.");
    Directory.Delete(uiDir, true);
    Directory.Delete(serviceDir, true);

    var source = _configurationService.Current.CurrentAresRepo;
    var tempPath = Path.GetTempPath();
    try
    {
      _updateStepSubject.OnNext("Downloading the UI.");
      var uiDest = await _downloader.Download(source, version, AresComponent.Ui, tempPath,
        new Progress<double>(pg => _updateProgressSubject.OnNext(pg / 2)));
      _updateStepSubject.OnNext("Unpacking the UI");
      await Unpacker.Unpack(uiDest, uiDir);
      BinaryMetadataHelper.WriteMetadata(uiDir, source, version);
    }
    catch (InvalidOperationException e)
    {
      _logger.LogError("Failed to acquire the UI. {}", e);
      throw;
    }

    try
    {
      _updateStepSubject.OnNext("Acquiring the Service.");
      var serviceDest = await _downloader.Download(source, version, AresComponent.Service, tempPath,
        new Progress<double>(pg => _updateProgressSubject.OnNext(.5 + pg / 2)));

      _updateStepSubject.OnNext("Unpacking the Service.");
      await Unpacker.Unpack(serviceDest, serviceDir);
      BinaryMetadataHelper.WriteMetadata(serviceDir, source, version);
    }
    catch (InvalidOperationException e)
    {
      _logger.LogError("Failed to acquire the Service. {}", e);
      throw;
    }

    await _aresBinaryManager.Refresh();
    _appSettingsUpdater.UpdateAll();
    await _certificateManager.Update();
  }

  public IObservable<string> UpdateStep { get; }
  public IObservable<double> UpdateProgress { get; }
}
