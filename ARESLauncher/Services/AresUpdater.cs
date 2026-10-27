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

  public async Task<AresRelease[]> GetAvailableVersions()
  {
    var source = _configurationService.Current.CurrentAresRepo;
    var remoteReleases = await _downloader.GetAvailableVersions(source, _configurationService.Current.GitToken);
    var localDebugVersions = GetLocalDebugVersions();

    var allReleases = remoteReleases.Union(localDebugVersions.Select(v => new AresRelease { Version = v, IsBeta = false }))
      .OrderByDescending(r => r.Version);

    if(!_configurationService.Current.IncludeBeta)
    {
      return allReleases.Where(r => !r.IsBeta).ToArray();
    }

    return allReleases.ToArray();
  }

  private SemanticVersion[] GetLocalDebugVersions()
  {
    var debugPath = Path.Combine(AppContext.BaseDirectory, "Debug");
    if(!Directory.Exists(debugPath))
      return Array.Empty<SemanticVersion>();

    return Directory.EnumerateFiles(debugPath, "*.zip")
      .Select(f =>
      {
        try
        {
          return Tools.VersionExtensions.GetVersionFromZipName(Path.GetFileName(f));
        }
        catch(Exception)
        {
          return null;
        }
      })
      .Where(v => v is not null)
      .Cast<SemanticVersion>()
      .ToArray();
  }

  public async Task InstallOffline(string path)
  {
    _updateStepDescriptionSubject.OnNext("Unpacking the package");
    var dest = _configurationService.Current.UiBinaryPath;
    var source = _configurationService.Current.CurrentAresRepo;
    var version = Tools.VersionExtensions.GetVersionFromZipName(path);  

    await Unpacker.Unpack(path, dest);
    BinaryMetadataHelper.WriteMetadata(dest, source, version);

    await ApplyLocalSettings();
  }

  public async Task Update(SemanticVersion version)
  {
    var uiDir = _configurationService.Current.UiBinaryPath;
    _logger.LogInformation("Cleaning Up Previous ARES Installs...");
    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    _updateStepDescriptionSubject.OnNext("Cleaning up the previous version.");

    if(Directory.Exists(uiDir))
      Directory.Delete(uiDir, true);

    var source = _configurationService.Current.CurrentAresRepo;

    _currentUpdateStepSubject.OnNext(UpdateStep.Downloading);
    try
    {
      await DownloadPackage(source, version, uiDir);
    }

    catch(Exception ex)
    {
      _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
      _updateStepDescriptionSubject.OnNext("");
      _logger.LogError(ex, "Encountered an error while downloading ARES");
      throw;
    }

    _logger.LogInformation("Applying local settings to users ARES system");
    await ApplyLocalSettings();
  }

  public AresRelease? GetManualUpdatePackage()
  {
    var offlinePath = Path.Combine(AppContext.BaseDirectory, "Offline");
    if(!Directory.Exists(offlinePath))
      return null;

    return Directory.EnumerateFiles(offlinePath, "*.zip")
      .Select(path => new { Path = path, IsValid = AresReleaseArchive.TryGetVersion(path, out var version), Version = version })
      .Where(package => package.IsValid && package.Version is not null)
      .OrderByDescending(package => package.Version)
      .Select(package => new AresRelease { Version = package.Version!, IsBeta = false })
      .FirstOrDefault();
  }

  public async Task UpdateFromManualPackage()
  {
    var offlinePath = Path.Combine(AppContext.BaseDirectory, "Offline");
    _logger.LogInformation("Determined offline install available, path to install is {path}", offlinePath);

    var package = Directory.Exists(offlinePath)
      ? Directory.EnumerateFiles(offlinePath, "*.zip")
        .Select(path => new { Path = path, IsValid = AresReleaseArchive.TryGetVersion(path, out var version), Version = version })
        .Where(candidate => candidate.IsValid && candidate.Version is not null)
        .OrderByDescending(candidate => candidate.Version)
        .FirstOrDefault() : null;

    if(package is null)
    {
      _logger.LogError("No compatible ARES package was found in the Offline folder.");
      throw new InvalidOperationException("No compatible ARES package was found in the Offline folder.");
    }
      
    var uiDir = _configurationService.Current.UiBinaryPath;
    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    _updateStepDescriptionSubject.OnNext("Cleaning up the previous version.");

    if(Directory.Exists(uiDir))
      Directory.Delete(uiDir, true);

    try
    {
      _updateStepDescriptionSubject.OnNext("Unpacking the local package.");
      _logger.LogInformation("Unpacking local ARES package");
      _updateProgressSubject.OnNext(1.0);
      await Unpacker.Unpack(package.Path, uiDir);
      BinaryMetadataHelper.WriteMetadata(uiDir, _configurationService.Current.CurrentAresRepo, package.Version!);
    }
    catch(Exception ex)
    {
      _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
      _updateStepDescriptionSubject.OnNext("");
      _logger.LogError(ex, "Encoutnered error trying to install local ARES package");
      throw;
    }

    await ApplyLocalSettings();
  }

  public async Task ApplyLocalSettings()
  {
    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    _updateStepDescriptionSubject.OnNext("Updating settings");
    _logger.LogInformation("Updating Local ARES Settings");
    _appSettingsUpdater.UpdateAresSettings();
    
    _updateStepDescriptionSubject.OnNext("Updating certificates");
    _logger.LogInformation("Updating Local ARES Certificates");
    await _certificateManager.Update();

    _updateStepDescriptionSubject.OnNext("Ensuring database is up to date");
    _logger.LogInformation("Checking Database Status");

    await _databaseManager.Refresh();
    if(_databaseManager.DatabaseStatus != DatabaseStatus.UpToDate)
    {
      _logger.LogInformation("Determined Database is Out of Date, Updating the Database");
      await _databaseManager.RunMigrations();
    }

    _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
    _updateStepDescriptionSubject.OnNext("");
  }

  public async Task UpdateLatest()
  {
    //No ARES installed, check for offline package
    if(_binaryManager.CurrentVersion is null)
    {
      _updateStepDescriptionSubject.OnNext("Checking for offline package.");
      _currentUpdateStepSubject.OnNext(UpdateStep.Other);
      var package = GetManualUpdatePackage();
      if(package is not null)
      {
        _updateStepDescriptionSubject.OnNext("Offline installation found, preparing to install.");
        await UpdateFromManualPackage();
        return;
      }

      _updateStepDescriptionSubject.OnNext("No Offline Install Found");
    }

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

    var latest = versions.FirstOrDefault();
    if(latest is null)
    {
      _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
      _updateStepDescriptionSubject.OnNext("");
      throw new InvalidOperationException("No ARES versions found. Ensure the right repository is selected and/or your Git token is correct.");
    }

    await Update(latest.Version);
  }

  public async Task CreateSnapshot(SemanticVersion version)
  {
    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    _updateStepDescriptionSubject.OnNext("Creating database snapshot");
    try
    {
      await _databaseManager.CreateSnapshot(version);
    }
    finally
    {
      _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
      _updateStepDescriptionSubject.OnNext("");
    }
  }

  public Task<bool> HasSnapshot(SemanticVersion version)
  {
    return _databaseManager.HasSnapshot(version);
  }

  public async Task RestoreSnapshot(SemanticVersion version)
  {
    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    _updateStepDescriptionSubject.OnNext("Restoring database snapshot");
    try
    {
      await _databaseManager.RestoreSnapshot(version);
    }
    finally
    {
      _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
      _updateStepDescriptionSubject.OnNext("");
    }
  }

  public async Task ResetDatabase()
  {
    _currentUpdateStepSubject.OnNext(UpdateStep.Other);
    _updateStepDescriptionSubject.OnNext("Resetting database");
    try
    {
      await _databaseManager.Reset();
    }
    finally
    {
      _currentUpdateStepSubject.OnNext(UpdateStep.Idle);
      _updateStepDescriptionSubject.OnNext("");
    }
  }

  public void InvalidateCache()
  {
    _downloader.InvalidateCache();
  }

  public IObservable<string> UpdateStepDescription { get; }
  public IObservable<double> UpdateProgress { get; }
  public IObservable<UpdateStep> CurrentUpdateStep { get; }

  private async Task DownloadPackage(AresSource source, SemanticVersion version, string dest)
  {
    var tempPath = Path.GetTempPath();
    try
    {
      string packageDest;
      var debugPath = Path.Combine(AppContext.BaseDirectory, "Debug");
      var localPackage = Directory.Exists(debugPath)
        ? Directory.EnumerateFiles(debugPath, "*.zip")
          .FirstOrDefault(f =>
          {
            try
            {
              return version.Equals(Tools.VersionExtensions.GetVersionFromZipName(Path.GetFileName(f)));
            }
            catch(Exception)
            {
              return false;
            }
          })
        : null;

      if(localPackage != null)
      {
        _logger.LogInformation("Using local debug package for version {Version}: {Path}", version, localPackage);
        _updateStepDescriptionSubject.OnNext("Using local debug package.");
        _updateProgressSubject.OnNext(1.0);
        packageDest = localPackage;
      }
      else
      {
        _logger.LogInformation("Downloading the ARES Pacakge for Install");
        _updateStepDescriptionSubject.OnNext("Downloading the package.");
        packageDest = await _downloader.Download(source, version, tempPath, _configurationService.Current.GitToken,
          new Progress<double>(pg => _updateProgressSubject.OnNext(pg)));
      }

      _logger.LogInformation("Unpackaging ARES...");
      _updateStepDescriptionSubject.OnNext("Unpacking the package");
      await Unpacker.Unpack(packageDest, dest);
      _logger.LogInformation("Writing Binary Metadata to User System");
      BinaryMetadataHelper.WriteMetadata(dest, source, version);
    }
    catch(Exception e)
    {
      _logger.LogError("Failed to acquire the ARES package. {}", e);
      throw;
    }
  }
}