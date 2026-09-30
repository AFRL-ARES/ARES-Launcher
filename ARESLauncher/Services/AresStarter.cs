using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ARESLauncher.Models;
using CliWrap;
using CliWrap.Exceptions;
using Microsoft.Extensions.Logging;
using ARESLauncher.Services.Configuration;

namespace ARESLauncher.Services;

public class AresStarter : IAresStarter
{
  private static readonly string[] DemoServiceExecutableNames =
  [
    "DemoRemoteAnalyzer",
    "DemoRemoteDevice",
    "DemoRemotePlanner"
  ];

  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly IExecutableGetter _executableGetter;
  private readonly IAppConfigurationService _configurationService;
  private readonly IDatabaseManager _databaseManager;
  private readonly ILogger<AresStarter> _logger;
  private readonly BehaviorSubject<bool> _aresUiRunningSubject = new(false);
  private readonly BehaviorSubject<bool> _aresServiceRunningSubject = new(false);

  private Task _uiTask = Task.CompletedTask;
  private Task _serviceTask = Task.CompletedTask;
  private Task[] _demoServiceTasks = [];

  private CancellationTokenSource _cancellationTokenSource = new();
  private int _stopInitiated = 0;

  public AresStarter(
    IAresBinaryManager aresBinaryManager,
    IExecutableGetter executableGetter,
    IAppConfigurationService configurationService,
    IDatabaseManager databaseManager,
    ILogger<AresStarter> logger)
  {
    _aresBinaryManager = aresBinaryManager;
    _executableGetter = executableGetter;
    _configurationService = configurationService;
    _databaseManager = databaseManager;
    _logger = logger;
    AresUiRunning = _aresUiRunningSubject.AsObservable();
    AresServiceRunning = _aresServiceRunningSubject.AsObservable();
  }

  public IObservable<bool> AresUiRunning { get; }
  public IObservable<bool> AresServiceRunning { get; }

  public async void Start()
  {
    if(IsHealthyRunning())
    {
      _logger.LogDebug("Start requested while ARES is already running. Skipping.");
      return;
    }

    var demoMode = _configurationService.Current.DemoMode;
    var currentVersion = _aresBinaryManager.CurrentVersion;
    if(!demoMode && currentVersion is not null)
    {
      try
      {
        await _databaseManager.CreateSnapshot(currentVersion);
      }
      catch(Exception e)
      {
        _logger.LogWarning("Failed to create database snapshot before start: {Exception}", e);
      }
    }

    _cancellationTokenSource = new CancellationTokenSource();
    _stopInitiated = 0;
    _demoServiceTasks = [];

    if(demoMode)
    {
      _demoServiceTasks = StartDemoServices(_cancellationTokenSource.Token);
    }

    if(!_aresUiRunningSubject.Value)
    {
      _uiTask = StartUi(_cancellationTokenSource.Token, demoMode) ?? Task.CompletedTask;
    }

    if(_aresBinaryManager.CurrentLayout == AresReleaseLayout.SplitUiAndService && !_aresServiceRunningSubject.Value)
    {
      _serviceTask = StartService(_cancellationTokenSource.Token) ?? Task.CompletedTask;
    }
  }

  public async Task Stop()
  {
    await _cancellationTokenSource.CancelAsync();
    try
    {
      await Task.WhenAll([_serviceTask, _uiTask, .. _demoServiceTasks]);
    }
    catch(OperationCanceledException)
    {
    }
    catch(CommandExecutionException e)
    {
      _logger.LogError("Error from execution: {Exception}", e);
    }
  }

  public async Task Restart()
  {
    await Stop();
    Start();
  }

  public void TakeOwnershipUi(Process uiProcess)
  {
    if(_aresUiRunningSubject.Value)
      throw new InvalidOperationException("We already have a UI process running. Can't take ownership of a new one before stopping the other one.");
    
    var uiTask = uiProcess.WaitForExitAndKillOnCancelAsync(_cancellationTokenSource.Token);
    ProcessUiTask(uiTask);
    _uiTask = uiTask;
  }

  public void TakeOwnershipService(Process serviceProcess)
  {
    if(_aresServiceRunningSubject.Value)
    {
      throw new InvalidOperationException("We already have a Service process running. Can't take ownership of a new one before stopping the other one.");
    }
    var serviceTask = serviceProcess.WaitForExitAndKillOnCancelAsync(_cancellationTokenSource.Token);
    ProcessServiceTask(serviceTask);
    _serviceTask = serviceTask;
  }

