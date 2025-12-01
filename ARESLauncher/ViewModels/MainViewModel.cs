using ARESLauncher.Models;
using ARESLauncher.Services;
using ARESLauncher.Tools;
using NuGet.Versioning;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ARESLauncher.ViewModels;

public partial class MainViewModel : ViewModelBase
{
  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly IAresStarter _aresStarter;
  private readonly IAppSettingsUpdater _appSettingsUpdater;
  private readonly ICertificateManager _certificateManager;
  private readonly IAresUpdater _aresUpdater;
  private readonly IDatabaseManager _databaseManager;
  private readonly IConflictManager _conflictManager;
  private readonly ObservableAsPropertyHelper<bool> _updateAvailable;
  private readonly ObservableAsPropertyHelper<int> _aresComponentsRunning;
  private readonly ObservableAsPropertyHelper<AresState> _aresState;
  private readonly ObservableAsPropertyHelper<string> _buttonText;
  private readonly ObservableAsPropertyHelper<IReactiveCommand?> _buttonCommand;
  private readonly ObservableAsPropertyHelper<object?> _auxButtonContent;
  private readonly ObservableAsPropertyHelper<IReactiveCommand?> _auxButtonCommand;
  private readonly ObservableAsPropertyHelper<string> _aresStateDescription;
  private readonly ObservableAsPropertyHelper<bool> _updateInProgress;
  private readonly ObservableAsPropertyHelper<string?> _updateStepDescription;
  private readonly ObservableAsPropertyHelper<UpdateStep> _currentUpdateStep;
  private readonly ObservableAsPropertyHelper<double> _progress;
  private readonly ObservableAsPropertyHelper<bool> _showProgressBar;
  private readonly ObservableAsPropertyHelper<bool> _launcherReady;

