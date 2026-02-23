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
      tempRoot,
      _logger);

    _logger.LogInformation("Launcher update to {Version} has been downloaded and staged.", latest.ToNormalizedString());
    return true;
  }

  private static void EnsureInstallDirectoryIsWritable(string installDirectory)
  {
    var probe = Path.Combine(installDirectory, $".areslauncher-update-probe-{Guid.NewGuid():N}.tmp");
    File.WriteAllText(probe, "");
    File.Delete(probe);
  }

  private static void StartUpdateWorker(int targetProcessId, string sourceDir, string targetDir, string executablePath, string workingDir,
    ILogger logger)
  {
    Directory.CreateDirectory(workingDir);

    CopyBootstrapperRuntime(executablePath, targetDir, workingDir);
    var bootstrapperPath = Path.Combine(workingDir, Path.GetFileName(executablePath));
    EnsureExecutablePermissions(bootstrapperPath);

    var psi = new ProcessStartInfo(bootstrapperPath)
    {
      UseShellExecute = false,
      CreateNoWindow = true,
      WorkingDirectory = workingDir
    };

    psi.ArgumentList.Add("--apply-update");
    psi.ArgumentList.Add("--target-pid");
    psi.ArgumentList.Add(targetProcessId.ToString(CultureInfo.InvariantCulture));
    psi.ArgumentList.Add("--source-dir");
    psi.ArgumentList.Add(sourceDir);
    psi.ArgumentList.Add("--target-dir");
    psi.ArgumentList.Add(targetDir);
    psi.ArgumentList.Add("--exe-path");
    psi.ArgumentList.Add(executablePath);

    var process = Process.Start(psi);
    if(process is null)
      throw new InvalidOperationException("Failed to start the launcher update bootstrapper.");

    logger.LogInformation("Launcher bootstrapper process started. Pid: {Pid}", process.Id);
  }

  private static void CopyBootstrapperRuntime(string executablePath, string installDirectory, string workingDirectory)
  {
    var exeName = Path.GetFileName(executablePath);
    var copiedFiles = 0;

    CopyIfExists(executablePath, Path.Combine(workingDirectory, exeName), ref copiedFiles);

    var baseName = Path.GetFileNameWithoutExtension(executablePath);
    CopyIfExists(Path.Combine(installDirectory, $"{baseName}.dll"), Path.Combine(workingDirectory, $"{baseName}.dll"), ref copiedFiles);
    CopyIfExists(Path.Combine(installDirectory, $"{baseName}.deps.json"), Path.Combine(workingDirectory, $"{baseName}.deps.json"), ref copiedFiles);
    CopyIfExists(Path.Combine(installDirectory, $"{baseName}.runtimeconfig.json"),
      Path.Combine(workingDirectory, $"{baseName}.runtimeconfig.json"), ref copiedFiles);

    foreach(var file in Directory.EnumerateFiles(installDirectory, "*.dll"))
    {
      var destination = Path.Combine(workingDirectory, Path.GetFileName(file));
      CopyIfExists(file, destination, ref copiedFiles);
    }

    if(copiedFiles == 0)
      throw new InvalidOperationException("Failed to stage launcher bootstrapper runtime files.");
  }

  private static void CopyIfExists(string source, string destination, ref int copiedFiles)
  {
    if(!File.Exists(source))
      return;

    File.Copy(source, destination, true);
    copiedFiles++;
  }

  private static void EnsureExecutablePermissions(string executablePath)
  {
    if(OperatingSystem.IsWindows())
      return;

    try
    {
      File.SetUnixFileMode(executablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    catch(Exception)
    {
      // Best effort; permissions can already be correct based on extraction umask.
    }
  }
}
