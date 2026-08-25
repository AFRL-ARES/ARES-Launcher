using ARESLauncher.Configuration;
using ReactiveUI.SourceGenerators;

namespace ARESLauncher.ViewModels;

public partial class PyAresComponentEditorViewModel : ViewModelBase
{
  public PyAresComponentEditorViewModel(PyAresComponentConfig config)
  {
    Name = config.Name;
    Description = config.Description;
    WorkingDirectory = config.WorkingDirectory;
    EntryPoint = config.EntryPoint;
    Arguments = config.Arguments;
    PythonInterpreterPath = config.PythonInterpreterPath;
    Enabled = config.Enabled;
    StartWithAres = config.StartWithAres;
  }

  [Reactive]
  public partial string Name { get; set; }

  [Reactive]
  public partial string Description { get; set; }

  [Reactive]
  public partial string WorkingDirectory { get; set; }

  [Reactive]
  public partial string EntryPoint { get; set; }

  [Reactive]
  public partial string Arguments { get; set; }

  [Reactive]
  public partial string PythonInterpreterPath { get; set; }

  [Reactive]
  public partial bool Enabled { get; set; }

  [Reactive]
  public partial bool StartWithAres { get; set; }

  public PyAresComponentConfig ToConfig()
  {
    return new PyAresComponentConfig
    {
      Name = Name,
      Description = Description,
      WorkingDirectory = WorkingDirectory,
      EntryPoint = EntryPoint,
      Arguments = Arguments,
      PythonInterpreterPath = PythonInterpreterPath,
      Enabled = Enabled,
      StartWithAres = StartWithAres
    };
  }
}

