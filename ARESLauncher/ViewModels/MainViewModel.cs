using ARESLauncher.Models;
using ARESLauncher.Models.PyAres;
using ARESLauncher.Services;
using ARESLauncher.Tools;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using NuGet.Versioning;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ARESLauncher.ViewModels;

public partial class MainViewModel : ViewModelBase
{
  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly ObservableAsPropertyHelper<int> _aresComponentsRunning;
  private readonly IAresStarter _aresStarter;
  private readonly ObservableAsPropertyHelper<AresState> _aresState;
  private readonly ObservableAsPropertyHelper<string> _aresStateDescription;
  private readonly IAresUpdater _aresUpdater;
  private readonly ILauncherUpdater _launcherUpdater;
  private readonly ObservableAsPropertyHelper<IReactiveCommand?> _auxButtonCommand;
  private readonly ObservableAsPropertyHelper<object?> _auxButtonContent;
  private readonly ObservableAsPropertyHelper<IReactiveCommand?> _buttonCommand;
  private readonly ObservableAsPropertyHelper<string> _buttonText;
  private readonly IConflictManager _conflictManager;
  private readonly IPyAresManager _pyAresManager;
  private readonly ObservableAsPropertyHelper<UpdateStep> _currentUpdateStep;
  private readonly IDatabaseManager _databaseManager;
  private readonly ObservableAsPropertyHelper<bool> _launcherReady;
  private readonly ObservableAsPropertyHelper<double> _progress;
  private readonly ObservableAsPropertyHelper<bool> _showProgressBar;
  private readonly ObservableAsPropertyHelper<bool> _updateAvailable;
  private readonly ObservableAsPropertyHelper<bool> _launcherUpdateAvailable;
  private readonly ObservableAsPropertyHelper<bool> _updateInProgress;
  private readonly ObservableAsPropertyHelper<bool> _showDisclaimer;
  private readonly ObservableAsPropertyHelper<string?> _updateStepDescription;
  private readonly bool _isMac = false;

