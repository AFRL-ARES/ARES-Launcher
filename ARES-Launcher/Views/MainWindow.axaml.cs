using Avalonia.Controls;

namespace ARES_Launcher.Views;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
  }

  protected override void OnClosing(WindowClosingEventArgs e)
  {
    e.Cancel = true;
    this.Hide();
    base.OnClosing(e);
  }
}