  public MainViewModel(ConfigurationOverviewViewModel overview,
    ConfigurationEditorViewModel editor,
    IAresBinaryManager aresBinaryManager,
    IAresStarter aresStarter,
    IAppSettingsUpdater appSettingsUpdater,
    ICertificateManager certificateManager,
    IAresUpdater aresUpdater,
    IDatabaseManager databaseManager,
    IBrowserOpener browserOpener,
    IConflictManager conflictManager)
  {
    Overview = overview ?? throw new ArgumentNullException(nameof(overview));
    Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    _aresBinaryManager = aresBinaryManager;
    _aresStarter = aresStarter;
    _appSettingsUpdater = appSettingsUpdater;
    _certificateManager = certificateManager;
    _aresUpdater = aresUpdater;
    _databaseManager = databaseManager;
    _conflictManager = conflictManager;
    Editor.ConfigurationSaved += OnConfigurationSaved;

    UpdateDatabaseCommand = ReactiveCommand.CreateFromTask(UpdateDb);
    StartAresCommand = ReactiveCommand.CreateFromTask(async () =>
    {
      aresStarter.Start();
      await Task.Delay(TimeSpan.FromSeconds(1));
      browserOpener.Open();
    });
    StopAresCommand = ReactiveCommand.CreateFromTask(aresStarter.Stop);
    UpdateAresCommand = ReactiveCommand.CreateFromTask(UpdateAres);
    OpenBrowserCommand = ReactiveCommand.Create(browserOpener.Open);
    ConflictDialog = new Interaction<Unit, Unit>();
    ResolveConflictsCommand = ReactiveCommand.CreateFromTask(async () =>
    {
      ConflictsResolved = false;
      var uiExists = conflictManager.FindPotentialUi();
      var serviceExists = conflictManager.FindPotentialService();
      var conflict = uiExists || serviceExists;
      if(!conflict)
      {
        ConflictsResolved = true;
        return;
      }

      await ConflictDialog.Handle(Unit.Default);
      ConflictsResolved = true;
    });

    _updateStepDescription = _aresUpdater.UpdateStepDescription.ToProperty(this, vm => vm.UpdateStepDescription);
    _currentUpdateStep = _aresUpdater.CurrentUpdateStep.ToProperty(this, vm => vm.CurrentUpdateStep);
    _progress = _aresUpdater.UpdateProgress.ToProperty(this, vm => vm.Progress);

    _aresComponentsRunning = _aresStarter
      .AresUiRunning
      .CombineLatest(_aresStarter.AresServiceRunning, (ui, service) => (ui ? 1 : 0) + (service ? 1 : 0))
      .ToProperty(this, vm => vm.AresComponentsRunning);

    _updateAvailable = this
      .WhenAnyValue(x => x.AvailableVersions, x => x.AresComponentsRunning, x => x.CurrentUpdateStep, (av, runnin, updateStep) =>
      {
        var updateAvailable = av is not null && _aresBinaryManager.CurrentVersion is not null && !_aresBinaryManager.CurrentVersion.IsGreatest(av);
        return updateAvailable && runnin == 0 && updateStep == UpdateStep.Idle;
      })
      .ToProperty(this, vm => vm.UpdateAvailable);

    _aresState = this.WhenAnyValue(
      vm => vm.AresComponentsRunning,
      vm => vm.AresPresent,
      vm => vm.DatabaseStatus,
      vm => vm.CurrentUpdateStep,
      (isRunning, isPresent, dbStatus, updootStep) =>
      {
        if(updootStep != UpdateStep.Idle)
        {
          return AresState.Updating;
        }

        if(isRunning == 1)
        {
          return AresState.OneRunning;
        }
        if(isRunning == 2)
        {
          return AresState.BothRunning;
        }

        if(!isPresent)
        {
          return AresState.NeedsInstall;
        }

        if(dbStatus != DatabaseStatus.UpToDate)
        {
          return AresState.NeedsDbUpdate;
        }

        return AresState.Ready;
      }).ToProperty(this, vm => vm.AresState);


    _buttonText = this
      .WhenAnyValue(vm => vm.AresState)
      .Select(s => s switch
      {
        AresState.Unknown => ":)",
        AresState.OneRunning => "Start",
        AresState.BothRunning => "Stop",
        AresState.Ready => "Start",
        AresState.NeedsDbUpdate => "Update DB",
        AresState.NeedsInstall => "Install",
        AresState.Updating => "Updating...",
        _ => throw new NotImplementedException()
      }).ToProperty(this, vm => vm.ButtonText);

    _buttonCommand = this
      .WhenAnyValue(vm => vm.AresState)
      .Select(s => s switch
      {
        AresState.Unknown => null,
        AresState.OneRunning => StartAresCommand,
        AresState.BothRunning => StopAresCommand,
        AresState.Ready => StartAresCommand,
        AresState.NeedsDbUpdate => UpdateDatabaseCommand,
        AresState.NeedsInstall => UpdateAresCommand,
        AresState.Updating => null,
        _ => throw new NotImplementedException(),
      }).ToProperty(this, vm => vm.ButtonCommand);

    _auxButtonContent = this
      .WhenAnyValue(vm => vm.AresState)
      .Select(s => s switch
      {
        AresState.Unknown => null,
        AresState.OneRunning => "Stop",
        AresState.BothRunning => "Globe",
        AresState.Ready => null,
        AresState.NeedsDbUpdate => null,
        AresState.NeedsInstall => null,
        AresState.Updating => null,
        _ => throw new NotImplementedException(),
      }).ToProperty(this, vm => vm.AuxButtonContent);

    _auxButtonCommand = this
      .WhenAnyValue(vm => vm.AresState)
      .Select(s => s switch
      {
        AresState.Unknown => null,
        AresState.OneRunning => StopAresCommand,
        AresState.BothRunning => OpenBrowserCommand,
        AresState.Ready => null,
        AresState.NeedsDbUpdate => null,
        AresState.NeedsInstall => null,
        AresState.Updating => null,
        _ => throw new NotImplementedException(),
      }).ToProperty(this, vm => vm.AuxButtonCommand);

    _aresStateDescription = this
      .WhenAnyValue(vm => vm.AresState)
      .Select(s => s switch
      {
        AresState.Unknown => "Launcher in a weird state, not sure why",
        AresState.OneRunning => "One component is currently running. You can either stop the current one, or start the other",
        AresState.BothRunning => "ARES is running",
        AresState.Ready => "ARES is ready",
        AresState.NeedsDbUpdate => "Database out of date",
        AresState.NeedsInstall => "Ready to install",
        AresState.Updating => "Update in progress",
        _ => throw new NotImplementedException(),
      }).ToProperty(this, vm => vm.AresStateDescription);

    _updateInProgress = this
      .WhenAnyValue(vm => vm.CurrentUpdateStep)
      .Select(s => s switch
      {
        UpdateStep.Idle => false,
        UpdateStep.Downloading => true,
        UpdateStep.Other => true,
        _ => throw new NotImplementedException(),
      }).ToProperty(this, vm => vm.UpdateInProgress);

    _showProgressBar = this
      .WhenAnyValue(vm => vm.CurrentUpdateStep)
      .Select(s => s == UpdateStep.Downloading)
      .ToProperty(this, vm => vm.ShowProgressBar);

    _launcherReady = this
      .WhenAnyValue(vm => vm.AresConditionChecked, vm => vm.ConflictsResolved, vm => vm.AresState, (chk, resolved, state) => chk && resolved && state != AresState.Updating)
      .ToProperty(this, vm => vm.LauncherReady);

    RefreshCommand = ReactiveCommand.CreateFromTask(CheckAresCondition);
    RefreshCommand.Execute();
  }

