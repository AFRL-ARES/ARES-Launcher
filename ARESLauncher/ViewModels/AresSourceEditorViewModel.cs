using ARESLauncher.Models;
using ReactiveUI.SourceGenerators;

namespace ARESLauncher.ViewModels;

public partial class AresSourceEditorViewModel : ViewModelBase
{
  public AresSourceEditorViewModel(string owner, string repo, bool bundle = true)
  {
    Owner = owner;
    Repo = repo;
    Bundle = bundle;
  }

  [Reactive]
  public partial string Owner { get; set; }

  [Reactive]
  public partial string Repo { get; set; }

  [Reactive]
  public partial bool Bundle { get; set; }

  public AresSource ToAresSource()
  {
    return new AresSource(Owner, Repo, Bundle);
  }
}
