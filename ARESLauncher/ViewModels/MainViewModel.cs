using System;

namespace ARESLauncher.ViewModels;

public class MainViewModel : ViewModelBase
{
  public MainViewModel(ConfigurationOverviewViewModel overview,
    ConfigurationEditorViewModel editor)
  {
    Overview = overview ?? throw new ArgumentNullException(nameof(overview));
    Editor = editor ?? throw new ArgumentNullException(nameof(editor));

    Editor.ConfigurationSaved += OnConfigurationSaved;
  }

  public ConfigurationOverviewViewModel Overview { get; }
  public ConfigurationEditorViewModel Editor { get; }

  private void OnConfigurationSaved(object? sender, EventArgs e)
  {
    Overview.Refresh();
  }
}
