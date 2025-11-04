using System.Reactive;
using ARESLauncher.Services;
using ReactiveUI;

namespace ARESLauncher.ViewModels;
public class ConflictResolutionDialogViewModel : ReactiveObject
{
  public ConflictResolutionDialogViewModel(IConflictManager conflictManager)
  {
    KillCommand = ReactiveCommand.CreateFromTask(conflictManager.Kill);
    TakeOverCommand = ReactiveCommand.Create(() =>
    {
      conflictManager.TakeOverService();
      conflictManager.TakeOverUi();
    });

    IgnoreCommand = ReactiveCommand.Create(() => Unit.Default);
  }

  public ReactiveCommand<Unit, Unit> KillCommand { get; }
  public ReactiveCommand<Unit, Unit> TakeOverCommand { get; }
  public ReactiveCommand<Unit, Unit> IgnoreCommand { get; }
}
