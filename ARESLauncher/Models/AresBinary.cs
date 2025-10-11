using System;

namespace ARESLauncher.Models;

public record AresBinary
{
  public AppSettings UiSettings { get; set; }
  public AppSettings ServiceSettings { get; set; }
  public Version Version { get; set; }
}