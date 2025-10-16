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
  private readonly IDatabaseManager _databaseManager;

  //[Reactive(SetModifier = AccessModifier.Private)]
  //private bool _aresPresent = false;

  public MainViewModel(ConfigurationOverviewViewModel overview,
    ConfigurationEditorViewModel editor,
    IAresBinaryManager aresBinaryManager,
    IAresStarter aresStarter,
    IAppSettingsUpdater appSettingsUpdater,
    ICertificateManager certificateManager,
    IDatabaseManager databaseManager)
  {
    Overview = overview ?? throw new ArgumentNullException(nameof(overview));
    Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    _aresBinaryManager = aresBinaryManager;
    _appSettingsUpdater = appSettingsUpdater;
    _certificateManager = certificateManager;
    _databaseManager = databaseManager;
    Editor.ConfigurationSaved += OnConfigurationSaved;

    Refresh = ReactiveCommand.CreateFromTask(Baboopis);
    StartAres = ReactiveCommand.Create(aresStarter.Start);
    StopAres = ReactiveCommand.CreateFromTask(aresStarter.Stop);
    AresUpdate = ReactiveCommand.CreateFromTask(AwesUpdayt);

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
    _appSettingsUpdater.UpdateAll();
    await _certificateManager.Update();
    await _databaseManager.RunMigrations();
  }

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
