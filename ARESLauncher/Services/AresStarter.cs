using System;
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
  private readonly BehaviorSubject<bool> _aresRunningSubject = new(false);

  private Task _uiTask = Task.CompletedTask;
  private Task _serviceTask = Task.CompletedTask;

  private CancellationTokenSource _cancellationTokenSource = new();
  private int _stopInitiated = 0;

  public AresStarter(IExecutableGetter executableGetter, ILogger<AresStarter> logger)
  {
    _executableGetter = executableGetter;
    _logger = logger;
    AresRunning = _aresRunningSubject.AsObservable();
  }

  public IObservable<bool> AresRunning { get; }

  public void Start()
  {
    if(_aresRunningSubject.Value)
    {
      _logger.LogDebug("Start requested while ARES is already running. Skipping.");
      return;
    }

    var serviceExecutable = _executableGetter.GetServiceExecutablePath();
    var uiExecutable = _executableGetter.GetUiExecutablePath();
    if(serviceExecutable is null || uiExecutable is null)
    {
      _logger.LogError("Couldn't resolve the paths for service and/or ui components.");
      return;
    }
    var serviceExists = File.Exists(serviceExecutable);
    var uiExists = File.Exists(uiExecutable);

    if(!serviceExists || !uiExists)
    {
      _logger.LogError("Ui and/or Service executables are not present. Don't know what to start.");
      return;
    }

    var serviceDir = Path.GetDirectoryName(serviceExecutable) ?? "";
    var uiDir = Path.GetDirectoryName(uiExecutable) ?? "";

    _cancellationTokenSource = new CancellationTokenSource();
    _stopInitiated = 0;

    _serviceTask = Cli.Wrap(serviceExecutable)
      .WithWorkingDirectory(serviceDir)
      .ExecuteAsync(_cancellationTokenSource.Token)
      .Task;

    _serviceTask.ContinueWith(t =>
    {
      if (t.IsFaulted)
      {
        _logger.LogError(t.Exception, "Service task faulted; stopping ARES.");
        TriggerStopOnce();
      }
      else if (!t.IsCanceled)
      {
        _logger.LogInformation("Service task completed; stopping ARES.");
        TriggerStopOnce();
      }
    }, TaskScheduler.Default);
    
    _uiTask = Cli.Wrap(uiExecutable)
      .WithWorkingDirectory(uiDir)
      .ExecuteAsync(_cancellationTokenSource.Token)
      .Task;

    _uiTask.ContinueWith(t =>
    {
      if (t.IsFaulted)
      {
        _logger.LogError(t.Exception, "UI task faulted; stopping ARES.");
        TriggerStopOnce();
      }
      else if (!t.IsCanceled)
      {
        _logger.LogInformation("UI task completed; stopping ARES.");
        TriggerStopOnce();
      }
    }, TaskScheduler.Default);

    void TriggerStopOnce()
    {
      if (Interlocked.Exchange(ref _stopInitiated, 1) == 0)
      {
        _ = Stop();
      }
    }

    Task.WhenAll(_serviceTask, _uiTask)
      .ContinueWith(_ => _aresRunningSubject.OnNext(false), TaskScheduler.Default);

    _aresRunningSubject.OnNext(true);
  }

  public async Task Stop()
  {
    await _cancellationTokenSource.CancelAsync();
    try
    {
      await Task.WhenAll(_serviceTask, _uiTask);
    }
    catch (OperationCanceledException)
    {
    }
    catch (CommandExecutionException e)
    {
      _logger.LogError("Error from execution: {Exception}", e);
    }
  }

  public async Task Restart()
  {
    await Stop();
    Start();
  }
}
