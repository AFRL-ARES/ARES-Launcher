using ARESLauncher.Services;
using ARESLauncher.Services.Configuration;
using ARESLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ARESLauncher;

public static class ServiceCollectionExtensions
{
  public static void AddCommonServices(this ServiceCollection collection)
  {
    collection.AddSingleton<IAppConfigurationService, JsonAppConfigurationService>();
    collection.AddSingleton<IAppSettingsUpdater, AppSettingsUpdater>();
    collection.AddSingleton<IAresBinaryManager, AresBinaryManager>();
    collection.AddSingleton<IAresDownloader, AresDownloader>();
    collection.AddSingleton<IAresUpdater, AresUpdater>();
    collection.AddSingleton<IAresStarter, AresStarter>();
    collection.AddSingleton<IDatabaseManager, DatabaseManager>();
    collection.AddSingleton<IExecutableGetter, ExecutableGetter>();
    collection.AddTransient<ConfigurationOverviewViewModel>();
    collection.AddTransient<ConfigurationEditorViewModel>();
    collection.AddTransient<MainViewModel>();
  }
}
