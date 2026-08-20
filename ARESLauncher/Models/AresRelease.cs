using NuGet.Versioning;

namespace ARESLauncher.Models;

public class AresRelease
{
  public required SemanticVersion Version { get; init; }
  public required bool IsBeta { get; init; }
  public bool IsInstalled { get; set; }

  // Release notes for this version (plain text or Markdown)
  public string? ReleaseNotes { get; set; }

  public override string ToString()
  {
    return IsBeta ? $"{Version} (Beta)" : Version.ToString();
  }
}