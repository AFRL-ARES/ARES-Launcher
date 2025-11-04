using System;
using System.Reactive;
using System.Reactive.Linq;
using ARESLauncher.ViewModels;
using Avalonia;
using Avalonia.Controls;

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
