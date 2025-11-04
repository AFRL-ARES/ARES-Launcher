using ARESLauncher.Services;
using ARESLauncher.Services.Configuration;
using ARESLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ARESLauncher;

public static class ServiceCollectionExtensions
{
  public static void AddCommonServices(this ServiceCollection collection)
  {

    collection.AddLogging(b =>
    {
      var logger = new LoggerConfiguration().WriteTo.File("ares-launcher.log", rollingInterval: RollingInterval.Day).CreateLogger();
      b.AddSerilog(logger);
    });
    collection.AddSingleton<IAppConfigurationService, JsonAppConfigurationService>();
    collection.AddSingleton<IAppSettingsUpdater, AppSettingsUpdater>();
    collection.AddSingleton<IAresBinaryManager, AresBinaryManager>();
    collection.AddSingleton<IAresDownloader, AresGithubDownloader>();
    collection.AddSingleton<IAresUpdater, AresUpdater>();
    collection.AddSingleton<IAresStarter, AresStarter>();
    collection.AddSingleton<IDatabaseManager, DatabaseManager>();
    collection.AddSingleton<IExecutableGetter, ExecutableGetter>();
    collection.AddSingleton<ICertificateManager, CertificateManager>();
    collection.AddSingleton<IBrowserOpener, BrowserOpener>();
    collection.AddSingleton<IConflictManager, ConflictManager>();
    collection.AddTransient<ConfigurationOverviewViewModel>();
    collection.AddTransient<ConfigurationEditorViewModel>();
    collection.AddTransient<MainViewModel>();
  }
}
