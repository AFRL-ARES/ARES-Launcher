using System;
using System.IO;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Models;
using ARESLauncher.Tools;
using ARESLauncher.Configuration;

namespace ARESLauncher.Services;

public class AppSettingsUpdater(IAppConfigurationService _configurationService) : IAppSettingsUpdater
{
  public void Update(AresComponent component)
  {
    var path = component switch
    {
      AresComponent.Ui => _configurationService.Current.UiDataPath,
      AresComponent.Service => _configurationService.Current.ServiceDataPath,
      _ => throw new ArgumentOutOfRangeException(nameof(component), component, null)
    };

    path = Path.Combine(path, "appsettings.json");
    AppSettingsHelper.Update(path, appSettings =>
    {
      appSettings.DatabaseProvider = _configurationService.Current.DatabaseProvider;
      appSettings.ConnectionStrings[DatabaseProvider.Sqlite] = $"Data Source={_configurationService.Current.SqliteDatabasePath}";
      appSettings.ConnectionStrings[DatabaseProvider.SqlServer] = _configurationService.Current.SqlServerConnectionString;
      appSettings.ConnectionStrings[DatabaseProvider.Postgres] = _configurationService.Current.PostgresConnectionString;

      UpdateKestrel(appSettings, component, _configurationService.Current);
    });
  }

  private static void UpdateKestrel(AppSettings settings, AresComponent component, LauncherConfiguration configuration)
  {
    var endpoint = component == AresComponent.Ui ? configuration.UiEndpoint : configuration.ServiceEndpoint;

    var uri = new Uri(endpoint);

    settings.Kestrel ??= new KestrelOptions();

    settings.Kestrel.Endpoints ??= new EndpointsOptions();

    var endpoints = settings.Kestrel.Endpoints;

    if(uri.Scheme == Uri.UriSchemeHttp)
    {
      endpoints.Http ??= new HttpEndpoint();
      endpoints.Http.Url = uri.AbsoluteUri;
    } else if (uri.Scheme == Uri.UriSchemeHttps)
    {
      endpoints.Https ??= new HttpsEndpoint();
      endpoints.Https.Url = uri.AbsoluteUri;

      var certOptions = new CertificateOptions();
      endpoints.Https.Certificate = certOptions;

      certOptions.Password = configuration.CertificatePassword;
      certOptions.Path = configuration.CertificatePath;
    }
  }

  public void UpdateAll()
  {
    foreach (var aresComponent in Enum.GetValues<AresComponent>())
    {
      Update(aresComponent);
    }
  }
}
