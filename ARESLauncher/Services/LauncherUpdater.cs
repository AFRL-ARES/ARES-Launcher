using ARESLauncher.Services.Configuration;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public class LauncherUpdater : ILauncherUpdater
{
  private readonly IAresDownloader _downloader;
  private readonly ILogger<LauncherUpdater> _logger;
  private readonly IAppConfigurationService _configurationService;

  public LauncherUpdater(IAresDownloader downloader, ILogger<LauncherUpdater> logger, IAppConfigurationService configurationService)
  {
    _downloader = downloader;
    _logger = logger;
    _configurationService = configurationService;
  }

  public Task<SemanticVersion[]> GetAvailableVersions()
  {
    var source = _configurationService.Current.LauncerSource;
    return _downloader.GetAvailableVersions(source);
  }
}
