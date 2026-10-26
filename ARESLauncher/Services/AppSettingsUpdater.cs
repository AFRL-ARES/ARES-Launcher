using System;
using System.IO;
using ARESLauncher.Configuration;
using ARESLauncher.Models;
using ARESLauncher.Models.AppSettings;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging;

namespace ARESLauncher.Services;

public class AppSettingsUpdater(IAppConfigurationService _configurationService, ILogger<AppSettingsUpdater> _logger) : IAppSettingsUpdater
{
  public void UpdateAresSettings()
  {
    var path = _configurationService.Current.UiBinaryPath;
    path = Path.Combine(path, "appsettings.ui.json");
    if(!File.Exists(path))
    {
      _logger.LogWarning("Tried to update the UI settings but the settings file at {Path} was not found.", path);
      return;
    }

    AppSettingsHelper.Update<AppSettingsUi>(path, appSettings =>
    {
      ApplyCommonSettings(appSettings);
      appSettings.RemoteServiceSettings ??= new RemoteServiceSettings();
      UpdateKestrel(appSettings, _configurationService.Current);
    });
  }

  private void ApplyCommonSettings(AppSettingsBase appSettings)
  {
    appSettings.DatabaseProvider = _configurationService.Current.DatabaseProvider;
    appSettings.ConnectionStrings[DatabaseProvider.Sqlite] =
      $"Data Source={_configurationService.Current.SqliteDatabasePath}";
    appSettings.ConnectionStrings[DatabaseProvider.SqlServer] = _configurationService.Current.SqlServerConnectionString;
    appSettings.ConnectionStrings[DatabaseProvider.Postgres] = _configurationService.Current.PostgresConnectionString;

    appSettings.CertificateSettings ??= new CertificateSettings();
    appSettings.CertificateSettings.Path = _configurationService.Current.CertificatePath;
    appSettings.CertificateSettings.Password = _configurationService.Current.CertificatePassword;
    appSettings.AresDataPath = _configurationService.Current.AresDataPath;
  }

  private static void UpdateKestrel(AppSettingsBase settingsBase, LauncherConfiguration configuration)
  {
    var endpoint = configuration.UiEndpoint;

    var uri = new Uri(endpoint);

    settingsBase.Kestrel ??= new KestrelOptions();

    settingsBase.Kestrel.Endpoints ??= new EndpointsOptions();

    var endpoints = settingsBase.Kestrel.Endpoints;

    if(uri.Scheme == Uri.UriSchemeHttp)
    {
      endpoints.Http ??= new HttpEndpoint();
      endpoints.Http.Url = uri.AbsoluteUri;
    }
    else if(uri.Scheme == Uri.UriSchemeHttps)
    {
      endpoints.Https ??= new HttpsEndpoint();
      endpoints.Https.Url = uri.AbsoluteUri;

      var certOptions = new CertificateOptions();
      endpoints.Https.Certificate = certOptions;

      certOptions.Password = configuration.CertificatePassword;
      certOptions.Path = configuration.CertificatePath;
    }
  }
}