  public MainViewModel(ConfigurationOverviewViewModel overview,
    ConfigurationEditorViewModel editor,
    IAresBinaryManager aresBinaryManager,
    IAresStarter aresStarter,
    IAppSettingsUpdater appSettingsUpdater,
    IAresUpdater aresUpdater,
    ILauncherUpdater launcherUpdater,
    IDatabaseManager databaseManager,
    IBrowserOpener browserOpener,
    IConflictManager conflictManager,
    IPyAresManager pyAresManager, 
    PyAresConfigurationViewModel pyAresConfigurationViewModel)
  {
    AvailableAresVersions = [];
    Overview = overview ?? throw new ArgumentNullException(nameof(overview));
    Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    _aresBinaryManager = aresBinaryManager;
    _aresStarter = aresStarter;
    _aresUpdater = aresUpdater;
    _launcherUpdater = launcherUpdater;
    _databaseManager = databaseManager;
    _conflictManager = conflictManager;
    _pyAresManager = pyAresManager;
    PyAresConfig = pyAresConfigurationViewModel;
    _isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    InstalledAresVersion = string.Empty;
    AvailableAresUpdateVersion = string.Empty;
    Editor.ConfigurationSaved += OnConfigurationSaved;

    UpdateDatabaseCommand = ReactiveCommand.CreateFromTask(UpdateDb);
    StartAresCommand = ReactiveCommand.CreateFromTask(async () =>
    {
      _aresStarter.Start();
      await _pyAresManager.StartAll();
      await Task.Delay(TimeSpan.FromSeconds(1));
      browserOpener.Open();
    });
    StopAresCommand = ReactiveCommand.CreateFromTask(async () =>
    {
      await _pyAresManager.StopAll();
      await _aresStarter.Stop();
    });
    UpdateAresCommand = ReactiveCommand.CreateFromTask(UpdateAres);
    OpenBrowserCommand = ReactiveCommand.Create(browserOpener.Open);
    OpenLauncherReleasePageCommand = ReactiveCommand.CreateFromTask(CheckForUpdatedLauncher);
    UpdateConfirmationDialog = new Interaction<UpdateConfirmationRequest, UpdateConfirmationResponse>();
    ConflictDialog = new Interaction<Unit, Unit>();
    PyAresOrphansDialog = new Interaction<IReadOnlyList<PyAresProcessInfo>, bool>();
    ResolveConflictsCommand = ReactiveCommand.CreateFromTask(async () =>
    {
      ConflictsResolved = false;
      bool uiExists = conflictManager.FindPotentialUi();
      bool serviceExists = conflictManager.FindPotentialService();
      bool conflict = uiExists || serviceExists;
      if(!conflict)
      {
        ConflictsResolved = true;

      // After resolving ARES conflicts, handle any orphaned PyAres processes
      var orphaned = await _pyAresManager.GetOrphanedProcessesAsync();
      if(orphaned.Count > 0)
      {
        var stopAll = await PyAresOrphansDialog.Handle(orphaned);
        if(stopAll)
          await _pyAresManager.StopOrphanedProcessesAsync();
        else
          await _pyAresManager.AttachExistingProcessesAsync();
      }
        return;
      }

      await ConflictDialog.Handle(Unit.Default);

      if(!conflictManager.IsCurrentUserProcessOwner)
        ProcessOwnerConflict = !conflictManager.IsCurrentUserProcessOwner;

      ConflictsResolved = true;
    });

    _updateStepDescription = _aresUpdater.UpdateStepDescription.ToProperty(this, vm => vm.UpdateStepDescription);
    _currentUpdateStep = _aresUpdater.CurrentUpdateStep.ToProperty(this, vm => vm.CurrentUpdateStep);
    _progress = _aresUpdater.UpdateProgress.ToProperty(this, vm => vm.Progress);

    this.WhenAnyValue(x => x.Editor.UpdateInProgress)
      .Skip(1)
      .Subscribe((bool inProgress) => 
      {
        if(inProgress == false)
          _ = this.CheckAresCondition();
      });

    _aresComponentsRunning = _aresStarter
      .AresUiRunning
      .CombineLatest(_aresStarter.AresServiceRunning, (ui, service) => (ui ? 1 : 0) + (service ? 1 : 0))
      .ToProperty(this, vm => vm.AresComponentsRunning);

    _updateAvailable = this
      .WhenAnyValue(x => x.AvailableAresUpdateVersion, x => x.AresComponentsRunning, x => x.CurrentUpdateStep, x => x.InstalledAresVersion, (av, runnin, updateStep, installedAresVersion) =>
      {
        if(string.IsNullOrEmpty(av) || _aresBinaryManager.CurrentVersion is null)
          return false;

        if(!SemanticVersion.TryParse(av, out var latest))
          return false;

        bool updateAvailable = latest > _aresBinaryManager.CurrentVersion;
        return updateAvailable && runnin == 0 && updateStep == UpdateStep.Idle;
      })
      .ToProperty(this, vm => vm.UpdateAvailable);


    _launcherUpdateAvailable = this
      .WhenAnyValue(x => x.AvailableLauncherVersions, (av) =>
      {
        var currentLauncherVersion = LauncherVersionHelper.GetLauncherVersion();
        var hasCurrentVersion = SemanticVersion.TryParse(currentLauncherVersion, out var currentSemantic);
        bool launcherUpdateAvailable = av is not null && hasCurrentVersion && currentSemantic!.IsGreatest(av) is false;
        return launcherUpdateAvailable;
      })
      .ToProperty(this, ViewModels => ViewModels.LauncherUpdateAvailable);
    
      _aresState = this.WhenAnyValue(
      vm => vm.AresComponentsRunning,
      vm => vm.AresPresent,
      vm => vm.DatabaseStatus,
      vm => vm.CurrentUpdateStep,
      vm => vm.ProcessOwnerConflict,
      (isRunning, isPresent, dbStatus, updateStep, processOwnerConflict) =>
      {
        if(updateStep != UpdateStep.Idle)
        {
          return AresState.Updating;
        }

        var layout = _aresBinaryManager.CurrentLayout;
        var fullyRunning = layout == AresReleaseLayout.UnifiedUiOnly ? isRunning >= 1 : isRunning == 2;
        var partiallyRunning = layout == AresReleaseLayout.SplitUiAndService && isRunning == 1;

        if(partiallyRunning)
          return AresState.OneRunning;
        
        if(fullyRunning)
          return AresState.BothRunning;

        if(!isPresent)
          return AresState.NeedsInstall;

        if(dbStatus != DatabaseStatus.UpToDate)
          return AresState.NeedsDbUpdate;

        if(processOwnerConflict)
          return AresState.ProcessOwnerConflict;

        return AresState.Ready;
      }).ToProperty(this, vm => vm.AresState);


    _buttonText = this
      .WhenAnyValue(vm => vm.AresState, (s) => 
      {
          return s switch
          {
            AresState.Unknown => ":)",
            AresState.OneRunning => "Start",
            AresState.BothRunning => "Stop",
            AresState.Ready => "Start",
            AresState.NeedsDbUpdate => "Update DB",
            AresState.NeedsInstall => "Install",
            AresState.Updating => "Updating...",
            AresState.ProcessOwnerConflict => "Conflict",
            _ => throw new NotImplementedException()
          };
      }).ToProperty(this, vm => vm.ButtonText);

    _buttonCommand = this
      .WhenAnyValue(vm => vm.AresState, (s) => 
      {
          return s switch
          {
            AresState.Unknown => null,
            AresState.OneRunning => StartAresCommand,
            AresState.BothRunning => StopAresCommand,
            AresState.Ready => StartAresCommand,
            AresState.NeedsDbUpdate => UpdateDatabaseCommand,
            AresState.NeedsInstall => UpdateAresCommand,
            AresState.Updating => null,
            AresState.ProcessOwnerConflict => null,
            _ => throw new NotImplementedException()
          };
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
        AresState.ProcessOwnerConflict => null,
        _ => throw new NotImplementedException()
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
        AresState.ProcessOwnerConflict => null,
        _ => throw new NotImplementedException()
      }).ToProperty(this, vm => vm.AuxButtonCommand);

    _aresStateDescription = this
      .WhenAnyValue(vm => vm.AresState)
      .Select(s => s switch
      {
        AresState.Unknown => "The ARES Launcher is in an unknown state, a fresh install is recommended",
        AresState.OneRunning => "One component is currently running. You can either stop the current one, or start the other",
        AresState.BothRunning => "ARES is running",
        AresState.Ready => "ARES is ready",
        AresState.NeedsDbUpdate => "Database out of date",
        AresState.NeedsInstall => "Ready to install",
        AresState.Updating => "Update in progress",
        AresState.ProcessOwnerConflict => "ARES process is owned by other user",
        _ => throw new NotImplementedException()
      }).ToProperty(this, vm => vm.AresStateDescription);

    _updateInProgress = this
      .WhenAnyValue(vm => vm.CurrentUpdateStep)
      .Select(step => step != UpdateStep.Idle)
      .ToProperty(this, vm => vm.UpdateInProgress);

    _showProgressBar = this
      .WhenAnyValue(vm => vm.CurrentUpdateStep)
      .Select(step => step == UpdateStep.Downloading)
      .ToProperty(this, vm => vm.ShowProgressBar);

    _launcherReady = this
      .WhenAnyValue(vm => vm.AresConditionChecked, vm => vm.ConflictsResolved, vm => vm.AresState, (chk, resolved, state) => chk && resolved && state != AresState.Updating)
      .ToProperty(this, vm => vm.LauncherReady);

    _showDisclaimer = this
      .WhenAnyValue(vm => vm.AresState, state => state == AresState.BothRunning && _isMac)
      .ToProperty(this, vm => vm.ShowDisclaimer);

    CheckForUpdate = ReactiveCommand.CreateFromTask(UpdateAvailableVersions);
    RefreshCommand = ReactiveCommand.CreateFromTask(CheckAresCondition);
    RefreshCommand.Execute();
  }

