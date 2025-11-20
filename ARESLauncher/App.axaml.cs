using ARESLauncher.ViewModels;
using ARESLauncher.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARESLauncher;

public class App : Application
{
  private const string AppMutexName = "6293b4fc-466d-4135-ad8c-7ed25c6840c2";
  private static Mutex? _mutex;

  public bool IsShuttingDown { get; private set; }

  public static IServiceProvider? ServiceProvider { get; private set; }

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    _mutex = new Mutex(true, AppMutexName, out var createdNew);
    if(!createdNew)
    {
      ShowInstanceAlreadyRunningPopup();
      return;
    }

    var collection = new ServiceCollection();
    collection.AddCommonServices();
    var services = collection.BuildServiceProvider();
    ServiceProvider = services;

    var vm = services.GetRequiredService<MainViewModel>();
    if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.MainWindow = new MainWindow
      {
        DataContext = vm
      };

      desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

      desktop.ShutdownRequested += (_, _) =>
      {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
        // TODO: Double check this works on Windows
        IsShuttingDown = true;
      };

      desktop.Exit += (_, _) => 
      {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
        IsShuttingDown = true; 
      };
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

  public void BeginShutdown()
  {
    IsShuttingDown = true;
  }

  private async void ShowInstanceAlreadyRunningPopup()
  {
    // Get a reference to the desktop lifetime
    if(ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
    {
      return;
    }

    // Create a simple window to act as a popup.
    var popup = new Window
    {
      Title = "Application Error",
      Width = 400,
      Height = 150,
      WindowStartupLocation = WindowStartupLocation.CenterScreen,
      Topmost = true, // Ensure it's seen
      Content = new StackPanel
      {
        Spacing = 10,
        Margin = new Thickness(20),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Children =
        {
          new TextBlock
          {
              Text = "Launcher Already Running",
              FontSize = 16,
              FontWeight = Avalonia.Media.FontWeight.Bold,
              HorizontalAlignment = HorizontalAlignment.Center
          },
          new TextBlock
          {
              Text = "Another instance of the ARES Launcher is already running. This instance will now close.",
              HorizontalAlignment = HorizontalAlignment.Center,
              TextWrapping = Avalonia.Media.TextWrapping.Wrap
          }
        }
      }
    };

    // Show the popup.
    popup.Show();

    // Give the user a few seconds to read the message.
    await Task.Delay(3000);

    // Close the popup and shut down the application.
    popup.Close();
    desktop.Shutdown();
  }
}