using NuGet.Versioning;

namespace ARESLauncher.Models;

public enum DowngradeOption
{
  None,
  RestoreSnapshot,
  Reset,
  Cancel
}

public class UpdateConfirmationRequest
{
  public required SemanticVersion CurrentVersion { get; init; }
  public required SemanticVersion TargetVersion { get; init; }
  public string ReleaseNotes { get; init; } = "";
  public bool HasSnapshot { get; set; }
}

public class UpdateConfirmationResponse
{
  public bool ShouldProceed { get; init; }
  public DowngradeOption DowngradeOption { get; init; } = DowngradeOption.None;

  public static UpdateConfirmationResponse Cancel => new() { ShouldProceed = false, DowngradeOption = DowngradeOption.Cancel };
  public static UpdateConfirmationResponse Proceed(DowngradeOption option = DowngradeOption.None) => new() { ShouldProceed = true, DowngradeOption = option };
}
