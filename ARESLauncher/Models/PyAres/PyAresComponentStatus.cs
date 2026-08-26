namespace ARESLauncher.Models.PyAres;

public class PyAresComponentStatus
{
  public string Name { get; set; } = "";
  public bool IsRunning { get; set; }
  public string? LastError { get; set; }
}
