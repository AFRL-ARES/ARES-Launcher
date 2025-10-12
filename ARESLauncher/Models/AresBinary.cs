using System;
using NuGet.Versioning;

namespace ARESLauncher.Models;

public record AresBinary
{
  public AppSettings UiSettings { get; set; }
  public AppSettings ServiceSettings { get; set; }
  public SemanticVersion Version { get; set; }
}
