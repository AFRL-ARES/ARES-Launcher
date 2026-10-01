using System.Threading.Tasks;

namespace ARESLauncher.Models;

public sealed record StartedDemoProcess(int ProcessId, Task Task);
