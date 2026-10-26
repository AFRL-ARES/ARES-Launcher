using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ARESLauncher.Services.Configuration;

namespace ARESLauncher.Services;

public class AresStarter : IAresStarter
{
  private readonly IAresBinaryManager _aresBinaryManager;
  private readonly IExecutableGetter _executableGetter;
  private readonly IAppConfigurationService _configurationService;
  private readonly IDatabaseManager _databaseManager;
  private readonly ILogger<AresStarter> _logger;
  private readonly BehaviorSubject<bool> _aresRunningSubject = new(false);
  private Task _uiTask = Task.CompletedTask;
  private Task _serviceTask = Task.CompletedTask;
  private CancellationTokenSource _cancellationTokenSource = new();
  private int _stopInitiated = 0;

  public AresStarter(IAresBinaryManager aresBinaryManager, 
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
    AresRunning = _aresRunningSubject.AsObservable();
  }

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

    if(!_aresRunningSubject.Value)
      _uiTask = StartUi(_cancellationTokenSource.Token, demoMode) ?? Task.CompletedTask;
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
    catch(Exception e)
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
    if(_aresRunningSubject.Value)
      throw new InvalidOperationException("We already have a UI process running. Can't take ownership of a new one before stopping the other one.");
    
    var uiTask = uiProcess.WaitForExitAndKillOnCancelAsync(_cancellationTokenSource.Token);
    ProcessAresTask(uiTask);
    _uiTask = uiTask;
  }

  private Task? StartUi(CancellationToken cancellationToken, bool demoMode)
  {
    var uiExecutable = _executableGetter.GetUiExecutablePath();
    if(uiExecutable is null)
    {
      _logger.LogError("Could not resolve the path for UI component.");
      return null;
    }
    var uiExists = File.Exists(uiExecutable);

    if(!uiExists)
    {
      _logger.LogError("UI executable is not present, unable to start ARES.");
      return null;
    }

    var uiDir = Path.GetDirectoryName(uiExecutable) ?? "";

    var startInfo = new ProcessStartInfo
    {
      FileName = uiExecutable,
      WorkingDirectory = uiDir,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    if(demoMode)
      startInfo.ArgumentList.Add("--demo");

    var process = new Process { StartInfo = startInfo };
    try
    {
      if(!process.Start())
        throw new InvalidOperationException("Failed to start the ARES process.");

      var uiTask = process.WaitForExitAndKillOnCancelAsync(cancellationToken);
      ProcessAresTask(uiTask);

      return uiTask;
    }
    catch(Exception ex)
    {
      ProcessExtensions.TerminateProcess(process);
      process.Dispose();
      var aresTask = Task.FromException<int>(ex);
      ProcessAresTask(aresTask);
      return aresTask;
    }
  }

  private void ProcessAresTask(Task ares)
  {
    ares.ContinueWith(t =>
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

       _aresRunningSubject.OnNext(false);
     }, TaskScheduler.Default);

    _aresRunningSubject.OnNext(true);
  }

  private void TriggerStopOnce()
  {
    if(Interlocked.Exchange(ref _stopInitiated, 1) == 0)
      _ = Stop();
  }

  private bool IsHealthyRunning()
  {
    return _aresRunningSubject.Value;
  }

  public IObservable<bool> AresRunning { get; }
}