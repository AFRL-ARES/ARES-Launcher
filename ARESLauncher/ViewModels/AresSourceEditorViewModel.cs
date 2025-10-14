using ARESLauncher.Models;
using ReactiveUI;

namespace ARESLauncher.ViewModels;

public class AresSourceEditorViewModel : ViewModelBase
{
  private string _owner;
  private string _repo;

  public AresSourceEditorViewModel(string owner, string repo)
  {
    _owner = owner;
    _repo = repo;
  }

  public string Owner
  {
    get => _owner;
    set => this.RaiseAndSetIfChanged(ref _owner, value);
  }

  public string Repo
  {
    get => _repo;
    set => this.RaiseAndSetIfChanged(ref _repo, value);
  }

  public AresSource ToAresSource()
  {
    return new AresSource(Owner, Repo);
  }
}
