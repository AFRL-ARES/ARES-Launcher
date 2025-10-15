using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;
using ReactiveUI;

namespace ARESLauncher.ViewModels;

public class ConfigurationEditorViewModel : ViewModelBase
{
  private readonly IAppConfigurationService _configurationService;
  private readonly IReadOnlyList<DatabaseProvider> _databaseProviders;
  private string _editableUiDataPath = string.Empty;
  private string _editableServiceDataPath = string.Empty;
  private string _editableSqliteDatabasePath = string.Empty;
  private DatabaseProvider _editableDatabaseProvider;
  private string _editableDefaultRepoOwner = string.Empty;
  private string _editableDefaultRepoName = string.Empty;
  private string _editableGitToken = string.Empty;
  private AresSourceEditorViewModel? _selectedAvailableRepository;

  public ConfigurationEditorViewModel(IAppConfigurationService configurationService)
  {
    _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    _databaseProviders = Enum.GetValues<DatabaseProvider>();

    AvailableRepositories = new ObservableCollection<AresSourceEditorViewModel>();
    LoadEditableConfiguration();

    AddRepositoryCommand = ReactiveCommand.Create(AddRepository);
    RemoveSelectedRepositoryCommand = ReactiveCommand.Create(RemoveSelectedRepository,
      this.WhenAnyValue(vm => vm.SelectedAvailableRepository).Select(repo => repo is not null));
    SaveConfigurationCommand = ReactiveCommand.Create(SaveConfiguration);
    ResetConfigurationCommand = ReactiveCommand.Create(ResetEditableConfiguration);
  }

  public event EventHandler? ConfigurationSaved;

  public IReadOnlyList<DatabaseProvider> DatabaseProviders => _databaseProviders;

  public ObservableCollection<AresSourceEditorViewModel> AvailableRepositories { get; }

  public AresSourceEditorViewModel? SelectedAvailableRepository
  {
    get => _selectedAvailableRepository;
    set => this.RaiseAndSetIfChanged(ref _selectedAvailableRepository, value);
  }

  public string EditableUiDataPath
  {
    get => _editableUiDataPath;
    set => this.RaiseAndSetIfChanged(ref _editableUiDataPath, value);
  }

  public string EditableServiceDataPath
  {
    get => _editableServiceDataPath;
    set => this.RaiseAndSetIfChanged(ref _editableServiceDataPath, value);
  }

  public string EditableSqliteDatabasePath
  {
    get => _editableSqliteDatabasePath;
    set => this.RaiseAndSetIfChanged(ref _editableSqliteDatabasePath, value);
  }

  public DatabaseProvider EditableDatabaseProvider
  {
    get => _editableDatabaseProvider;
    set => this.RaiseAndSetIfChanged(ref _editableDatabaseProvider, value);
  }

  public string EditableDefaultRepoOwner
  {
    get => _editableDefaultRepoOwner;
    set => this.RaiseAndSetIfChanged(ref _editableDefaultRepoOwner, value);
  }

  public string EditableDefaultRepoName
  {
    get => _editableDefaultRepoName;
    set => this.RaiseAndSetIfChanged(ref _editableDefaultRepoName, value);
  }

  public string EditableGitToken
  {
    get => _editableGitToken;
    set => this.RaiseAndSetIfChanged(ref _editableGitToken, value);
  }

  public ReactiveCommand<Unit, Unit> AddRepositoryCommand { get; }

  public ReactiveCommand<Unit, Unit> RemoveSelectedRepositoryCommand { get; }

  public ReactiveCommand<Unit, Unit> SaveConfigurationCommand { get; }

  public ReactiveCommand<Unit, Unit> ResetConfigurationCommand { get; }

  private void AddRepository()
  {
    var newRepository = new AresSourceEditorViewModel(string.Empty, string.Empty);
    AvailableRepositories.Add(newRepository);
    SelectedAvailableRepository = newRepository;
  }

  private void RemoveSelectedRepository()
  {
    if (SelectedAvailableRepository is null)
    {
      return;
    }

    var index = AvailableRepositories.IndexOf(SelectedAvailableRepository);
    AvailableRepositories.Remove(SelectedAvailableRepository);

    if (AvailableRepositories.Count == 0)
    {
      SelectedAvailableRepository = null;
      return;
    }

    index = Math.Clamp(index, 0, AvailableRepositories.Count - 1);
    SelectedAvailableRepository = AvailableRepositories[index];
  }

  private void SaveConfiguration()
  {
    _configurationService.Update(configuration =>
    {
      configuration.UiDataPath = EditableUiDataPath;
      configuration.ServiceDataPath = EditableServiceDataPath;
      configuration.SqliteDatabasePath = EditableSqliteDatabasePath;
      configuration.DatabaseProvider = EditableDatabaseProvider;
      configuration.GitToken = EditableGitToken;
      configuration.CurrentAresRepo = new AresSource(EditableDefaultRepoOwner, EditableDefaultRepoName);
      configuration.AvailableAresRepos = AvailableRepositories
        .Where(repo => !string.IsNullOrWhiteSpace(repo.Owner) && !string.IsNullOrWhiteSpace(repo.Repo))
        .Select(repo => repo.ToAresSource())
        .ToArray();
    });

    LoadEditableConfiguration();
    ConfigurationSaved?.Invoke(this, EventArgs.Empty);
  }

  private void ResetEditableConfiguration()
  {
    LoadEditableConfiguration();
  }

  private void LoadEditableConfiguration()
  {
    var current = _configurationService.Current;

    EditableUiDataPath = current.UiDataPath;
    EditableServiceDataPath = current.ServiceDataPath;
    EditableSqliteDatabasePath = current.SqliteDatabasePath;
    EditableDatabaseProvider = current.DatabaseProvider;
    EditableDefaultRepoOwner = current.CurrentAresRepo.Owner;
    EditableDefaultRepoName = current.CurrentAresRepo.Repo;
    EditableGitToken = current.GitToken;

    AvailableRepositories.Clear();
    foreach (var repo in current.AvailableAresRepos)
    {
      AvailableRepositories.Add(new AresSourceEditorViewModel(repo.Owner, repo.Repo));
    }

    SelectedAvailableRepository = AvailableRepositories.FirstOrDefault();
  }
}
