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
    BrowseWorkingDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseWorkingDirectory);
    BrowsePythonInterpreterCommand = ReactiveCommand.CreateFromTask(BrowsePythonInterpreter);
    BrowseEntryPointCommand = ReactiveCommand.CreateFromTask(BrowseEntryPoint);
    RestartSelectedComponentCommand = ReactiveCommand.CreateFromTask(
      RestartSelectedComponent,
      this.WhenAnyValue(vm => vm.SelectedComponent).Select(c => c is not null));
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
  public ReactiveCommand<Unit, Unit> BrowseWorkingDirectoryCommand { get; }
  public ReactiveCommand<Unit, Unit> BrowsePythonInterpreterCommand { get; }
  public ReactiveCommand<Unit, Unit> BrowseEntryPointCommand { get; }
  public ReactiveCommand<Unit, Unit> RestartSelectedComponentCommand { get; }

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

  private async Task BrowseWorkingDirectory()
  {
    if(SelectedComponent is null)
      return;

    if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
       && desktop.MainWindow is Window window)
    {
      var dialog = new OpenFolderDialog();
      var result = await dialog.ShowAsync(window);
      if(!string.IsNullOrWhiteSpace(result))
      {
        SelectedComponent.WorkingDirectory = result;
      }
    }
  }

  private async Task BrowsePythonInterpreter()
  {
    if(SelectedComponent is null)
      return;

    if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
       && desktop.MainWindow is Window window)
    {
      var dialog = new OpenFileDialog
      {
        AllowMultiple = false,
        Title = "Select Python Interpreter"
      };
      var result = await dialog.ShowAsync(window);
      var path = result?.FirstOrDefault();
      if(!string.IsNullOrWhiteSpace(path))
      {
        SelectedComponent.PythonInterpreterPath = path;
      }
    }
  }

  private async Task BrowseEntryPoint()
  {
    if(SelectedComponent is null)
      return;

    if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
       && desktop.MainWindow is Window window)
    {
      var dialog = new OpenFileDialog
      {
        AllowMultiple = false,
        Title = "Select Entry Script",
        Directory = string.IsNullOrWhiteSpace(SelectedComponent.WorkingDirectory)
          ? null
          : SelectedComponent.WorkingDirectory
      };
      var result = await dialog.ShowAsync(window);
      var path = result?.FirstOrDefault();
      if(!string.IsNullOrWhiteSpace(path))
      {
        SelectedComponent.EntryPoint = System.IO.Path.GetFileName(path);
      }
    }
  }

  private async Task RestartSelectedComponent()
  {
    if(SelectedComponent is null)
      return;

    await _pyAresManager.RestartComponent(SelectedComponent.Name);
  }
}
