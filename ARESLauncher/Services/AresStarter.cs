using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Exceptions;
using Microsoft.Extensions.Logging;

namespace ARESLauncher.Services;

public class AresStarter : IAresStarter
{
  private readonly IExecutableGetter _executableGetter;
  private readonly ILogger<AresStarter> _logger;
  private readonly BehaviorSubject<bool> _aresUiRunningSubject = new(false);
  private readonly BehaviorSubject<bool> _aresServiceRunningSubject = new(false);

  private Task _uiTask = Task.CompletedTask;
  private Task _serviceTask = Task.CompletedTask;

  private CancellationTokenSource _cancellationTokenSource = new();
  private int _stopInitiated = 0;

  public AresStarter(IExecutableGetter executableGetter, ILogger<AresStarter> logger)
  {
    _executableGetter = executableGetter;
    _logger = logger;
    AresUiRunning = _aresUiRunningSubject.AsObservable();
    AresServiceRunning = _aresServiceRunningSubject.AsObservable();
  }

  public IObservable<bool> AresUiRunning { get; }
  public IObservable<bool> AresServiceRunning { get; }

  public void Start()
  {
    if(_aresUiRunningSubject.Value && _aresServiceRunningSubject.Value)
    {
      _logger.LogDebug("Start requested while ARES is already running. Skipping.");
      return;
    }

    _cancellationTokenSource = new CancellationTokenSource();
    _stopInitiated = 0;

    if(!_aresUiRunningSubject.Value)
    {
      var uiTask = StartUi(_cancellationTokenSource.Token);
      if(uiTask is null)
      {
        _uiTask = Task.CompletedTask;
      }
    }

    if(!_aresServiceRunningSubject.Value)
    {
      var serviceTask = StartService(_cancellationTokenSource.Token);
      if(serviceTask is null)
      {
        _serviceTask = Task.CompletedTask;
      }
    }
  }

  public async Task Stop()
  {
    await _cancellationTokenSource.CancelAsync();
    try
    {
      await Task.WhenAll(_serviceTask, _uiTask);
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
    {
      throw new InvalidOperationException("We already have a UI process running. Can't take ownership of a new one before stopping the other one.");
    }
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

  private Task? StartUi(CancellationToken cancellationToken)
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

    var uiTask = Cli.Wrap(uiExecutable)
      .WithWorkingDirectory(uiDir)
      .ExecuteAsync(cancellationToken)
      .Task;

    ProcessUiTask(uiTask);

    return uiTask;
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

  private void TriggerStopOnce()
  {
    if(Interlocked.Exchange(ref _stopInitiated, 1) == 0)
    {
      _ = Stop();
    }
  }
}
