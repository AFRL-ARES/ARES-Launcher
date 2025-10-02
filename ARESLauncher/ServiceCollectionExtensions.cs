using ARESLauncher.Services.Configuration;
using ARESLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ARESLauncher;

public static class ServiceCollectionExtensions
{
  public static void AddCommonServices(this ServiceCollection collection)
  {
    collection.AddSingleton<IAppConfigurationService, JsonAppConfigurationService>();
    collection.AddTransient<MainViewModel>();
  }
}
