using System;
using ARESLauncher.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace ARESLauncher.Views;

public partial class MainView : UserControl
{
  public MainView()
  {
    InitializeComponent();
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    if(DataContext is not MainViewModel vm)
      return;

    vm.ResolveConflictsCommand.Execute().Subscribe();
  }
}
