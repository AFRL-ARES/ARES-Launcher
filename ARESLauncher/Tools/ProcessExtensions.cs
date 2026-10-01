using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public static class ProcessExtensions
{
  public static async Task<int> WaitForExitAndKillOnCancelAsync(this Process process, CancellationToken cancellationToken)
  {
    using var reg = cancellationToken.Register(() => TerminateProcess(process));
    try
    {
      await process.WaitForExitAsync();
      process.WaitForExit();
      return process.ExitCode;
    }
    finally
    {
      process.Dispose();
    }
  }

  public static void TerminateProcess(Process process)
  {
    try
    {
      if(OperatingSystem.IsWindows())
      {
        if(TryStartWindowsProcessTreeTermination(process.Id))
          return;

        process.Kill();
        return;
      }

      process.Kill(entireProcessTree: true);
    }
    catch(InvalidOperationException)
    {
      // The process exited before the cancellation callback reached it.
    }
    catch(Win32Exception)
    {
      // Windows released the process handle while termination was in progress.
    }
  }

  public static bool TryStartWindowsProcessTreeTermination(int processId)
  {
    try
    {
      var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
      var executablePath = string.IsNullOrEmpty(systemDirectory) ? "taskkill.exe" : Path.Combine(systemDirectory, "taskkill.exe");
      var startInfo = new ProcessStartInfo
      {
        FileName = executablePath,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      startInfo.ArgumentList.Add("/PID");
      startInfo.ArgumentList.Add(processId.ToString());
      startInfo.ArgumentList.Add("/T");
      startInfo.ArgumentList.Add("/F");

      var terminator = Process.Start(startInfo);
      if(terminator is null)
        return false;

      _ = DisposeProcessWhenExitedAsync(terminator);

      return true;
    }
    catch(InvalidOperationException)
    {
      return false;
    }
    catch(Win32Exception)
    {
      return false;
    }
  }

  private static async Task DisposeProcessWhenExitedAsync(Process process)
  {
    try
    {
      await process.WaitForExitAsync();
    }
    catch(InvalidOperationException)
    {
    }
    catch(Win32Exception)
    {
    }
    finally
    {
      process.Dispose();
    }
  }
}
