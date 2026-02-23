using ARESLauncher.Services.Configuration;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public class LauncherUpdater : ILauncherUpdater
{
  private readonly IAresDownloader _downloader;
  private readonly ILogger<LauncherUpdater> _logger;
  private readonly IAppConfigurationService _configurationService;

  public LauncherUpdater(IAresDownloader downloader, ILogger<LauncherUpdater> logger, IAppConfigurationService configurationService)
  {
    _downloader = downloader;
    _logger = logger;
    _configurationService = configurationService;
  }

  public Task<SemanticVersion[]> GetAvailableVersions()
  {
    var source = _configurationService.Current.LauncherSource;
    return _downloader.GetAvailableVersions(source);
  }

  public async Task<bool> UpdateLatest()
  {
    var versions = await GetAvailableVersions();
    var latest = versions.OrderDescending().FirstOrDefault();
    if(latest is null)
      throw new InvalidOperationException("No launcher versions found.");

    var currentLauncherVersion = LauncherVersionHelper.GetLauncherVersion();
    if(!SemanticVersion.TryParse(currentLauncherVersion, out var currentVersion))
      throw new InvalidOperationException($"Unable to parse current launcher version: {currentLauncherVersion}");

    if(currentVersion.IsGreatest(versions))
    {
      _logger.LogInformation("Launcher is already up to date at {Version}", currentVersion.ToNormalizedString());
      return false;
    }

    var source = _configurationService.Current.LauncherSource;
    var tempRoot = Path.Combine(Path.GetTempPath(), "ares-launcher-selfupdate", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var archivePath = await _downloader.Download(source, latest, tempRoot, _configurationService.Current.GitToken);
    if(!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
      throw new NotSupportedException("Automatic launcher updates currently support only .zip release assets.");

    var stagingPath = Path.Combine(tempRoot, "staging");
    await Unpacker.Unpack(archivePath, stagingPath);

    var executablePath = Environment.ProcessPath;
    if(string.IsNullOrWhiteSpace(executablePath))
      throw new InvalidOperationException("Unable to determine launcher executable path.");

    var installDirectory = Path.GetFullPath(AppContext.BaseDirectory);
    EnsureInstallDirectoryIsWritable(installDirectory);

    StartUpdateWorker(
      Environment.ProcessId,
      stagingPath,
      installDirectory,
      executablePath,
      tempRoot);

    _logger.LogInformation("Launcher update to {Version} has been downloaded and staged.", latest.ToNormalizedString());
    return true;
  }

  private static void EnsureInstallDirectoryIsWritable(string installDirectory)
  {
    var probe = Path.Combine(installDirectory, $".areslauncher-update-probe-{Guid.NewGuid():N}.tmp");
    File.WriteAllText(probe, "");
    File.Delete(probe);
  }

  private static void StartUpdateWorker(int targetProcessId, string sourceDir, string targetDir, string executablePath, string workingDir)
  {
    if(OperatingSystem.IsWindows())
    {
      var scriptPath = Path.Combine(workingDir, "apply-launcher-update.ps1");
      File.WriteAllText(scriptPath, BuildWindowsScript());

      var psi = new ProcessStartInfo("powershell.exe")
      {
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = workingDir
      };

      psi.ArgumentList.Add("-NoProfile");
      psi.ArgumentList.Add("-ExecutionPolicy");
      psi.ArgumentList.Add("Bypass");
      psi.ArgumentList.Add("-File");
      psi.ArgumentList.Add(scriptPath);
      psi.ArgumentList.Add(targetProcessId.ToString(CultureInfo.InvariantCulture));
      psi.ArgumentList.Add(sourceDir);
      psi.ArgumentList.Add(targetDir);
      psi.ArgumentList.Add(executablePath);

      var process = Process.Start(psi);
      if(process is null)
        throw new InvalidOperationException("Failed to start the launcher update worker.");
      return;
    }

    var shellScriptPath = Path.Combine(workingDir, "apply-launcher-update.sh");
    File.WriteAllText(shellScriptPath, BuildUnixScript());
    try
    {
      File.SetUnixFileMode(shellScriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    catch(Exception)
    {
      // Best effort; script may still be executable depending on the environment umask.
    }

    var shellPsi = new ProcessStartInfo("/bin/bash")
    {
      UseShellExecute = false,
      CreateNoWindow = true,
      WorkingDirectory = workingDir
    };

    shellPsi.ArgumentList.Add(shellScriptPath);
    shellPsi.ArgumentList.Add(targetProcessId.ToString(CultureInfo.InvariantCulture));
    shellPsi.ArgumentList.Add(sourceDir);
    shellPsi.ArgumentList.Add(targetDir);
    shellPsi.ArgumentList.Add(executablePath);

    var shellProcess = Process.Start(shellPsi);
    if(shellProcess is null)
      throw new InvalidOperationException("Failed to start the launcher update worker.");
  }

  private static string BuildWindowsScript()
  {
    return """
           param(
             [int]$TargetProcessId,
             [string]$SourceDir,
             [string]$TargetDir,
             [string]$ExecutablePath
           )

           $ErrorActionPreference = "Stop"

           while (Get-Process -Id $TargetProcessId -ErrorAction SilentlyContinue) {
             Start-Sleep -Milliseconds 300
           }

           Start-Sleep -Milliseconds 300
           New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
           Copy-Item -Path (Join-Path $SourceDir '*') -Destination $TargetDir -Recurse -Force
           Start-Process -FilePath $ExecutablePath -WorkingDirectory (Split-Path -Path $ExecutablePath -Parent)
           """;
  }

  private static string BuildUnixScript()
  {
    return """
           #!/usr/bin/env bash
           set -euo pipefail

           TARGET_PID="$1"
           SOURCE_DIR="$2"
           TARGET_DIR="$3"
           EXECUTABLE_PATH="$4"

           while kill -0 "$TARGET_PID" >/dev/null 2>&1; do
             sleep 0.3
           done

           sleep 0.3
           mkdir -p "$TARGET_DIR"
           cp -R "$SOURCE_DIR"/. "$TARGET_DIR"/
           chmod +x "$EXECUTABLE_PATH" || true
           nohup "$EXECUTABLE_PATH" >/dev/null 2>&1 &
           """;
  }
}
