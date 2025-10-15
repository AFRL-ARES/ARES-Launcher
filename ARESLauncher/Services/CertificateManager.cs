using System;
using System.IO;
using System.Threading.Tasks;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging;

namespace ARESLauncher.Services;

internal class CertificateManager(IAppConfigurationService _configurationService, ILogger<CertificateManager> _logger) : ICertificateManager
{
  public async Task Update()
  {
    var certPath = _configurationService.Current.CertificatePath;
    var certPassword = _configurationService.Current.CertificatePassword;

    if (!File.Exists(certPath))
    {
      certPath = await CertificateHelper.GenerateCertificate(certPath, certPassword);
      if (string.IsNullOrEmpty(certPath))
      {
        _logger.LogCritical("Unable to generate a certificate to {}", certPath);
        return;
      }
    }

    try
    {
      await CertificateHelper.AddCertificate(certPath, certPassword);
    }
    catch(Exception ex)
    {
      _logger.LogCritical("Failed to add certificate: {Exception}", ex);
    }
  }
}