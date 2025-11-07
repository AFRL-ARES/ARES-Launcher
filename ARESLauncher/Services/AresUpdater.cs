using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public class AresUpdater : IAresUpdater
{
  private readonly IAppSettingsUpdater _appSettingsUpdater;
  private readonly ICertificateManager _certificateManager;
  private readonly IDatabaseManager _databaseManager;
  private readonly IAresBinaryManager _binaryManager;
  private readonly IAppConfigurationService _configurationService;
  private readonly IAresDownloader _downloader;
  private readonly ILogger<AresUpdater> _logger;
  private readonly BehaviorSubject<double> _updateProgressSubject = new(0);
  private readonly BehaviorSubject<string> _updateStepDescriptionSubject = new("");
  private readonly BehaviorSubject<UpdateStep> _currentUpdateStepSubject = new(UpdateStep.Idle);

  public AresUpdater(IAresDownloader downloader,
    IAppConfigurationService configurationService,
    IAppSettingsUpdater appSettingsUpdater,
    ICertificateManager certificateManager,
    IDatabaseManager databaseManager,
    IAresBinaryManager binaryManager,
    ILogger<AresUpdater> logger)
  {
    _downloader = downloader;
    _configurationService = configurationService;
    _appSettingsUpdater = appSettingsUpdater;
    _certificateManager = certificateManager;
    _databaseManager = databaseManager;
    _binaryManager = binaryManager;
    _logger = logger;
    UpdateStepDescription = _updateStepDescriptionSubject.AsObservable();
    UpdateProgress = _updateProgressSubject.AsObservable();
    CurrentUpdateStep = _currentUpdateStepSubject.AsObservable();
  }

  public Task<SemanticVersion[]> GetAvailableVersions()
  {
    var source = _configurationService.Current.CurrentAresRepo;
    return _downloader.GetAvailableVersions(source, _configurationService.Current.GitToken);
  }

  public async Task Update(SemanticVersion version)
  {
    var uiDir = _configurationService.Current.UiBinaryPath;
    var serviceDir = _configurationService.Current.ServiceBinaryPath;
    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    _updateStepDescriptionSubject.OnNext("Cleaning up the previous version.");

    if(Directory.Exists(uiDir))
      Directory.Delete(uiDir, true);
    if(Directory.Exists(serviceDir))
      Directory.Delete(serviceDir, true);

    var source = _configurationService.Current.CurrentAresRepo;

    _currentUpdateStepSubject.OnNext(UpdateStep.Downloading);
    try
    {
      if(source.Bundle)
        await DownloadBundle(source, version, uiDir);
      else
        await DownloadIndividualComponents(source, version, uiDir, serviceDir);
    }
    catch(Exception)
    {
      _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
      _updateStepDescriptionSubject.OnNext("");
      throw;
    }

    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    _updateStepDescriptionSubject.OnNext("Updating settings");
    _appSettingsUpdater.UpdateAll();
    _updateStepDescriptionSubject.OnNext("Updating certificates");
    await _certificateManager.Update();
    _updateStepDescriptionSubject.OnNext("Ensuring database is up to date");
    await _databaseManager.Refresh();
    if(_databaseManager.DatabaseStatus != DatabaseStatus.UpToDate)
    {
      await _databaseManager.RunMigrations();
    }
    _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
    _updateStepDescriptionSubject.OnNext("");
  }

  public async Task UpdateLatest()
  {
    _updateStepDescriptionSubject.OnNext("Checking version");
    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    var versions = await GetAvailableVersions();
    var currentVersion = _binaryManager.CurrentVersion;
    if(currentVersion?.IsGreatest(versions) ?? false)
    {
      _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
      _updateStepDescriptionSubject.OnNext("Already on the latest version.");
      return;
    }

    var latest = versions.OrderDescending().FirstOrDefault() ?? throw new InvalidOperationException("No versions found for ARES");

    await Update(latest);
  }

  public IObservable<string> UpdateStepDescription { get; }
  public IObservable<double> UpdateProgress { get; }
  public IObservable<UpdateStep> CurrentUpdateStep { get; }

  private async Task DownloadBundle(AresSource source, SemanticVersion version, string dest)
  {
    var tempPath = Path.GetTempPath();
    try
    {
      _updateStepDescriptionSubject.OnNext("Downloading the bundle.");
      var bundleDest = await _downloader.Download(source, version, AresComponent.Both, tempPath, _configurationService.Current.GitToken,
        new Progress<double>(pg => _updateProgressSubject.OnNext(pg)));
      _updateStepDescriptionSubject.OnNext("Unpacking the bundle");
      await Unpacker.Unpack(bundleDest, dest);
      BinaryMetadataHelper.WriteMetadata(dest, source, version);
    }
    catch(Exception e)
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
      _updateStepDescriptionSubject.OnNext("Downloading the UI.");
      var uiDest = await _downloader.Download(source, version, AresComponent.Ui, tempPath, _configurationService.Current.GitToken,
        new Progress<double>(pg => _updateProgressSubject.OnNext(pg / 2)));
      _updateStepDescriptionSubject.OnNext("Unpacking the UI");
      await Unpacker.Unpack(uiDest, uiDir);
      BinaryMetadataHelper.WriteMetadata(uiDir, source, version);
    }
    catch(Exception e)
    {
      _logger.LogError("Failed to acquire the UI. {}", e);
      throw;
    }

    try
    {
      _updateStepDescriptionSubject.OnNext("Acquiring the Service.");
      var serviceDest = await _downloader.Download(source, version, AresComponent.Service, tempPath, _configurationService.Current.GitToken,
        new Progress<double>(pg => _updateProgressSubject.OnNext(.5 + pg / 2)));

      _updateStepDescriptionSubject.OnNext("Unpacking the Service.");
      await Unpacker.Unpack(serviceDest, serviceDir);
      BinaryMetadataHelper.WriteMetadata(serviceDir, source, version);
    }
    catch(Exception e)
    {
      _logger.LogError("Failed to acquire the Service. {}", e);
      throw;
    }
  }
}