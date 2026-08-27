using System;
using System.Reactive;
using System.Reactive.Linq;
using ARESLauncher.Models;
using ARESLauncher.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ARESLauncher.Views;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
  }

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    if(DataContext is not MainViewModel vm)
      return;

    vm.ConflictDialog.RegisterHandler(async interaction =>
    {
      var dialogVm = vm.GetConflictResolutionDialogViewModel();
      var dialog = new ConflictResolutionDialog
      {
        DataContext = dialogVm
      };

      var mergedObservable = dialogVm.TakeOverCommand.Merge(dialogVm.KillCommand).Merge(dialogVm.IgnoreCommand);
      mergedObservable.Subscribe(_ =>
      {
        dialog.Close(Unit.Default);
      });

      await dialog.ShowDialog<Unit>(this);

      interaction.SetOutput(Unit.Default);
    });

    vm.UpdateConfirmationDialog.RegisterHandler(async interaction =>
    {
      var dialogVm = new UpdateConfirmationDialogViewModel(interaction.Input);
      var dialog = new UpdateConfirmationDialog
      {
        DataContext = dialogVm
      };

      var closeAndSet = new Action<UpdateConfirmationResponse>(res => dialog.Close(res));

      dialogVm.ProceedCommand.Subscribe(closeAndSet);
      dialogVm.CancelCommand.Subscribe(closeAndSet);
      dialogVm.RestoreSnapshotCommand.Subscribe(closeAndSet);
      dialogVm.ResetCommand.Subscribe(closeAndSet);

      var result = await dialog.ShowDialog<UpdateConfirmationResponse>(this);
      interaction.SetOutput(result ?? UpdateConfirmationResponse.Cancel);
    });

    vm.PyAresOrphansDialog.RegisterHandler(async interaction =>
    {
      var orphans = interaction.Input;
      if(orphans is null || orphans.Count == 0)
      {
        interaction.SetOutput(false);
        return;
      }

      var dialog = new Window
      {
        Title = "PyAres Services Running",
        Width = 420,
        Height = 260,
        WindowStartupLocation = WindowStartupLocation.CenterOwner
      };

      var header = new TextBlock
      {
        Text = $"We found {orphans.Count} existing PyAres service(s) running.",
        Margin = new Thickness(0, 0, 0, 8),
        TextWrapping = Avalonia.Media.TextWrapping.Wrap
      };

      var listPanel = new StackPanel { Orientation = Orientation.Vertical };
      foreach(var info in orphans)
      {
        listPanel.Children.Add(new TextBlock
        {
          Text = $"• {info.Name} (PID {info.Pid}, {info.EntryPoint})",
          FontSize = 12
        });
      }

      var stopButton = new Button
      {
        Content = "Stop All PyAres Services",
        Margin = new Thickness(0, 8, 8, 0)
      };

      var keepButton = new Button
      {
        Content = "Keep Services Running",
        Margin = new Thickness(0, 8, 0, 0)
      };

      var buttonPanel = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Right
      };
      buttonPanel.Children.Add(stopButton);
      buttonPanel.Children.Add(keepButton);

      var root = new StackPanel
      {
        Margin = new Thickness(16),
        Orientation = Orientation.Vertical
      };
      root.Children.Add(header);
      root.Children.Add(listPanel);
      root.Children.Add(buttonPanel);

      dialog.Content = root;

      var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
      stopButton.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
      keepButton.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };

      dialog.Show();
      var stopAll = await tcs.Task;

      interaction.SetOutput(stopAll);
    });
  }

  protected override void OnClosing(WindowClosingEventArgs e)
  {
    if(Application.Current is App app && app.IsShuttingDown)
    {
      base.OnClosing(e);
      return;
    }

    e.Cancel = true;
    Hide();
    base.OnClosing(e);
  }
}
