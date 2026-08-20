using System.Reactive;
using ARESLauncher.Models;
using ReactiveUI;

namespace ARESLauncher.ViewModels;

public class UpdateConfirmationDialogViewModel : ReactiveObject
{
  private const string _pyaresReminderText = "If you have PyAres services, you should update their versions of the PyAres library as well.";

  public UpdateConfirmationDialogViewModel(UpdateConfirmationRequest request)
  {
    CurrentVersion = request.CurrentVersion.ToNormalizedString();
    TargetVersion = request.TargetVersion.ToNormalizedString();
    ReleaseNotes = request.ReleaseNotes;

    MajorUpdate = request.TargetVersion > request.CurrentVersion && request.TargetVersion.Major > request.CurrentVersion.Major;
    IsDowngrade = request.TargetVersion < request.CurrentVersion;
    HasSnapshot = request.HasSnapshot;
    
    ProceedCommand = ReactiveCommand.Create(() => UpdateConfirmationResponse.Proceed());
    CancelCommand = ReactiveCommand.Create(() => UpdateConfirmationResponse.Cancel);
    RestoreSnapshotCommand = ReactiveCommand.Create(() => UpdateConfirmationResponse.Proceed(DowngradeOption.RestoreSnapshot));
    ResetCommand = ReactiveCommand.Create(() => UpdateConfirmationResponse.Proceed(DowngradeOption.Reset));
  }

   public string CurrentVersion { get; }
   public string TargetVersion { get; }

   // Release notes for the target version being updated to.
   public string? ReleaseNotes { get; }

   public bool MajorUpdate { get; }
   public bool IsDowngrade { get; }
   public bool HasSnapshot { get; }

  public string Message =>
    IsDowngrade
      ? HasSnapshot
        ? $"You are downgrading ARES from {CurrentVersion} to {TargetVersion}.\nA database snapshot for version {TargetVersion} was found. Would you like to restore it?"
        : $"You are downgrading ARES from {CurrentVersion} to {TargetVersion}.\nNo database snapshot for version {TargetVersion} was found. Your database will be reset to avoid compatibility issues."
      : MajorUpdate 
        ? $"This will update ARES from {CurrentVersion} to {TargetVersion}.\nThis is a major update and we recommend backing up your database as there is potential of data loss. {_pyaresReminderText}"
        : $"This will update ARES from {CurrentVersion} to {TargetVersion}.\nWhile this is a minor update, we would still recommend backing up your database just to be safe. {_pyaresReminderText}";

  public ReactiveCommand<Unit, UpdateConfirmationResponse> ProceedCommand { get; }
  public ReactiveCommand<Unit, UpdateConfirmationResponse> CancelCommand { get; }
  public ReactiveCommand<Unit, UpdateConfirmationResponse> RestoreSnapshotCommand { get; }
  public ReactiveCommand<Unit, UpdateConfirmationResponse> ResetCommand { get; }
}
