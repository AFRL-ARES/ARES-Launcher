using ARESLauncher.Configuration;
using ARESLauncher.Services;
using ARESLauncher.Services.Configuration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;
using ARESLauncher.Models.PyAres;
using Avalonia.Platform.Storage;

namespace ARESLauncher.ViewModels;

public partial class PyAresConfigurationViewModel : ViewModelBase
{
  private readonly IAppConfigurationService _configurationService;
  private readonly IPyAresManager _pyAresManager;
  private IDisposable? _outputSubscription;
  private IReadOnlyList<PyAresComponentStatus> _latestStatuses = Array.Empty<PyAresComponentStatus>();

  public PyAresConfigurationViewModel(IAppConfigurationService configurationService, IPyAresManager pyAresManager)
  {
    _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    _pyAresManager = pyAresManager ?? throw new ArgumentNullException(nameof(pyAresManager));
    SelectedOutput = string.Empty;

    Components = new ObservableCollection<PyAresComponentEditorViewModel>();
    LoadFromConfiguration();

    _pyAresManager.ComponentStatuses.Subscribe(UpdateStatuses);

    this.WhenAnyValue(vm => vm.SelectedComponent)
      .Select(component => component?.Name)
      .DistinctUntilChanged()
      .Subscribe(componentName =>
      {
        _outputSubscription?.Dispose();
        SelectedOutput = string.Empty;

        if(string.IsNullOrWhiteSpace(componentName))
        {
          return;
        }

        _outputSubscription = _pyAresManager
          .GetOutput(componentName)
          .ObserveOn(RxApp.MainThreadScheduler)
          .Subscribe(output => SelectedOutput = output);
      });

    AddComponentCommand = ReactiveCommand.Create(AddComponent);
    NewComponentCommand = ReactiveCommand.Create(NewComponent);
    RemoveSelectedComponentCommand = ReactiveCommand.Create(
      RemoveSelectedComponent,
      this.WhenAnyValue(vm => vm.SelectedComponent).Select(c => c is not null));
    SaveCommand = ReactiveCommand.Create(SaveConfiguration);
    ResetCommand = ReactiveCommand.Create(LoadFromConfiguration);
    BrowseWorkingDirectoryCommand = ReactiveCommand.CreateFromTask<IStorageProvider>(BrowseWorkingDirectory);
    BrowsePythonInterpreterCommand = ReactiveCommand.CreateFromTask<IStorageProvider>(BrowsePythonInterpreter);
    BrowseEntryPointCommand = ReactiveCommand.CreateFromTask<IStorageProvider>(BrowseEntryPoint);
    RestartSelectedComponentCommand = ReactiveCommand.CreateFromTask(
      RestartSelectedComponent,
      this.WhenAnyValue(vm => vm.SelectedComponent).Select(c => c is not null));
  }

  private void LoadFromConfiguration()
  {
    Components.Clear();
    var current = _configurationService.Current;
    if(current.PyAresComponents is not null)
    {
      foreach(var cfg in current.PyAresComponents)
      {
        Components.Add(new PyAresComponentEditorViewModel(cfg));
      }
    }

    SelectedComponent = Components.FirstOrDefault();

    // Reapply latest status information so icons remain accurate after reload
    if(_latestStatuses is not null && _latestStatuses.Count > 0)
    {
      UpdateStatuses(_latestStatuses);
    }
  }

  private void AddComponent()
  {
    if(SelectedComponent is null)
      return;

    var cfg = SelectedComponent.ToConfig();
    var vm = new PyAresComponentEditorViewModel(cfg);
    Components.Add(vm);
    SelectedComponent = vm;
  }

  private void NewComponent()
  {
    var cfg = new PyAresComponentConfig
    {
      Name = string.Empty,
      Description = string.Empty,
      WorkingDirectory = string.Empty,
      EntryPoint = string.Empty,
      Arguments = string.Empty,
      PythonInterpreterPath = string.Empty,
      Enabled = true,
      StartWithAres = true
    };

    var vm = new PyAresComponentEditorViewModel(cfg);
    Components.Add(vm);
    SelectedComponent = vm;
  }

