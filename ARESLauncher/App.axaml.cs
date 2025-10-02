using ARESLauncher.ViewModels;
using ARESLauncher.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace ARESLauncher;

public partial class App : Application
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    var collection = new ServiceCollection();
    collection.AddCommonServices();
    var services = collection.BuildServiceProvider();

    var vm = services.GetRequiredService<MainViewModel>();
    if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.MainWindow = new MainWindow
      {
        DataContext = vm
      };

      desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
    }
    else if(ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
    {
      singleViewPlatform.MainView = new MainView
      {
        DataContext = vm
      };
    }

    base.OnFrameworkInitializationCompleted();
  }
}
