using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ARESLauncher.Models;
using ARESLauncher.Models.AppSettings;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace ARESLauncher.Services;

public class AresBinaryManager : IAresBinaryManager
{
  private readonly IAppConfigurationService _configurationService;
  private readonly IAresDownloader _downloader;
  private readonly IExecutableGetter _executableGetter;
  private readonly ILogger<AresBinaryManager> _logger;

  private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

  public AresBinaryManager(
    IAppConfigurationService configurationService,
    IAresDownloader downloader,
    IExecutableGetter executableGetter,
    ILogger<AresBinaryManager> logger)
  {
    _configurationService = configurationService;
    _downloader = downloader;
    _executableGetter = executableGetter;
    _logger = logger;
  }

  public SemanticVersion? CurrentVersion { get; private set; }
  public AppSettingsService? ServiceSettings { get; private set; }
  public AppSettingsUi? UiSettings { get; private set; }
  public AresSource? CurrentSource { get; private set; }
  public SemanticVersion[] AvailableVersions { get; private set; } = [];

  public bool UpdateAvailable
  {
    get
    {
      if(AvailableVersions.Length == 0) return false;
      if(CurrentVersion is null) return true;
      var latest = GetLatestVersion(AvailableVersions);
      return latest > CurrentVersion;
    }
  }

  public async Task Refresh()
  {
    // Load appsettings (if present)
    UiSettings = TryLoadAppSettings<AppSettingsUi>(Path.Combine(_configurationService.Current.UiBinaryPath, "appsettings.json"));
    ServiceSettings = TryLoadAppSettings<AppSettingsService>(Path.Combine(_configurationService.Current.ServiceBinaryPath, "appsettings.json"));

    if(_logger.IsEnabled(LogLevel.Debug))
    {
      if(UiSettings is null)
      {
        _logger.LogDebug("Ui settings not found in {}", _configurationService.Current.UiBinaryPath);
      }
      if(ServiceSettings is null)
      {
        _logger.LogDebug("Service settings not found in {}", _configurationService.Current.ServiceBinaryPath);
      }
    }

    var uiMetadata = BinaryMetadataHelper.ReadMetadata(_configurationService.Current.UiBinaryPath);
    var serviceMetadata = BinaryMetadataHelper.ReadMetadata(_configurationService.Current.ServiceBinaryPath);
    var metadata = uiMetadata ?? serviceMetadata;

    if(uiMetadata is not null && serviceMetadata is not null)
    {
      var versionsMatch = string.Equals(uiMetadata.Version, serviceMetadata.Version, StringComparison.OrdinalIgnoreCase);
      var sourcesMatch = Equals(uiMetadata.Source, serviceMetadata.Source);
      if(!versionsMatch || !sourcesMatch)
        _logger.LogWarning("Binary metadata mismatch between UI and Service directories.");
    }

    var versionFromMetadata = TryParseVersion(metadata?.Version);

    if(versionFromMetadata is null)
    {
      _logger.LogWarning("Unable to find the version metadata file. Gonna try assembly version.");
    }

    // Determine installed version by inspecting one of the binaries (prefer UI, then service) if metadata missing.
    // We technically wouldn't really use the file/assembly info to set the version, but AI thought it was a good
    // idea and it doesn't hurt to have a fallback in case the version was not populated via the metadata.
    CurrentVersion = versionFromMetadata ?? TryGetInstalledVersion();

    // Prefer source from metadata, otherwise fall back to default source when version is known.
    CurrentSource = metadata?.Source ?? (CurrentVersion is not null ? _configurationService.Current.CurrentAresRepo : null);

    // Query available versions from the default source
    try
    {
      AvailableVersions = await _downloader.GetAvailableVersions(_configurationService.Current.CurrentAresRepo);
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Failed to query available ARES versions.");
      AvailableVersions = [];
    }
  }

  public void SetUiDataPath(Uri path)
  {
    var newPath = ToLocalPath(path);
    _configurationService.Update(cfg => cfg.UiBinaryPath = newPath);
  }

  public void SetServiceDataPath(Uri uri)
  {
    var newPath = ToLocalPath(uri);
    _configurationService.Update(cfg => cfg.ServiceBinaryPath = newPath);
  }

  private SemanticVersion? TryGetInstalledVersion()
  {
    string? uiPath = _executableGetter.GetUiExecutablePath();
    string? servicePath = _executableGetter.GetServiceExecutablePath();

    string?[] candidates = [ uiPath, servicePath ];

    // 1) Try to read managed assembly version from the given paths or sibling .dll files
    foreach (var candidate in candidates)
    {
      if (string.IsNullOrWhiteSpace(candidate)) continue;

      // Try exact path first
      if (File.Exists(candidate))
      {
        var semver = TryGetVersionFromAssembly(candidate) ?? TryGetVersionFromFileInfo(candidate);
        if (semver is not null) return semver;
      }

      // If no extension (common on Linux/macOS), try sibling .dll (framework-dependent publish)
      if (Path.GetExtension(candidate) == string.Empty)
      {
        var dllCandidate = candidate + ".dll";
        if (File.Exists(dllCandidate))
        {
          var semver = TryGetVersionFromAssembly(dllCandidate);
          if (semver is not null) return semver;
        }
      }
    }

    return null;
  }

  private static SemanticVersion? TryGetVersionFromAssembly(string path)
  {
    try
    {
      var asmName = AssemblyName.GetAssemblyName(path);
      var v = asmName.Version;
      if(v is null) return null;
      // Convert System.Version to SemanticVersion (drop revision if -1)
      return new SemanticVersion(v.Major, v.Minor, Math.Max(0, v.Build));
    }
    catch
    {
      return null;
    }
  }

  private static SemanticVersion? TryGetVersionFromFileInfo(string path)
  {
    try
    {
      var info = FileVersionInfo.GetVersionInfo(path);
      var versionString = info.ProductVersion ?? info.FileVersion;
      if(string.IsNullOrWhiteSpace(versionString)) return null;

      // Trim possible metadata/suffixes not compatible with SemanticVersion parser
      versionString = versionString.Trim();
      var spaceIdx = versionString.IndexOf(' ');
      if(spaceIdx > 0) versionString = versionString[..spaceIdx];

      return SemanticVersion.TryParse(versionString, out var semver) ? semver : null;
    }
    catch
    {
      return null;
    }
  }

  private static T? TryLoadAppSettings<T>(string path) where T : AppSettingsBase
  {
    if(!File.Exists(path)) return null;
    try
    {
      var json = File.ReadAllText(path);
      if(string.IsNullOrWhiteSpace(json)) return null;
      return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }
    catch
    {
      return null;
    }
  }

  private static string ToLocalPath(Uri uri)
  {
    if(uri.IsAbsoluteUri)
    {
      if(uri.IsFile) return uri.LocalPath;
      // For non-file URIs, best effort: use absolute URI string
      return uri.AbsoluteUri;
    }

    // Relative or opaque: fall back to original string
    return uri.ToString();
  }

  private static SemanticVersion GetLatestVersion(SemanticVersion[] versions)
  {
    var latest = versions[0];
    for(var i = 1; i < versions.Length; i++)
    {
      if(versions[i] > latest) latest = versions[i];
    }
    return latest;
  }

  private static SemanticVersion? TryParseVersion(string? versionString)
  {
    if(string.IsNullOrWhiteSpace(versionString)) return null;
    return SemanticVersion.TryParse(versionString, out var version) ? version : null;
  }
}
