using System;
using System.IO;
using ARESLauncher.Configuration;
using ARESLauncher.Models;
using ARESLauncher.Models.AppSettings;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;

namespace ARESLauncher.Services;

public class AppSettingsUpdater(IAppConfigurationService _configurationService) : IAppSettingsUpdater
{
  public void Update(AresComponent component)
  {
    switch (component)
    {
      case AresComponent.Ui:
        UpdateUi();
        return;
      case AresComponent.Service:
        UpdateService();
        return;
      default:
        throw new ArgumentOutOfRangeException(nameof(component), component, null);
    }
  }

  public void UpdateAll()
  {
    foreach (var aresComponent in Enum.GetValues<AresComponent>()) Update(aresComponent);
  }

  private void UpdateUi()
  {
    var path = _configurationService.Current.UiBinaryPath;
    path = Path.Combine(path, "appsettings.ui.json");

    var serviceUri = new Uri(_configurationService.Current.ServiceEndpoint);

    AppSettingsHelper.Update<AppSettingsUi>(path, appSettings =>
    {
      ApplyCommonSettings(appSettings);
      appSettings.RemoteServiceSettings ??= new RemoteServiceSettings();
      appSettings.RemoteServiceSettings.ServerHost = serviceUri.Host;
      appSettings.RemoteServiceSettings.ServerPort = serviceUri.Port;
      UpdateKestrel(appSettings, AresComponent.Ui, _configurationService.Current);
    });
  }

  private void UpdateService()
  {
    var path = _configurationService.Current.ServiceBinaryPath;
    path = Path.Combine(path, "appsettings.aresservice.json");

    AppSettingsHelper.Update<AppSettingsService>(path, appSettings =>
    {
      ApplyCommonSettings(appSettings);
      appSettings.AresDataPath = _configurationService.Current.AresDataPath;
      UpdateKestrel(appSettings, AresComponent.Service, _configurationService.Current);
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
  }

  private static void UpdateKestrel(AppSettingsBase settingsBase, AresComponent component,
    LauncherConfiguration configuration)
  {
    var endpoint = component == AresComponent.Ui ? configuration.UiEndpoint : configuration.ServiceEndpoint;

    var uri = new Uri(endpoint);

    settingsBase.Kestrel ??= new KestrelOptions();

    settingsBase.Kestrel.Endpoints ??= new EndpointsOptions();

    var endpoints = settingsBase.Kestrel.Endpoints;

    if (uri.Scheme == Uri.UriSchemeHttp)
    {
      endpoints.Http ??= new HttpEndpoint();
      endpoints.Http.Url = uri.AbsoluteUri;
    }
    else if (uri.Scheme == Uri.UriSchemeHttps)
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