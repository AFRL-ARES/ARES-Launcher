using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ARESLauncher.Models;
using ARESLauncher.Services;
using ARESLauncher.Tools;
using ARESLauncher.ViewModels.Misc;
using NuGet.Versioning;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ARESLauncher.ViewModels;

public partial class MainViewModel : ViewModelBase
{
  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly IAresStarter _aresStarter;
  private readonly IAppSettingsUpdater _appSettingsUpdater;
  private readonly ICertificateManager _certificateManager;
  private readonly IAresUpdater _aresUpdater;
  private readonly IDatabaseManager _databaseManager;

  private readonly ObservableAsPropertyHelper<bool> _updateAvailable;
  private readonly ObservableAsPropertyHelper<bool> _aresRunning;
  private readonly ObservableAsPropertyHelper<string> _buttonText;
  private readonly ObservableAsPropertyHelper<AresState> _aresState;
  private readonly ObservableAsPropertyHelper<IReactiveCommand?> _buttonCommand;

  public MainViewModel(ConfigurationOverviewViewModel overview,
    ConfigurationEditorViewModel editor,
    IAresBinaryManager aresBinaryManager,
    IAresStarter aresStarter,
    IAppSettingsUpdater appSettingsUpdater,
    ICertificateManager certificateManager,
    IAresUpdater aresUpdater,
    IDatabaseManager databaseManager)
  {
    Overview = overview ?? throw new ArgumentNullException(nameof(overview));
    Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    _aresBinaryManager = aresBinaryManager;
    _aresStarter = aresStarter;
    _appSettingsUpdater = appSettingsUpdater;
    _certificateManager = certificateManager;
    _aresUpdater = aresUpdater;
    _databaseManager = databaseManager;
    Editor.ConfigurationSaved += OnConfigurationSaved;

    UpdateDatabaseCommand = ReactiveCommand.CreateFromTask(UpdateDb);
    StartAresCommand = ReactiveCommand.Create(aresStarter.Start);
    StopAresCommand = ReactiveCommand.CreateFromTask(aresStarter.Stop);
    UpdateAresCommand = ReactiveCommand.CreateFromTask(UpdateAres);

    _updateAvailable = this
      .WhenAnyValue(x => x.AvailableVersions)
      .Select(av => av is not null && _aresBinaryManager.CurrentVersion is not null && !_aresBinaryManager.CurrentVersion.IsGreatest(av))
      .ToProperty(this, vm => vm.UpdateAvailable);

    _aresRunning = _aresStarter.AresRunning.ToProperty(this, vm => vm.AresRunning);

    _aresState = this.WhenAnyValue(
      vm => vm.AresRunning,
      vm => vm.AresPresent,
      vm => vm.DatabaseStatus,
      (isRunning, isPresent, dbStatus) =>
      {
        if(isRunning)
        {
          return AresState.Running;
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
        AresState.Running => "Stop",
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
        AresState.Running => StopAresCommand,
        AresState.Ready => StartAresCommand,
        AresState.NeedsDbUpdate => UpdateDatabaseCommand,
        AresState.NeedsInstall => UpdateAresCommand,
        AresState.Updating => null,
        _ => throw new NotImplementedException(),
      }).ToProperty(this, vm => vm.ButtonCommand);

    _aresUpdater.UpdateStepDescription.ToProperty(this, vm => vm.UpdateStepDescription);
    _aresUpdater.CurrentUpdateStep.ToProperty(this, vm => vm.CurrentStep);
    _aresUpdater.UpdateProgress.ToProperty(this, vm => vm.Progress);



    aresStarter.AresRunning.ToProperty(this, vm => vm.AresRunning);

    RefreshCommand = ReactiveCommand.CreateFromTask(CheckAresCondition);

    RefreshCommand.Execute();
  }

  [Reactive]
  public partial bool AresConditionChecked { get; private set; }

  [Reactive]
  public partial bool ButtonEnabled { get; private set; }

  public AresState AresState => _aresState.Value;

  public IReactiveCommand? ButtonCommand => _buttonCommand.Value;

  public string ButtonText => _buttonText.Value;

  [Reactive]
  public partial MainButtonState ButtonState { get; private set; }

  public ConfigurationOverviewViewModel Overview { get; }
  public ConfigurationEditorViewModel Editor { get; }

  private async Task CheckAresCondition()
  {
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
  }

  [Reactive]
  public partial string? Error { get; private set; }

  [Reactive]
  public partial string? UpdateStepDescription { get; private set; }

  [Reactive]
  public partial UpdateStep CurrentStep { get; private set; }

  [Reactive]
  public partial double Progress { get; private set; }

  [Reactive]
  public partial bool AresPresent { get; private set; }

  [Reactive]
  public partial DatabaseStatus DatabaseStatus { get; private set; }

  public bool AresRunning => _aresRunning.Value;

  public bool UpdateAvailable => _updateAvailable.Value;

  [Reactive]
  public partial SemanticVersion[]? AvailableVersions { get; private set; }

  public ReactiveCommand<Unit, Unit> StartAresCommand { get; }

  public ReactiveCommand<Unit, Unit> StopAresCommand { get; }

  public ReactiveCommand<Unit, Unit> UpdateDatabaseCommand { get; }

  public ReactiveCommand<Unit, Unit> UpdateAresCommand { get; }

  public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

  private void OnConfigurationSaved(object? sender, EventArgs e)
  {
    Overview.Refresh();
  }
}
