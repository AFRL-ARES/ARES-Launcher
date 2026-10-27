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
  private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
  private readonly IAppConfigurationService _configurationService;
  private readonly IExecutableGetter _executableGetter;
  private readonly ILogger<AresBinaryManager> _logger;

  public AresBinaryManager(IAppConfigurationService configurationService, IExecutableGetter executableGetter, ILogger<AresBinaryManager> logger)
  {
    _configurationService = configurationService;
    _executableGetter = executableGetter;
    _logger = logger;
  }

  public async Task Refresh()
  {
    var metadata = await BinaryMetadataHelper.ReadMetadata(_configurationService.Current.UiBinaryPath);

    // Load appsettings (if present)
    AresSettings = TryLoadAppSettings<AppSettingsUi>(Path.Combine(_configurationService.Current.UiBinaryPath, "appsettings.ui.json"));

    if(AresSettings is null)
      _logger.LogWarning("Ui settings not found in {}", _configurationService.Current.UiBinaryPath);

    var versionFromMetadata = TryParseVersion(metadata?.Version);

    if(versionFromMetadata is null)
      _logger.LogWarning("Unable to find the version metadata file. Gonna try assembly version.");

    // Determine installed version by inspecting one of the binaries (prefer UI, then service) if metadata missing.
    // We technically wouldn't really use the file/assembly info to set the version, but AI thought it was a good
    // idea and it doesn't hurt to have a fallback in case the version was not populated via the metadata.
    CurrentVersion = versionFromMetadata ?? TryGetInstalledVersion();

    // Prefer source from metadata, otherwise fall back to default source when version is known.
    CurrentSource = metadata?.Source ?? (CurrentVersion is not null ? _configurationService.Current.CurrentAresRepo : null);
  }

  public void SetDataPath(Uri path)
  {
    var newPath = ToLocalPath(path);
    _logger.LogInformation("Setting UI Executable to {pathdood}", path);
    _configurationService.Update(cfg => cfg.UiBinaryPath = newPath);
  }

  private SemanticVersion? TryGetInstalledVersion()
  {
    _logger.LogInformation("Trying to Get Installed Version");
    var aresExecutablePath = _executableGetter.GetUiExecutablePath();

    if(string.IsNullOrWhiteSpace(aresExecutablePath)) 
      return null;

    // Try exact path first
    if(File.Exists(aresExecutablePath))
    {
      var semver = TryGetVersionFromAssembly(aresExecutablePath) ?? TryGetVersionFromFileInfo(aresExecutablePath);
      if(semver is not null)
      {
        _logger.LogInformation("Found Version from Assembly, Determined ARES Version from {ares-version}", semver);
        return semver;
      }
          
    }

    // If no extension (common on Linux/macOS), try sibling .dll (framework-dependent publish)
    if(Path.GetExtension(aresExecutablePath) == string.Empty)
    {
      var dllCandidate = aresExecutablePath + ".dll";
      if(File.Exists(dllCandidate))
      {
        var semver = TryGetVersionFromAssembly(dllCandidate);
        if(semver is not null)
        {
          _logger.LogInformation("Determined semantic version from DLL of ARES to be {version}", semver);
          return semver;
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
      if(v is null) 
        return null;
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
      if(string.IsNullOrWhiteSpace(versionString)) 
        return null;

      // Trim possible metadata/suffixes not compatible with SemanticVersion parser
      versionString = versionString.Trim();
      var spaceIdx = versionString.IndexOf(' ');
      if(spaceIdx > 0) 
        versionString = versionString[..spaceIdx];

      return SemanticVersion.TryParse(versionString, out var semver) ? semver : null;
    }
    catch
    {
      return null;
    }
  }

  private static T? TryLoadAppSettings<T>(string path) where T : AppSettingsBase
  {
    if(!File.Exists(path)) 
      return null;

    try
    {
      var json = File.ReadAllText(path);
      if(string.IsNullOrWhiteSpace(json)) return null;
      return JsonSerializer.Deserialize<T>(json, _serializerOptions);
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

  private static SemanticVersion? TryParseVersion(string? versionString)
  {
    if(string.IsNullOrWhiteSpace(versionString)) 
      return null;
    
    return SemanticVersion.TryParse(versionString, out var version) ? version : null;
  }

  public SemanticVersion? CurrentVersion { get; private set; }
  public AppSettingsService? ServiceSettings { get; private set; }
  public AppSettingsUi? AresSettings { get; private set; }
  public AresSource? CurrentSource { get; private set; }
}