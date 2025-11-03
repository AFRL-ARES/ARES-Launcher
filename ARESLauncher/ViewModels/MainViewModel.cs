using System;
using System.Reactive;
using System.Threading.Tasks;
using ARESLauncher.Models;
using ARESLauncher.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ARESLauncher.ViewModels;

public partial class MainViewModel : ViewModelBase
{
  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly IAppSettingsUpdater _appSettingsUpdater;
  private readonly ICertificateManager _certificateManager;
  private readonly IAresUpdater _aresUpdater;
  private readonly IDatabaseManager _databaseManager;

  //[Reactive(SetModifier = AccessModifier.Private)]
  //private bool _aresPresent = false;

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
    _appSettingsUpdater = appSettingsUpdater;
    _certificateManager = certificateManager;
    _aresUpdater = aresUpdater;
    _databaseManager = databaseManager;
    Editor.ConfigurationSaved += OnConfigurationSaved;

    Refresh = ReactiveCommand.CreateFromTask(Baboopis);
    StartAres = ReactiveCommand.Create(aresStarter.Start);
    StopAres = ReactiveCommand.CreateFromTask(aresStarter.Stop);
    AresUpdate = ReactiveCommand.CreateFromTask(AwesUpdayt);

    _aresUpdater.UpdateStepDescription.BindTo(this, vm => vm.UpdateStepDescription);
    _aresUpdater.CurrentUpdateStep.BindTo(this, vm => vm.CurrentStep);
    _aresUpdater.UpdateProgress.BindTo(this, vm => vm.Progress);

    aresStarter.AresRunning.BindTo(this, vm => vm.AresRunning);
  }

  public ConfigurationOverviewViewModel Overview { get; }
  public ConfigurationEditorViewModel Editor { get; }

  private async Task Baboopis()
  {
    await _aresBinaryManager.Refresh();
    AresPresent = _aresBinaryManager.CurrentVersion is not null;
    var dbStatus = _databaseManager.Refresh();
    DatabaseStatus = _databaseManager.DatabaseStatus;
  }

  private async Task AwesUpdayt()
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

  //public IObservable<string> UpdateStepDescription { get; }

  [Reactive]
  public partial string Error { get; private set; }

  [Reactive]
  public partial string UpdateStepDescription { get; private set; }

  [Reactive]
  public partial UpdateStep CurrentStep { get; private set; }
  //public IObservable<UpdateStep> CurrentStep { get; }

  [Reactive]
  public partial double Progress { get; private set; }
  //public IObservable<double> Progress { get; }

  [Reactive]
  public partial bool AresPresent { get; private set; }

  [Reactive]
  public partial DatabaseStatus DatabaseStatus { get; private set; }

  [Reactive]
  public partial bool AresRunning { get; private set; }

  public ReactiveCommand<Unit, Unit> StartAres { get; }

  public ReactiveCommand<Unit, Unit> StopAres { get; set; }

  public ReactiveCommand<Unit, Unit> Refresh { get; private set; }

  public ReactiveCommand<Unit, Unit> AresUpdate { get; private set; }

  private void OnConfigurationSaved(object? sender, EventArgs e)
  {
    Overview.Refresh();
  }
}
