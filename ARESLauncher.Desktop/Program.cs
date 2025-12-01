using System;
using System.Runtime.InteropServices;
using ARESLauncher.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace ARESLauncher.Desktop;

class Program
{
  // Initialization code. Don't use any Avalonia, third-party APIs or any
  // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
  // yet and stuff might break.
  [STAThread]
  public static void Main(string[] args) => BuildAvaloniaApp()
      .StartWithClassicDesktopLifetime(args);

  // Avalonia configuration, don't remove; also used by visual designer.
  public static AppBuilder BuildAvaloniaApp()
      => AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .WithInterFont()
          .LogToTrace()
          .UseReactiveUI()
          .AfterSetup(AfterSetup);

  private static void AfterSetup(AppBuilder builder)
  {
    if(Application.Current?.ApplicationLifetime
                is not IClassicDesktopStyleApplicationLifetime lifetime)
    {
      return;
    }

    var iconPath = "avares://ARESLauncher.Base/Assets/BlackARESLogo_Smol.ico";

    if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      // White logo looks better in the system bar
      iconPath = "avares://ARESLauncher.Base/Assets/WhiteARESLogo_Smol.ico";
    }

    var icon = AssetLoader.Open(new Uri(iconPath));
    var trayIcon = new TrayIcon
    {
      ToolTipText = "ARES",
      Icon = new WindowIcon(icon),
      Menu = new NativeMenu
      {
        Items =
          {
            new NativeMenuItem("Open ARES")
            {
              Command = ReactiveCommand.Create(() =>
              {
                var browserOpener = App.ServiceProvider?.GetService<IBrowserOpener>();
                browserOpener?.Open();
              })
            },
            new NativeMenuItem("Open Launcher")
            {
              Command = ReactiveCommand.Create(() =>
              {
                lifetime.MainWindow?.Show();
                lifetime.MainWindow?.Activate();
              })
            },
            new NativeMenuItemSeparator(),
            new NativeMenuItem("Exit")
            {
              Command = ReactiveCommand.Create(() =>
              {
                if(Application.Current is App app)
                {
                  app.BeginShutdown();
                }
                lifetime.Shutdown();
              })
            }
          }
      },
      IsVisible = true,
      Command = ReactiveCommand.Create(() =>
      {
        lifetime.MainWindow?.Show();
        lifetime.MainWindow?.Activate();
      })
    };
  }
}
