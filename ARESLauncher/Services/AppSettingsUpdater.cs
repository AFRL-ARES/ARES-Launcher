using System;
using System.IO;
using ARESLauncher.Services.Configuration;
using ARESLauncher.Models;

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
      appSettings.ConnectionStrings[DatabaseProvider.Sqlite] = _configurationService.Current.SqliteDatabasePath;
    });
  }

  public void UpdateAll()
  {
    foreach (var aresComponent in Enum.GetValues<AresComponent>())
    {
      Update(aresComponent);
    }
  }
}
