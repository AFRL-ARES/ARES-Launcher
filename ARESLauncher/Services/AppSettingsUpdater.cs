using System;
using System.IO;
using ARESLauncher.Configuration;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

public class AppSettingsUpdater(LauncherConfiguration _configuration) : IAppSettingsUpdater
{
  public void Update(AresComponent component)
  {
    var path = component switch
    {
      AresComponent.Ui => _configuration.UiDataPath,
      AresComponent.Service => _configuration.ServiceDataPath,
      _ => throw new ArgumentOutOfRangeException(nameof(component), component, null)
    };

    path = Path.Combine(path, "appsettings.json");
    AppSettingsHelper.Update(path, appSettings =>
    {
      appSettings.DatabaseProvider = _configuration.DatabaseProvider;
      appSettings.ConnectionStrings[DatabaseProvider.Sqlite] = _configuration.SqliteDatabasePath;
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