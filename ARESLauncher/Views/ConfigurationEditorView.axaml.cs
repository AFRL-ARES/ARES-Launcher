using ARESLauncher.Models;
using ARESLauncher.ViewModels;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Threading.Tasks;

namespace ARESLauncher.Views;

public partial class ConfigurationEditorView : UserControl, IActivatableView
{
  public ConfigurationEditorView()
  {
    InitializeComponent();
    this.WhenActivated(d =>
    {
      if(DataContext is ConfigurationEditorViewModel viewModel)
        d(viewModel.UpdateConfirmationDialog.RegisterHandler(DoShowUpdateConfirmationDialog));
      
    });
  }

  private async Task DoShowUpdateConfirmationDialog(InteractionContext<UpdateConfirmationRequest, UpdateConfirmationResponse> context)
  {
    var dialogVm = new UpdateConfirmationDialogViewModel(context.Input);
    var dialog = new UpdateConfirmationDialog
    {
      DataContext = dialogVm
    };

    var closeAndSet = new Action<UpdateConfirmationResponse>(dialog.Close);

    dialogVm.ProceedCommand.Subscribe(closeAndSet);
    dialogVm.CancelCommand.Subscribe(closeAndSet);
    dialogVm.RestoreSnapshotCommand.Subscribe(closeAndSet);
    dialogVm.ResetCommand.Subscribe(closeAndSet);

    var topLevel = TopLevel.GetTopLevel(this);
    if(topLevel is Window window)
    {
      var result = await dialog.ShowDialog<UpdateConfirmationResponse>(window);
      context.SetOutput(result ?? UpdateConfirmationResponse.Cancel);
    }

    else
      context.SetOutput(UpdateConfirmationResponse.Proceed());
  }
}
