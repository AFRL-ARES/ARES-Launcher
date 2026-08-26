namespace ARESLauncher.Models.PyAres;

public class PyAresProcessInfo
{
  public string Name { get; set; } = "";
  public int Pid { get; set; }
  public string WorkingDirectory { get; set; } = "";
  public string EntryPoint { get; set; } = "";
  public bool IsAlive { get; set; }
}
