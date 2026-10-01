using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public sealed class DemoAresManager : IDemoAresManager
{
  private static readonly string[] DemoServiceNames = [ "DemoRemoteAnalyzer", "DemoRemoteDevice", "DemoRemotePlanner" ];
  private readonly IAppConfigurationService _configurationService;
  private readonly IExecutableGetter _executableGetter;
  private readonly ILogger<DemoAresManager> _logger;
  private readonly Dictionary<string, CancellationTokenSource> _serviceTokens = new();
  private readonly Dictionary<string, int> _serviceProcessIds = new();
  private readonly Dictionary<string, Task> _serviceTasks = new();

  public DemoAresManager(IAppConfigurationService configurationService, IExecutableGetter executableGetter, ILogger<DemoAresManager> logger)
  {
    _configurationService = configurationService;
    _executableGetter = executableGetter;
    _logger = logger;
  }

  public Task StartAll()
  {
    if(!_configurationService.Current.DemoMode)
      return Task.CompletedTask;

    foreach(var serviceName in DemoServiceNames)
      StartService(serviceName);

    return Task.CompletedTask;
  }

  public async Task StopAll()
  {
    var tokens = _serviceTokens.Values.ToArray();
    var serviceTasks = _serviceTasks.Values.ToArray();

    foreach(var token in tokens)
    {
      try
      {
        await token.CancelAsync();
      }
      catch(OperationCanceledException)
      {
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Failed to request shutdown for a demo service.");
      }
    }

    try
    {
      await Task.WhenAll(serviceTasks);
    }
    catch(OperationCanceledException)
    {
    }
    catch(Exception ex)
    {
      _logger.LogWarning(ex, "A demo service faulted while stopping.");
    }

    _serviceTokens.Clear();
    _serviceProcessIds.Clear();
    _serviceTasks.Clear();
  }

  private void StartService(string serviceName)
  {
    if(_serviceTokens.ContainsKey(serviceName))
      return;

    var executablePath = _executableGetter.GetDemoServiceExecutablePath(serviceName);
    if(executablePath is null || !File.Exists(executablePath))
    {
      _logger.LogError("Demo service executable is not present: {ExecutablePath}", executablePath);
      return;
    }

    var workingDirectory = Path.GetDirectoryName(executablePath);
    if(string.IsNullOrEmpty(workingDirectory))
    {
      _logger.LogError("Couldn't resolve the working directory for demo service {ServiceName}.", serviceName);
      return;
    }

    var process = new Process
    {
      StartInfo = new ProcessStartInfo
      {
        FileName = executablePath,
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        CreateNoWindow = true
      }
    };

    try
    {
      if(!process.Start())
        throw new InvalidOperationException($"Failed to start demo service {serviceName}.");

      var cancellationTokenSource = new CancellationTokenSource();
      var startedProcess = new StartedDemoProcess(process.Id, process.WaitForExitAndKillOnCancelAsync(cancellationTokenSource.Token));

      _serviceTokens[serviceName] = cancellationTokenSource;
      _serviceProcessIds[serviceName] = startedProcess.ProcessId;
      _serviceTasks[serviceName] = startedProcess.Task;

      ProcessServiceTask(serviceName, startedProcess, cancellationTokenSource);
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Failed to start demo service {ServiceName}.", serviceName);
      ProcessExtensions.TerminateProcess(process);
      process.Dispose();
    }
  }

  private void ProcessServiceTask(
    string serviceName,
    StartedDemoProcess startedProcess,
    CancellationTokenSource cancellationTokenSource)
  {
    var serviceTask = startedProcess.Task;
    var processId = startedProcess.ProcessId;
    serviceTask.ContinueWith(task =>
    {
      if(task.IsFaulted)
      {
        _logger.LogError(task.Exception, "Demo service {ServiceName} faulted.", serviceName);
      }
      else if(!cancellationTokenSource.IsCancellationRequested)
      {
        _logger.LogInformation("Demo service {ServiceName} completed.", serviceName);
      }

      if(_serviceTokens.TryGetValue(serviceName, out var currentToken) && ReferenceEquals(currentToken, cancellationTokenSource))
        _serviceTokens.Remove(serviceName);

      if(_serviceProcessIds.TryGetValue(serviceName, out var currentProcessId) && currentProcessId == processId)
        _serviceProcessIds.Remove(serviceName);

      if(_serviceTasks.TryGetValue(serviceName, out var currentTask) && ReferenceEquals(currentTask, serviceTask))
        _serviceTasks.Remove(serviceName);
    }, TaskScheduler.Default);
  }
}
