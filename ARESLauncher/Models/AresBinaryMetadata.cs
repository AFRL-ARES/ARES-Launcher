namespace ARESLauncher.Models;

public class AresBinaryMetadata
{
  public AresSource? Source { get; set; }

  /// <summary>
  ///   SemanticVersion stored as normalized string to simplify serialization.
  /// </summary>
  public string? Version { get; set; }
}
