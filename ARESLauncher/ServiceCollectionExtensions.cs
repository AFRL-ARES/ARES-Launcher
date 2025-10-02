using ARESLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ARESLauncher;

public static class ServiceCollectionExtensions
{
  public static void AddCommonServices(this ServiceCollection collection)
  {
    collection.AddTransient<MainViewModel>();
  }
}