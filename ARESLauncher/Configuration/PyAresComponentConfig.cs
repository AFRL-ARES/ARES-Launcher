namespace ARESLauncher.Configuration;

public class PyAresComponentConfig
{
  public string Name { get; set; } = "";
  public string Description { get; set; } = "";
  public string WorkingDirectory { get; set; } = "";
  public string EntryPoint { get; set; } = ""; // script path or module name
  public string Arguments { get; set; } = "";
  public string PythonInterpreterPath { get; set; } = "";
  public bool Enabled { get; set; } = true;
  public bool StartWithAres { get; set; } = true;
  public bool AutoRestart { get; set; } = false;
}
