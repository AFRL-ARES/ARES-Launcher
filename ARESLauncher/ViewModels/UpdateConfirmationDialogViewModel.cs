using System.Reactive;
using ARESLauncher.Models;
using ReactiveUI;

namespace ARESLauncher.ViewModels;

public class UpdateConfirmationDialogViewModel : ReactiveObject
{
  public UpdateConfirmationDialogViewModel(UpdateConfirmationRequest request)
  {
    CurrentVersion = request.CurrentVersion.ToNormalizedString();
    TargetVersion = request.TargetVersion.ToNormalizedString();

    MajorUpdate = request.TargetVersion.Major > request.CurrentVersion.Major;
    
    ProceedCommand = ReactiveCommand.Create(() => Unit.Default);
    CancelCommand = ReactiveCommand.Create(() => Unit.Default);
  }

  public string CurrentVersion { get; }
  public string TargetVersion { get; }
  
  public bool MajorUpdate { get; }

  public string Message =>
    MajorUpdate 
      ? $"This will update ARES from {CurrentVersion} to {TargetVersion}.\nThis is a major update and we recommend backing up your database as there is potential of data loss."
      : $"This will update ARES from {CurrentVersion} to {TargetVersion}.\nWhile this is a minor update, we would still recommend backing up your database just to be safe.";

  public ReactiveCommand<Unit, Unit> ProceedCommand { get; }
  public ReactiveCommand<Unit, Unit> CancelCommand { get; }
}
