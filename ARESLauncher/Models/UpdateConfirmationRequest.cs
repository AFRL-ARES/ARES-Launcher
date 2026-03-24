using NuGet.Versioning;

namespace ARESLauncher.Models;

public class UpdateConfirmationRequest
{
  public required SemanticVersion CurrentVersion { get; init; }
  public required SemanticVersion TargetVersion { get; init; }
}
