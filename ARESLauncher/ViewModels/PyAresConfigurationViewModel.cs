using ARESLauncher.Configuration;
using ARESLauncher.Services.Configuration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;

namespace ARESLauncher.ViewModels;

public partial class PyAresConfigurationViewModel : ViewModelBase
{
  private readonly IAppConfigurationService _configurationService;

  public PyAresConfigurationViewModel(IAppConfigurationService configurationService)
  {
    _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

    Components = new ObservableCollection<PyAresComponentEditorViewModel>();
    LoadFromConfiguration();

    AddComponentCommand = ReactiveCommand.Create(AddComponent);
    RemoveSelectedComponentCommand = ReactiveCommand.Create(RemoveSelectedComponent, this.WhenAnyValue(vm => vm.SelectedComponent).Select(c => c is not null));
    SaveCommand = ReactiveCommand.Create(SaveConfiguration);
    ResetCommand = ReactiveCommand.Create(LoadFromConfiguration);
    BrowseWorkingDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseWorkingDirectory);
    BrowsePythonInterpreterCommand = ReactiveCommand.CreateFromTask(BrowsePythonInterpreter);
  }

  public ObservableCollection<PyAresComponentEditorViewModel> Components { get; }

  [Reactive]
  public partial PyAresComponentEditorViewModel? SelectedComponent { get; set; }

  public ReactiveCommand<Unit, Unit> AddComponentCommand { get; }
  public ReactiveCommand<Unit, Unit> RemoveSelectedComponentCommand { get; }
  public ReactiveCommand<Unit, Unit> SaveCommand { get; }
  public ReactiveCommand<Unit, Unit> ResetCommand { get; }
  public ReactiveCommand<Unit, Unit> BrowseWorkingDirectoryCommand { get; }
  public ReactiveCommand<Unit, Unit> BrowsePythonInterpreterCommand { get; }

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
  }

  private void AddComponent()
  {
    PyAresComponentConfig cfg;
    if(SelectedComponent is not null)
    {
      cfg = SelectedComponent.ToConfig();
    }
    else
    {
      cfg = new PyAresComponentConfig
      {
        Name = "New PyAres component",
        Enabled = true,
        StartWithAres = true
      };
    }

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
}