  private Task? StartService(CancellationToken cancellationToken)
  {
    var serviceExecutable = _executableGetter.GetServiceExecutablePath();
    if(serviceExecutable is null)
    {
      _logger.LogError("Couldn't resolve the path for service component.");
      return null;
    }
    var serviceExists = File.Exists(serviceExecutable);

    if(!serviceExists)
    {
      _logger.LogError("Service executable is not present. Don't know what to start.");
      return null;
    }

    var serviceDir = Path.GetDirectoryName(serviceExecutable) ?? "";

    var serviceTask = Cli.Wrap(serviceExecutable)
      .WithWorkingDirectory(serviceDir)
      .ExecuteAsync(cancellationToken)
      .Task;

    ProcessServiceTask(serviceTask);

    return serviceTask;
  }

  private Task? StartUi(CancellationToken cancellationToken, bool demoMode)
  {
    var uiExecutable = _executableGetter.GetUiExecutablePath();
    if(uiExecutable is null)
    {
      _logger.LogError("Couldn't resolve the path for ui component.");
      return null;
    }
    var uiExists = File.Exists(uiExecutable);

    if(!uiExists)
    {
      _logger.LogError("Ui executable is not present. Don't know what to start.");
      return null;
    }

    var uiDir = Path.GetDirectoryName(uiExecutable) ?? "";

    var command = Cli.Wrap(uiExecutable)
      .WithWorkingDirectory(uiDir);

    if(demoMode)
    {
      command = command.WithArguments("--demo");
    }

    var uiTask = command.ExecuteAsync(cancellationToken).Task;

    ProcessUiTask(uiTask);

    return uiTask;
  }

  private Task[] StartDemoServices(CancellationToken cancellationToken)
  {
    var tasks = new Task[DemoServiceExecutableNames.Length];
    for(var i = 0; i < DemoServiceExecutableNames.Length; i++)
    {
      var serviceName = DemoServiceExecutableNames[i];
      var executablePath = _executableGetter.GetDemoServiceExecutablePath(serviceName);
      if(executablePath is null || !File.Exists(executablePath))
      {
        _logger.LogError("Demo service executable is not present: {ExecutablePath}", executablePath);
        tasks[i] = Task.CompletedTask;
        continue;
      }

      var workingDirectory = Path.GetDirectoryName(executablePath);
      if(string.IsNullOrEmpty(workingDirectory))
      {
        _logger.LogError("Couldn't resolve the working directory for demo service {ServiceName}.", serviceName);
        tasks[i] = Task.CompletedTask;
        continue;
      }

      var demoServiceTask = Cli.Wrap(executablePath)
        .WithWorkingDirectory(workingDirectory)
        .ExecuteAsync(cancellationToken)
        .Task;

      ProcessDemoServiceTask(demoServiceTask, serviceName);
      tasks[i] = demoServiceTask;
    }

    return tasks;
  }

  private void ProcessUiTask(Task ui)
  {
    ui.ContinueWith(t =>
     {
       if(t.IsFaulted)
       {
         _logger.LogError(t.Exception, "UI task faulted; stopping ARES.");
         TriggerStopOnce();
       }
       else if(!t.IsCanceled)
       {
         _logger.LogInformation("UI task completed; stopping ARES.");
         TriggerStopOnce();
       }

       _aresUiRunningSubject.OnNext(false);
     }, TaskScheduler.Default);

    _aresUiRunningSubject.OnNext(true);
  }

  private void ProcessServiceTask(Task service)
  {
    service.ContinueWith(t =>
    {
      if(t.IsFaulted)
      {
        _logger.LogError(t.Exception, "Service task faulted; stopping ARES.");
        TriggerStopOnce();
      }
      else if(!t.IsCanceled)
      {
        _logger.LogInformation("Service task completed; stopping ARES.");
        TriggerStopOnce();
      }

      _aresServiceRunningSubject.OnNext(false);
    }, TaskScheduler.Default);

    _aresServiceRunningSubject.OnNext(true);
  }

  private void ProcessDemoServiceTask(Task demoService, string executableName)
  {
    demoService.ContinueWith(t =>
    {
      if(t.IsFaulted)
      {
        _logger.LogError(t.Exception, "Demo service {ExecutableName} task faulted; stopping ARES.", executableName);
        TriggerStopOnce();
      }
      else if(!t.IsCanceled)
      {
        _logger.LogInformation("Demo service {ExecutableName} task completed; stopping ARES.", executableName);
        TriggerStopOnce();
      }
    }, TaskScheduler.Default);
  }

  private void TriggerStopOnce()
  {
    if(Interlocked.Exchange(ref _stopInitiated, 1) == 0)
    {
      _ = Stop();
    }
  }

  private bool IsHealthyRunning()
  {
    return _aresBinaryManager.CurrentLayout == AresReleaseLayout.UnifiedUiOnly
      ? _aresUiRunningSubject.Value
      : _aresUiRunningSubject.Value && _aresServiceRunningSubject.Value;
  }
}
