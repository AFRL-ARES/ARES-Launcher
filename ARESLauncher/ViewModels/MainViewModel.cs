using ARESLauncher.Services.Configuration;
using ReactiveUI;

namespace ARESLauncher.ViewModels;

public class MainViewModel : ViewModelBase
{
  private readonly IAppConfigurationService _configurationService;

  public MainViewModel(IAppConfigurationService configurationService)
  {
    _configurationService = configurationService;
    
  }

  public string UIDirectory => _configurationService.Current.UiDataPath;

  public string ServiceDirectory => _configurationService.Current.ServiceDataPath;
}