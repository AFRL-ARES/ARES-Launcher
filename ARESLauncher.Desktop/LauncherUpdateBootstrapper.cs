using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ARESLauncher.Desktop;

internal static class LauncherUpdateBootstrapper
{
  private const string BootstrapperMode = "--apply-update";

  public static bool TryRun(string[] args)
  {
    if(args.Length == 0 || !string.Equals(args[0], BootstrapperMode, StringComparison.OrdinalIgnoreCase))
      return false;

    try
    {
      Run(args);
    }
    catch
    {
      // A bootstrapper failure should not launch the full UI process.
    }

    return true;
  }

  private static void Run(string[] args)
  {
    var parsedArgs = ParseArgs(args);
    var targetProcessId = int.Parse(GetRequiredArg(parsedArgs, "--target-pid"), CultureInfo.InvariantCulture);
    var sourceDir = GetRequiredArg(parsedArgs, "--source-dir");
    var targetDir = GetRequiredArg(parsedArgs, "--target-dir");
    var executablePath = GetRequiredArg(parsedArgs, "--exe-path");

    WaitForProcessExit(targetProcessId);
    Thread.Sleep(300);

    CopyDirectory(sourceDir, targetDir);
    EnsureExecutablePermissions(executablePath);
    Relaunch(executablePath, targetDir);
  }

  private static Dictionary<string, string> ParseArgs(string[] args)
  {
    if(args.Length < 9 || args.Length % 2 == 0)
      throw new InvalidOperationException("Invalid launcher bootstrapper arguments.");

    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for(var i = 1; i < args.Length; i += 2)
    {
      var key = args[i];
      var value = args[i + 1];
      parsed[key] = value;
    }

    return parsed;
  }

  private static string GetRequiredArg(IReadOnlyDictionary<string, string> args, string key)
  {
    return args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
      ? value
      : throw new InvalidOperationException($"Missing required launcher bootstrapper arg: {key}");
  }

  private static void WaitForProcessExit(int processId)
  {
    while(IsProcessRunning(processId))
    {
      Thread.Sleep(300);
    }
  }

  private static bool IsProcessRunning(int processId)
  {
    try
    {
      var process = Process.GetProcessById(processId);
      return !process.HasExited;
    }
    catch(ArgumentException)
    {
      return false;
    }
  }

  private static void CopyDirectory(string sourceDir, string targetDir)
  {
    if(!Directory.Exists(sourceDir))
      throw new DirectoryNotFoundException($"Staging directory not found: {sourceDir}");

    Directory.CreateDirectory(targetDir);

    foreach(var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
    {
      var relativePath = Path.GetRelativePath(sourceDir, directory);
      var destinationDirectory = Path.Combine(targetDir, relativePath);
      Directory.CreateDirectory(destinationDirectory);
    }

    foreach(var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
      var relativePath = Path.GetRelativePath(sourceDir, file);
      var destinationFile = Path.Combine(targetDir, relativePath);
      var destinationDirectory = Path.GetDirectoryName(destinationFile);
      if(!string.IsNullOrEmpty(destinationDirectory))
      {
        Directory.CreateDirectory(destinationDirectory);
      }

      File.Copy(file, destinationFile, true);
    }
  }

  private static void EnsureExecutablePermissions(string executablePath)
  {
    if(OperatingSystem.IsWindows())
      return;

    try
    {
      File.SetUnixFileMode(executablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    catch
    {
      // Best effort only.
    }
  }

  private static void Relaunch(string executablePath, string targetDir)
  {
    var workingDirectory = Path.GetDirectoryName(executablePath);
    if(string.IsNullOrWhiteSpace(workingDirectory))
      workingDirectory = targetDir;

    var psi = new ProcessStartInfo(executablePath)
    {
      UseShellExecute = false,
      CreateNoWindow = true,
      WorkingDirectory = workingDirectory
    };

    var process = Process.Start(psi);
    if(process is null)
      throw new InvalidOperationException("Failed to relaunch launcher after update.");
  }
}
