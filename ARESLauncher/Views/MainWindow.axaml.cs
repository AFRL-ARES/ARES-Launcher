using Avalonia;
using Avalonia.Controls;

namespace ARESLauncher.Views;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
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
