using ARESLauncher.Services.Configuration;

namespace ARESLauncher.ViewModels;

public class MainViewModel : ViewModelBase
{
  private readonly IAppConfigurationService _configurationService;

  public MainViewModel(IAppConfigurationService configurationService)
  {
    _configurationService = configurationService;
  }

  public string UIDirectory => _configurationService.Current.UIDataPath;

  public string ServiceDirectory => _configurationService.Current.ServiceDataPath;
}