using System;
using System.Collections.Generic;

namespace ARESLauncher.Models.PyAres;

public class PyAresRuntimeState
{
  public DateTime LastUpdated { get; set; }
  public List<PyAresRuntimeEntry> Components { get; set; } = new();
}