  [Reactive]
  public partial bool AresConditionChecked { get; private set; }

  [Reactive]
  public partial bool ConflictsResolved { get; private set; }

  [Reactive]
  public partial bool ButtonEnabled { get; private set; }

  public AresState AresState => _aresState.Value;

  public IReactiveCommand? ButtonCommand => _buttonCommand.Value;

  public string ButtonText => _buttonText.Value;

  public IReactiveCommand? AuxButtonCommand => _auxButtonCommand.Value;

  public object? AuxButtonContent => _auxButtonContent.Value;

  public bool LauncherReady => _launcherReady.Value;

  public ConfigurationOverviewViewModel Overview { get; }
  public ConfigurationEditorViewModel Editor { get; }

  public ConflictResolutionDialogViewModel GetConflictResolutionDialogViewModel()
  {
    return new ConflictResolutionDialogViewModel(_conflictManager);
  }

  private async Task CheckAresCondition()
  {
    AresConditionChecked = false;

    await _aresBinaryManager.Refresh();
    AresPresent = _aresBinaryManager.CurrentVersion is not null;
    if(!AresPresent)
    {
      AresConditionChecked = true;
      return;
    }

    AvailableVersions = await _aresUpdater.GetAvailableVersions();

    await _databaseManager.Refresh();
    DatabaseStatus = _databaseManager.DatabaseStatus;
    if(DatabaseStatus != DatabaseStatus.UpToDate)
    {
      AresConditionChecked = true;
      return;
    }

    AresConditionChecked = true;
  }

  private async Task UpdateAres()
  {
    try
    {
      Error = "";
      await _aresUpdater.UpdateLatest();
    }
    catch(Exception e)
    {
      Error = e.Message;
    }
    finally
    {
      await CheckAresCondition();
    }
  }

  private async Task UpdateDb()
  {
    try
    {
      Error = "";
      await _databaseManager.RunMigrations();
    }
    catch(Exception e)
    {
      Error = e.Message;
    }
    finally
    {
      await CheckAresCondition();
    }
  }

  [Reactive]
  public partial string? Error { get; private set; }

  public string? UpdateStepDescription => _updateStepDescription.Value;

  public UpdateStep CurrentUpdateStep => _currentUpdateStep.Value;

  public double Progress => _progress.Value;

  [Reactive]
  public partial bool AresPresent { get; private set; }

  [Reactive]
  public partial DatabaseStatus DatabaseStatus { get; private set; }

  public string AresStateDescription => _aresStateDescription.Value;

  public int AresComponentsRunning => _aresComponentsRunning.Value;

  public bool UpdateInProgress => _updateInProgress.Value;

  public bool UpdateAvailable => _updateAvailable.Value;

  [Reactive]
  public partial SemanticVersion[]? AvailableVersions { get; private set; }

  public ReactiveCommand<Unit, Unit> StartAresCommand { get; }

  public ReactiveCommand<Unit, Unit> StopAresCommand { get; }

  public ReactiveCommand<Unit, Unit> UpdateDatabaseCommand { get; }

  public ReactiveCommand<Unit, Unit> UpdateAresCommand { get; }

  public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

  public ReactiveCommand<Unit, Unit> OpenBrowserCommand { get; }

  public ReactiveCommand<Unit, Unit> ResolveConflictsCommand { get; }

  public Interaction<Unit, Unit> ConflictDialog { get; }
  public bool ShowProgressBar => _showProgressBar.Value;

  private void OnConfigurationSaved(object? sender, EventArgs e)
  {
    Overview.Refresh();
    _ = CheckAresCondition();
  }
}
