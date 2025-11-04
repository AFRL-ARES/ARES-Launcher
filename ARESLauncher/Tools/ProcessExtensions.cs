using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public static class ProcessExtensions
{
  public static async Task<int> WaitForExitAndKillOnCancelAsync(
      this Process process,
      CancellationToken cancellationToken)
  {
    using var reg = cancellationToken.Register(() =>
    {
      try
      {
        if(!process.HasExited)
        {
          // Kill the process (and optionally its children)
          process.Kill(entireProcessTree: true);
        }
      }
      catch(Exception ex)
      {
        // Swallow exceptions like "already exited"
        Console.WriteLine($"Kill attempt failed: {ex.Message}");
      }
    });

    await process.WaitForExitAsync(cancellationToken);
    return process.ExitCode;
  }
}
