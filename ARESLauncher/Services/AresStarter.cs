using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
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
  
  public AresStarter(IExecutableGetter executableGetter, ILogger<AresStarter> logger)
  {
    _executableGetter = executableGetter;
    _logger = logger;
    AresRunning = _aresRunningSubject.AsObservable();
  }

  public IObservable<bool> AresRunning { get; }
  
  public void Start()
  {
    if (_aresRunningSubject.Value)
    {
      _logger.LogDebug("Start requested while ARES is already running. Skipping.");
      return;
    }

    var serviceExecutable = _executableGetter.GetServiceExecutablePath();
    var uiExecutable = _executableGetter.GetUiExecutablePath();
    if (serviceExecutable is null || uiExecutable is null)
    {
      _logger.LogError("Ui and/or Service executables are not present. Don't know what to start.");
      return;
    }

    _cancellationTokenSource = new CancellationTokenSource();
    
    _serviceTask = Cli.Wrap(serviceExecutable)
      .ExecuteAsync(_cancellationTokenSource.Token)
      .Task;
    
    _uiTask = Cli.Wrap(uiExecutable)
      .ExecuteAsync(_cancellationTokenSource.Token)
      .Task;

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
  }

  public async Task Restart()
  {
    await Stop();
    Start();
  }
}
