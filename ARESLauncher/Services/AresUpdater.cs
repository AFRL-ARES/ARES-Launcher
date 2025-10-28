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
  private readonly IAppSettingsUpdater _appSettingsUpdater;
  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly ICertificateManager _certificateManager;
  private readonly IAppConfigurationService _configurationService;
  private readonly IAresDownloader _downloader;
  private readonly ILogger<AresUpdater> _logger;
  private readonly ISubject<double> _updateProgressSubject = new BehaviorSubject<double>(0);
  private readonly ISubject<string> _updateStepSubject = new BehaviorSubject<string>("");

  public AresUpdater(IAresDownloader downloader, IAppConfigurationService configurationService,
    IAresBinaryManager aresBinaryManager, IAppSettingsUpdater appSettingsUpdater,
    ICertificateManager certificateManager, ILogger<AresUpdater> logger)
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
    if (version == _aresBinaryManager.CurrentVersion &&
        _configurationService.Current.CurrentAresRepo == _aresBinaryManager.CurrentSource)
      // We must be up to date
      return;

    var uiDir = _configurationService.Current.UiBinaryPath;
    var serviceDir = _configurationService.Current.ServiceBinaryPath;
    var rootDir = _configurationService.Current.BinariesRoot;

    _updateStepSubject.OnNext("Cleaning up the previous version.");

    if (Directory.Exists(uiDir))
      Directory.Delete(uiDir, true);
    if (Directory.Exists(serviceDir))
      Directory.Delete(serviceDir, true);
    if (Directory.Exists(rootDir))
      Directory.Delete(rootDir, true);

    var source = _configurationService.Current.CurrentAresRepo;

    if (source.Bundle)
      await DownloadBundle(source, version, rootDir);
    else
      await DownloadIndividualComponents(source, version, uiDir, serviceDir);

    await _aresBinaryManager.Refresh();
    _appSettingsUpdater.UpdateAll();
    await _certificateManager.Update();
  }

  public IObservable<string> UpdateStep { get; }
  public IObservable<double> UpdateProgress { get; }

  private async Task DownloadBundle(AresSource source, SemanticVersion version, string dest)
  {
    var tempPath = Path.GetTempPath();
    try
    {
      _updateStepSubject.OnNext("Downloading the bundle.");
      var bundleDest = await _downloader.Download(source, version, AresComponent.Both, tempPath,
        new Progress<double>(pg => _updateProgressSubject.OnNext(pg)));
      _updateStepSubject.OnNext("Unpacking the bundle");
      await Unpacker.Unpack(bundleDest, dest);
      BinaryMetadataHelper.WriteMetadata(dest, source, version);
    }
    catch (InvalidOperationException e)
    {
      _logger.LogError("Failed to acquire the combined bundle. {}", e);
      throw;
    }
  }

  private async Task DownloadIndividualComponents(AresSource source, SemanticVersion version, string uiDir,
    string serviceDir)
  {
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
  }
}