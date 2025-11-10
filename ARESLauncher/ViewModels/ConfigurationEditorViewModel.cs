using ARESLauncher.Models;
using ARESLauncher.Services;
using ARESLauncher.Services.Configuration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace ARESLauncher.ViewModels;

public partial class ConfigurationEditorViewModel : ViewModelBase
{
  private readonly IAppConfigurationService _configurationService;
  private readonly IAppSettingsUpdater _appSettingsUpdater;
  private readonly IReadOnlyList<DatabaseProvider> _databaseProviders;

  public ConfigurationEditorViewModel(IAppConfigurationService configurationService, IAppSettingsUpdater appSettingsUpdater)
  {
    _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    _appSettingsUpdater = appSettingsUpdater;
    _databaseProviders = Enum.GetValues<DatabaseProvider>();

    AvailableRepositories = new ObservableCollection<AresSourceEditorViewModel>();
    EditableUiBinaryPath = string.Empty;
    EditableServiceBinaryPath = string.Empty;
    EditableSqliteDatabasePath = string.Empty;
    EditableSqlServerConnectionString = string.Empty;
    EditablePostgresConnectionString = string.Empty;
    EditableServiceEndpoint = string.Empty;
    EditableUiEndpoint = string.Empty;
    EditableGitToken = string.Empty;
    EditableAresDataPath = string.Empty;
    EditableAresServiceProcessName = string.Empty;
    EditableAresUiProcessName = string.Empty;
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

  [Reactive]
  public partial AresSourceEditorViewModel? SelectedAvailableRepository { get; set; }

  [Reactive]
  public partial AresSourceEditorViewModel? SelectedCurrentRepository { get; set; }

  [Reactive]
  public partial string EditableUiBinaryPath { get; set; }

  [Reactive]
  public partial string EditableServiceBinaryPath { get; set; }

  [Reactive]
  public partial string EditableSqliteDatabasePath { get; set; }

  [Reactive]
  public partial string EditableSqlServerConnectionString { get; set; }

  [Reactive]
  public partial string EditablePostgresConnectionString { get; set; }

  [Reactive]
  public partial string EditableServiceEndpoint { get; set; }

  [Reactive]
  public partial string EditableUiEndpoint { get; set; }

  [Reactive]
  public partial DatabaseProvider EditableDatabaseProvider { get; set; }

  [Reactive]
  public partial string EditableGitToken { get; set; }

  [Reactive]
  public partial string EditableAresDataPath { get; set; }

  [Reactive]
  public partial string EditableAresServiceProcessName { get; set; }

  [Reactive]
  public partial string EditableAresUiProcessName { get; set; }

  [Reactive]
  public partial bool ShowAdvancedOptions { get; set; }

  public ReactiveCommand<Unit, Unit> AddRepositoryCommand { get; }

  public ReactiveCommand<Unit, Unit> RemoveSelectedRepositoryCommand { get; }

  public ReactiveCommand<Unit, Unit> SaveConfigurationCommand { get; }

  public ReactiveCommand<Unit, Unit> ResetConfigurationCommand { get; }

  private void AddRepository()
  {
    var newRepository = new AresSourceEditorViewModel(string.Empty, string.Empty);
    AvailableRepositories.Add(newRepository);
    SelectedAvailableRepository = newRepository;

    SelectedCurrentRepository ??= newRepository;
  }

  private void RemoveSelectedRepository()
  {
    if(SelectedAvailableRepository is null)
    {
      return;
    }

    var repositoryToRemove = SelectedAvailableRepository;
    var index = AvailableRepositories.IndexOf(repositoryToRemove);
    AvailableRepositories.Remove(repositoryToRemove);

    if(repositoryToRemove == SelectedCurrentRepository)
    {
      SelectedCurrentRepository = AvailableRepositories.FirstOrDefault();
    }

    if(AvailableRepositories.Count == 0)
    {
      SelectedAvailableRepository = null;
      SelectedCurrentRepository = null;
      return;
    }

    index = Math.Clamp(index, 0, AvailableRepositories.Count - 1);
    SelectedAvailableRepository = AvailableRepositories[index];
    SelectedCurrentRepository ??= AvailableRepositories.FirstOrDefault();
  }

  private void SaveConfiguration()
  {
    _configurationService.Update(configuration =>
    {
      configuration.UiBinaryPath = EditableUiBinaryPath;
      configuration.ServiceBinaryPath = EditableServiceBinaryPath;
      configuration.SqliteDatabasePath = EditableSqliteDatabasePath;
      configuration.SqlServerConnectionString = EditableSqlServerConnectionString;
      configuration.PostgresConnectionString = EditablePostgresConnectionString;
      configuration.DatabaseProvider = EditableDatabaseProvider;
      configuration.ServiceEndpoint = EditableServiceEndpoint;
      configuration.UiEndpoint = EditableUiEndpoint;
      configuration.GitToken = EditableGitToken;
      configuration.AresDataPath = EditableAresDataPath;
      configuration.AresServiceProcessName = EditableAresServiceProcessName;
      configuration.AresUiProcessName = EditableAresUiProcessName;

      var validRepositories = AvailableRepositories
        .Where(IsValidRepository)
        .Select(repo => repo.ToAresSource())
        .ToArray();

      configuration.AvailableAresRepos = validRepositories;

      if(validRepositories.Length > 0)
      {
        if(SelectedCurrentRepository is not null && IsValidRepository(SelectedCurrentRepository))
        {
          configuration.CurrentAresRepo = SelectedCurrentRepository.ToAresSource();
        }
        else
        {
          configuration.CurrentAresRepo = validRepositories[0];
        }
      }
    });

    LoadEditableConfiguration();
    ConfigurationSaved?.Invoke(this, EventArgs.Empty);
    _appSettingsUpdater.UpdateAll();
  }

  private void ResetEditableConfiguration()
  {
    LoadEditableConfiguration();
  }

  private void LoadEditableConfiguration()
  {
    var current = _configurationService.Current;

    EditableUiBinaryPath = current.UiBinaryPath;
    EditableServiceBinaryPath = current.ServiceBinaryPath;
    EditableSqliteDatabasePath = current.SqliteDatabasePath;
    EditableSqlServerConnectionString = current.SqlServerConnectionString;
    EditablePostgresConnectionString = current.PostgresConnectionString;
    EditableDatabaseProvider = current.DatabaseProvider;
    EditableServiceEndpoint = current.ServiceEndpoint;
    EditableUiEndpoint = current.UiEndpoint;
    EditableGitToken = current.GitToken;
    EditableAresDataPath = current.AresDataPath;
    EditableAresServiceProcessName = current.AresServiceProcessName;
    EditableAresUiProcessName = current.AresUiProcessName;

    AvailableRepositories.Clear();
    foreach(var repo in current.AvailableAresRepos)
    {
      AvailableRepositories.Add(new AresSourceEditorViewModel(repo.Owner, repo.Repo, repo.Bundle));
    }

    SelectedAvailableRepository = AvailableRepositories.FirstOrDefault();
    SelectedCurrentRepository = AvailableRepositories.FirstOrDefault(repo =>
      string.Equals(repo.Owner, current.CurrentAresRepo.Owner, StringComparison.OrdinalIgnoreCase) &&
      string.Equals(repo.Repo, current.CurrentAresRepo.Repo, StringComparison.OrdinalIgnoreCase))
      ?? AvailableRepositories.FirstOrDefault();
  }

  private static bool IsValidRepository(AresSourceEditorViewModel repo)
  {
    return !string.IsNullOrWhiteSpace(repo.Owner) && !string.IsNullOrWhiteSpace(repo.Repo);
  }
}