  private void RemoveSelectedComponent()
  {
    if(SelectedComponent is null)
      return;

    var idx = Components.IndexOf(SelectedComponent);
    if(idx >= 0)
    {
      Components.RemoveAt(idx);
    }

    SelectedComponent = Components.FirstOrDefault();
  }

  private void SaveConfiguration()
  {
    _configurationService.Update(cfg =>
    {
      cfg.PyAresComponents = Components.Select(c => c.ToConfig()).ToArray();
    });

    LoadFromConfiguration();
  }

  private void UpdateStatuses(IReadOnlyList<PyAresComponentStatus> statuses)
  {
    _latestStatuses = statuses ?? Array.Empty<PyAresComponentStatus>();

    foreach(var vm in Components)
    {
      var status = _latestStatuses.FirstOrDefault(s => s.Name == vm.Name);
      vm.IsRunning = status?.IsRunning ?? false;
    }
  }

  private async Task BrowseWorkingDirectory(IStorageProvider storageProvider)
  {
    if(SelectedComponent is null)
      return;

    var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Working Directory", AllowMultiple = false });
    var folder = result.FirstOrDefault();
    var localPath = folder?.TryGetLocalPath();

    if(localPath is not null)
      SelectedComponent.WorkingDirectory = localPath;
  }

  private async Task BrowsePythonInterpreter(IStorageProvider storageProvider)
  {
    if(SelectedComponent is null)
      return;

    IStorageFolder? startFolder = null;

    if(!string.IsNullOrWhiteSpace(SelectedComponent.WorkingDirectory))
      startFolder = await storageProvider.TryGetFolderFromPathAsync(SelectedComponent.WorkingDirectory);

    var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Select Python Interpreter", AllowMultiple = false, SuggestedStartLocation = startFolder });

    var file = files.FirstOrDefault();

    if(file is not null)
      SelectedComponent.PythonInterpreterPath = file.TryGetLocalPath() ?? "";
  }

  private async Task BrowseEntryPoint(IStorageProvider storageProvider)
  {
    if(SelectedComponent is null || !storageProvider.CanOpen)
      return;

    IStorageFolder? startFolder = null;

    if(!string.IsNullOrWhiteSpace(SelectedComponent.WorkingDirectory))
      startFolder = await storageProvider.TryGetFolderFromPathAsync(SelectedComponent.WorkingDirectory);

    var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Select Entry Script", AllowMultiple = false, SuggestedStartLocation = startFolder });

    var file = files.FirstOrDefault();

    if(file is not null)
      SelectedComponent.EntryPoint = file.Name;
  }

  private async Task RestartSelectedComponent()
  {
    if(SelectedComponent is null)
      return;

    await _pyAresManager.RestartComponent(SelectedComponent.Name);
  }

  public ObservableCollection<PyAresComponentEditorViewModel> Components { get; }

  [Reactive]
  public partial PyAresComponentEditorViewModel? SelectedComponent { get; set; }

  [Reactive]
  public partial string SelectedOutput { get; set; }

  public ReactiveCommand<Unit, Unit> AddComponentCommand { get; }
  public ReactiveCommand<Unit, Unit> NewComponentCommand { get; }
  public ReactiveCommand<Unit, Unit> RemoveSelectedComponentCommand { get; }
  public ReactiveCommand<Unit, Unit> SaveCommand { get; }
  public ReactiveCommand<Unit, Unit> ResetCommand { get; }
  public ReactiveCommand<IStorageProvider, Unit> BrowseWorkingDirectoryCommand { get; }
  public ReactiveCommand<IStorageProvider, Unit> BrowsePythonInterpreterCommand { get; }
  public ReactiveCommand<IStorageProvider, Unit> BrowseEntryPointCommand { get; }
  public ReactiveCommand<Unit, Unit> RestartSelectedComponentCommand { get; }
}
