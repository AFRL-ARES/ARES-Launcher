using System;
using System.Diagnostics;

namespace ARESLauncher.Tools;

public class ProcessRunner
{
  public ProcessResult Run(string fileName, string arguments, int timeoutSeconds = 10)
  {
    var psi = new ProcessStartInfo(fileName, arguments)
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };

    using var process = new Process();
    process.StartInfo = psi;
    process.Start();

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();

    if (!process.WaitForExit(timeoutSeconds * 1000))
      throw new TimeoutException($"Process '{fileName}' timed out.");

    return new ProcessResult(process.ExitCode, output, error);
  }
}

public record ProcessResult(int ExitCode, string StdOut, string StdErr);