namespace ARESLauncher.Models.PyAres;

public class PyAresRuntimeEntry
{
  public string Name { get; set; } = "";
  public int Pid { get; set; }
  public string WorkingDirectory { get; set; } = "";
  public string EntryPoint { get; set; } = "";
}
