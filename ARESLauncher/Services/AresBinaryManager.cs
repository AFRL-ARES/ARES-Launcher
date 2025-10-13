using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Models;
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

    AvailableVersions = [];
  }

  public SemanticVersion? CurrentVersion { get; private set; }
  public AppSettings? ServiceSettings { get; private set; }
  public AppSettings? UiSettings { get; private set; }
  public AresSource? CurrentSource { get; private set; }
  public SemanticVersion[] AvailableVersions { get; private set; }

  public bool UpdateAvailable
  {
    get
    {
      if (AvailableVersions.Length == 0) return false;
      if (CurrentVersion is null) return true;
      var latest = GetLatestVersion(AvailableVersions);
      return latest > CurrentVersion;
    }
  }

  public async Task Refresh()
  {
    // Load appsettings (if present)
    UiSettings = TryLoadAppSettings(Path.Combine(_configurationService.Current.UiDataPath, "appsettings.json"));
    ServiceSettings = TryLoadAppSettings(Path.Combine(_configurationService.Current.ServiceDataPath, "appsettings.json"));

    var uiMetadata = BinaryMetadataHelper.ReadMetadata(_configurationService.Current.UiDataPath);
    var serviceMetadata = BinaryMetadataHelper.ReadMetadata(_configurationService.Current.ServiceDataPath);
    var metadata = uiMetadata ?? serviceMetadata;

    if (uiMetadata is not null && serviceMetadata is not null)
    {
      var versionsMatch = string.Equals(uiMetadata.Version, serviceMetadata.Version, StringComparison.OrdinalIgnoreCase);
      var sourcesMatch = Equals(uiMetadata.Source, serviceMetadata.Source);
      if (!versionsMatch || !sourcesMatch)
        _logger.LogWarning("Binary metadata mismatch between UI and Service directories.");
    }

    var versionFromMetadata = TryParseVersion(metadata?.Version);

    // Determine installed version by inspecting one of the binaries (prefer UI, then service) if metadata missing.
    // We technically wouldn't really use the file/assembly info to set the version, but AI thought it was a good
    // idea and it doesn't hurt to have a fallback in case the version was not populated via the metadata.
    CurrentVersion = versionFromMetadata ?? TryGetInstalledVersion();

    // Prefer source from metadata, otherwise fall back to default source when version is known.
    CurrentSource = metadata?.Source ?? (CurrentVersion is not null ? _configurationService.Current.DefaultAresRepo : null);

    // Query available versions from the default source
    try
    {
      AvailableVersions = await _downloader.GetAvailableVersions(_configurationService.Current.DefaultAresRepo);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to query available ARES versions.");
      AvailableVersions = Array.Empty<SemanticVersion>();
    }
  }

  public void SetUiDataPath(Uri path)
  {
    var newPath = ToLocalPath(path);
    _configurationService.Update(cfg => cfg.UiDataPath = newPath);
  }

  public void SetServiceDataPath(Uri uri)
  {
    var newPath = ToLocalPath(uri);
    _configurationService.Update(cfg => cfg.ServiceDataPath = newPath);
  }

  private SemanticVersion? TryGetInstalledVersion()
  {
    string?[] candidates =
    [
      _executableGetter.GetUiExecutablePath(),
      _executableGetter.GetServiceExecutablePath()
    ];

    foreach (var candidate in candidates)
    {
      if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)) continue;

      // Prefer managed assembly version if available
      var semver = TryGetVersionFromAssembly(candidate) ?? TryGetVersionFromFileInfo(candidate);
      if (semver is not null) return semver;
    }

    return null;
  }

  private static SemanticVersion? TryGetVersionFromAssembly(string path)
  {
    try
    {
      var asmName = AssemblyName.GetAssemblyName(path);
      var v = asmName.Version;
      if (v is null) return null;
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
      if (string.IsNullOrWhiteSpace(versionString)) return null;

      // Trim possible metadata/suffixes not compatible with SemanticVersion parser
      versionString = versionString.Trim();
      var spaceIdx = versionString.IndexOf(' ');
      if (spaceIdx > 0) versionString = versionString[..spaceIdx];

      return SemanticVersion.TryParse(versionString, out var semver) ? semver : null;
    }
    catch
    {
      return null;
    }
  }

  private static AppSettings? TryLoadAppSettings(string path)
  {
    if (!File.Exists(path)) return null;
    try
    {
      var json = File.ReadAllText(path);
      if (string.IsNullOrWhiteSpace(json)) return null;
      return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
    }
    catch
    {
      return null;
    }
  }

  private static string ToLocalPath(Uri uri)
  {
    if (uri.IsAbsoluteUri)
    {
      if (uri.IsFile) return uri.LocalPath;
      // For non-file URIs, best effort: use absolute URI string
      return uri.AbsoluteUri;
    }

    // Relative or opaque: fall back to original string
    return uri.ToString();
  }

  private static SemanticVersion GetLatestVersion(SemanticVersion[] versions)
  {
    var latest = versions[0];
    for (var i = 1; i < versions.Length; i++)
    {
      if (versions[i] > latest) latest = versions[i];
    }
    return latest;
  }

  private static SemanticVersion? TryParseVersion(string? versionString)
  {
    if (string.IsNullOrWhiteSpace(versionString)) return null;
    return SemanticVersion.TryParse(versionString, out var version) ? version : null;
  }
}
