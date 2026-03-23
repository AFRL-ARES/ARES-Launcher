using ARESLauncher.Models;
using ReactiveUI.SourceGenerators;

namespace ARESLauncher.ViewModels;

public partial class AresSourceEditorViewModel : ViewModelBase
{
  public AresSourceEditorViewModel(string owner, string repo)
  {
    Owner = owner;
    Repo = repo;
  }

  [Reactive]
  public partial string Owner { get; set; }

  [Reactive]
  public partial string Repo { get; set; }

  public AresSource ToAresSource()
  {
    return new AresSource(Owner, Repo);
  }
}