  private async Task UpdateAvailableVersions()
  {
    await _aresBinaryManager.Refresh();
    InstalledAresVersion = _aresBinaryManager.CurrentVersion?.ToNormalizedString() ?? string.Empty;
    AvailableAresVersions = await _aresUpdater.GetAvailableVersions();
    AvailableAresUpdateVersion = AvailableAresVersions?.FirstOrDefault()?.Version.ToNormalizedString() ?? string.Empty;
    AvailableLauncherVersions = await _launcherUpdater.GetAvailableVersions();
  }

  public ConflictResolutionDialogViewModel GetConflictResolutionDialogViewModel()
  {
    return new ConflictResolutionDialogViewModel(_conflictManager);
  }

  private async Task CheckAresCondition()
  {
    AresConditionChecked = false;

    await _aresBinaryManager.Refresh();
    InstalledAresVersion = _aresBinaryManager.CurrentVersion?.ToNormalizedString() ?? string.Empty;
    AresPresent = _aresBinaryManager.CurrentVersion is not null;

    AvailableAresVersions = await _aresUpdater.GetAvailableVersions();
    AvailableAresUpdateVersion = AvailableAresVersions?.FirstOrDefault()?.Version.ToNormalizedString() ?? string.Empty;
    AvailableLauncherVersions = await _launcherUpdater.GetAvailableVersions();

    if(!AresPresent)
    {
      AresConditionChecked = true;
      return;
    }

    await _databaseManager.Refresh();
    DatabaseStatus = _databaseManager.DatabaseStatus;
    if(DatabaseStatus != DatabaseStatus.UpToDate)
    {
      AresConditionChecked = true;
      this.RaisePropertyChanged(nameof(UpdateAvailable));
      return;
    }

    AresConditionChecked = true;
    this.RaisePropertyChanged(nameof(UpdateAvailable));
  }

