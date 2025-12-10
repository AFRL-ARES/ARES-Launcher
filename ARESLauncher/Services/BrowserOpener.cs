using System;
using System.Diagnostics;
using ARESLauncher.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace ARESLauncher.Services;

public class BrowserOpener(IAppConfigurationService _configurationService, ILogger<BrowserOpener> _logger) : IBrowserOpener
{
  public void Open()
  {
    var config = _configurationService.Current;
    var url = config.UiEndpoint;

    try
    {
      var psi = new ProcessStartInfo
      {
        FileName = url,
        UseShellExecute = true
      };

      Process.Start(psi);
    }
    catch(Exception e)
    {
      _logger.LogError("Failed to open ARES in browser: {}", e);
    }
  }

  public void Open(string url)
  {
    try
    {
      var psi = new ProcessStartInfo
      {
        FileName = url,
        UseShellExecute = true
      };

      Process.Start(psi);
    }
    catch(Exception e)
    {
      _logger.LogError("Failed to open browser: {}", e);
    }
  }
}
