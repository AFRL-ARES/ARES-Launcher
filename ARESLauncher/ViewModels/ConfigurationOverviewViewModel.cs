using System;
using System.Collections.Generic;
using System.Linq;
using ARESLauncher.Services.Configuration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ARESLauncher.ViewModels;

public partial class ConfigurationOverviewViewModel : ViewModelBase
{
  private readonly IAppConfigurationService _configurationService;
  private string _uiDataPath = string.Empty;
  private string _serviceDataPath = string.Empty;
  private string _sqliteDatabasePath = string.Empty;
  private string _databaseProvider = string.Empty;
  private string _defaultRepositoryDisplay = string.Empty;
  private IEnumerable<string> _availableRepositoriesDisplay = Enumerable.Empty<string>();
  private string _gitToken = string.Empty;

  public ConfigurationOverviewViewModel(IAppConfigurationService configurationService)
  {
    _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    Refresh();
  }

  public string UiDataPath
  {
    get => _uiDataPath;
    private set => this.RaiseAndSetIfChanged(ref _uiDataPath, value);
  }

  public string ServiceDataPath
  {
    get => _serviceDataPath;
    private set => this.RaiseAndSetIfChanged(ref _serviceDataPath, value);
  }

  public string SqliteDatabasePath
  {
    get => _sqliteDatabasePath;
    private set => this.RaiseAndSetIfChanged(ref _sqliteDatabasePath, value);
  }

  public string DatabaseProvider
  {
    get => _databaseProvider;
    private set => this.RaiseAndSetIfChanged(ref _databaseProvider, value);
  }

  public string DefaultRepositoryDisplay
  {
    get => _defaultRepositoryDisplay;
    private set => this.RaiseAndSetIfChanged(ref _defaultRepositoryDisplay, value);
  }

  public IEnumerable<string> AvailableRepositoriesDisplay
  {
    get => _availableRepositoriesDisplay;
    private set => this.RaiseAndSetIfChanged(ref _availableRepositoriesDisplay, value);
  }

  public string GitToken
  {
    get => _gitToken;
    private set => this.RaiseAndSetIfChanged(ref _gitToken, value);
  }

  [Reactive]
  public partial string AresDataPath { get; private set; }

  [Reactive]
  public partial string BinariesRoot { get; private set; }

  public void Refresh()
  {
    var current = _configurationService.Current;

    UiDataPath = current.UiBinaryPath;
    ServiceDataPath = current.ServiceBinaryPath;
    SqliteDatabasePath = current.SqliteDatabasePath;
    DatabaseProvider = current.DatabaseProvider.ToString();
    DefaultRepositoryDisplay = $"{current.CurrentAresRepo.Owner}/{current.CurrentAresRepo.Repo}";
    AvailableRepositoriesDisplay = current.AvailableAresRepos.Select(repo => $"{repo.Owner}/{repo.Repo}").ToArray();
    GitToken = current.GitToken;
    AresDataPath = current.AresDataPath;
  }
}