  private async Task UpdateAres()
  {
    var currentVersion = _aresBinaryManager.CurrentVersion;
    AvailableAresVersions = await _aresUpdater.GetAvailableVersions();
    var latest = AvailableAresVersions.FirstOrDefault();

    if(latest is null)
      return;

    var targetVersion = latest.Version;

    if(RequiresUpdateConfirmation(currentVersion, targetVersion))
    {
      var isDowngrade = currentVersion is not null && targetVersion < currentVersion;
      var hasSnapshot = isDowngrade && await _aresUpdater.HasSnapshot(targetVersion);

      var response = await UpdateConfirmationDialog.Handle(new UpdateConfirmationRequest
      {
        CurrentVersion = currentVersion ?? targetVersion,
        TargetVersion = targetVersion,
        ReleaseNotes = latest.ReleaseNotes ?? "",
        HasSnapshot = hasSnapshot
      });

      if(!response.ShouldProceed)
        return;

      // Take snapshot of current version before doing anything
      if(currentVersion is not null)
        await _aresUpdater.CreateSnapshot(currentVersion);

      if(response.DowngradeOption == DowngradeOption.RestoreSnapshot)
        await _aresUpdater.RestoreSnapshot(targetVersion);

      else if(response.DowngradeOption == DowngradeOption.Reset)
        await _aresUpdater.ResetDatabase();

      await _aresBinaryManager.Refresh();
    }

    try
    {
      Error = "";
      await _aresUpdater.Update(targetVersion);
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

  private async Task CheckForUpdatedLauncher()
  {
    try
    {
      Error = "";
      LauncherUpdateInProgress = true;
      var updateStarted = await _launcherUpdater.UpdateLatest();
      if(!updateStarted)
      {
        LauncherUpdateInProgress = false;
        return;
      }

      if(Application.Current is App app)
        app.BeginShutdown();

      if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        desktopLifetime.Shutdown();
    }

    catch(Exception e)
    {
      LauncherUpdateInProgress = false;
      Error = e.Message;
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

  private void OnConfigurationSaved(object? sender, EventArgs e)
  {
    Overview.Refresh();
    _ = CheckAresCondition();
  }

  // Check if we should ask for confirmation. We should ask if there's a major/minor update
  // Or if it's a downgrade
  private static bool RequiresUpdateConfirmation(SemanticVersion? currentVersion, SemanticVersion? targetVersion)
  {
    if(currentVersion is null || targetVersion is null)
    {
      return false;
    }

    if(targetVersion < currentVersion)
    {
      return true;
    }

    if(targetVersion.Major > currentVersion.Major)
    {
      return true;
    }

    return targetVersion.Major == currentVersion.Major && targetVersion.Minor > currentVersion.Minor;
  }

  [Reactive]
  public partial bool AresConditionChecked { get; private set; }

  [Reactive]
  public partial bool ConflictsResolved { get; private set; }

  [Reactive]
  public partial bool ButtonEnabled { get; private set; }

  [Reactive]
  public partial bool LauncherUpdateInProgress { get; private set; }

  [Reactive]
  public partial bool ProcessOwnerConflict { get; private set; }

  public AresState AresState => _aresState.Value;

  public IReactiveCommand? ButtonCommand => _buttonCommand.Value;

  public string ButtonText => _buttonText.Value;

  public IReactiveCommand? AuxButtonCommand => _auxButtonCommand.Value;

  public object? AuxButtonContent => _auxButtonContent.Value;

  public bool LauncherReady => _launcherReady.Value;

  public ConfigurationOverviewViewModel Overview { get; }
  public ConfigurationEditorViewModel Editor { get; }

  [Reactive]
  public partial string? Error { get; private set; }

  public bool ShowDisclaimer => _showDisclaimer.Value;

  public string? UpdateStepDescription => _updateStepDescription.Value;

  public UpdateStep CurrentUpdateStep => _currentUpdateStep.Value;

  public double Progress => _progress.Value;

  [Reactive]
  public partial bool AresPresent { get; private set; }

  [Reactive]
  public partial DatabaseStatus DatabaseStatus { get; private set; }

  [Reactive]
  public partial string InstalledAresVersion { get; private set; }

  [Reactive]
  public partial string AvailableAresUpdateVersion { get; private set; }

  public string AresStateDescription => _aresStateDescription.Value;

  public int AresComponentsRunning => _aresComponentsRunning.Value;

  public bool UpdateInProgress => _updateInProgress.Value;

  public bool UpdateAvailable => _updateAvailable.Value; 

  public bool LauncherUpdateAvailable => _launcherUpdateAvailable.Value;

  public AresRelease[] AvailableAresVersions { get; set; }

  [Reactive]
  public partial SemanticVersion[]? AvailableLauncherVersions { get; private set; }

  public ReactiveCommand<Unit, Unit> StartAresCommand { get; }

  public ReactiveCommand<Unit, Unit> StopAresCommand { get; }

  public ReactiveCommand<Unit, Unit> UpdateDatabaseCommand { get; }

  public ReactiveCommand<Unit, Unit> UpdateAresCommand { get; }

  public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

  public ReactiveCommand<Unit, Unit> OpenBrowserCommand { get; }

  public ReactiveCommand<Unit, Unit> OpenLauncherReleasePageCommand { get; }

  public ReactiveCommand<Unit, Unit> ResolveConflictsCommand { get; }

  public ReactiveCommand<Unit, Unit> CheckForUpdate { get; }

  public Interaction<Unit, Unit> ConflictDialog { get; }

  public Interaction<IReadOnlyList<PyAresProcessInfo>, bool> PyAresOrphansDialog { get; }

  public Interaction<UpdateConfirmationRequest, UpdateConfirmationResponse> UpdateConfirmationDialog { get; }

  public bool ShowProgressBar => _showProgressBar.Value;

  public PyAresConfigurationViewModel PyAresConfig { get; }
}
