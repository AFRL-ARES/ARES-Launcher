using ARESLauncher.ViewModels;
using ARESLauncher.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace ARESLauncher;

public class App : Application
{
  public bool IsShuttingDown { get; private set; }

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
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.MainWindow = new MainWindow
      {
        DataContext = vm
      };

      desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

      desktop.ShutdownRequested += (_, _) =>
      {
        // TODO: Double check this works on Windows
        IsShuttingDown = true;
      };

      desktop.Exit += (_, _) => { IsShuttingDown = true; };
    }
    else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
    {
      singleViewPlatform.MainView = new MainView
      {
        DataContext = vm
      };
    }

    base.OnFrameworkInitializationCompleted();
  }

  public void BeginShutdown()
  {
    IsShuttingDown = true;
  }
}