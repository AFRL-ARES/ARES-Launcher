using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ARESLauncher.Configuration;
using ARESLauncher.Services.Configuration;
using CliWrap;
using CliWrap.Exceptions;
using Microsoft.Extensions.Logging;

namespace ARESLauncher.Services;

public class PyAresComponentStatus
{
  public string Name { get; set; } = "";
  public bool IsRunning { get; set; }
  public string? LastError { get; set; }
}

public interface IPyAresManager
{
  IObservable<bool> AnyPyAresRunning { get; }
  IObservable<IReadOnlyList<PyAresComponentStatus>> ComponentStatuses { get; }

  IObservable<string> GetOutput(string componentName);

  Task StartAll();
  Task StopAll();
}

public class PyAresManager : IPyAresManager
{
  private readonly IAppConfigurationService _configurationService;
  private readonly ILogger<PyAresManager> _logger;
  private readonly BehaviorSubject<bool> _anyRunningSubject = new(false);
  private readonly BehaviorSubject<IReadOnlyList<PyAresComponentStatus>> _statusSubject = new(new List<PyAresComponentStatus>());
  private readonly Dictionary<string, CancellationTokenSource> _componentTokens = new();
  private readonly Dictionary<string, BehaviorSubject<string>> _outputSubjects = new();

  public PyAresManager(IAppConfigurationService configurationService, ILogger<PyAresManager> logger)
  {
    _configurationService = configurationService;
    _logger = logger;
    AnyPyAresRunning = _anyRunningSubject.AsObservable();
    ComponentStatuses = _statusSubject.AsObservable();
  }

  public IObservable<bool> AnyPyAresRunning { get; }
  public IObservable<IReadOnlyList<PyAresComponentStatus>> ComponentStatuses { get; }

  public IObservable<string> GetOutput(string componentName)
  {
    if(!_outputSubjects.TryGetValue(componentName, out var subject))
    {
      subject = new BehaviorSubject<string>(string.Empty);
      _outputSubjects[componentName] = subject;
    }

    return subject.AsObservable();
  }

  public async Task StartAll()
  {
    var components = _configurationService.Current.PyAresComponents?.Where(c => c.Enabled && c.StartWithAres).ToArray() ?? Array.Empty<PyAresComponentConfig>();
    var statuses = new List<PyAresComponentStatus>();

    foreach(var component in components)
    {
      var status = new PyAresComponentStatus { Name = component.Name };
      statuses.Add(status);

      try
      {
        await StartComponentInternal(component, status);
      }
      catch(Exception ex)
      {
        status.IsRunning = false;
        status.LastError = ex.Message;
        _logger.LogError(ex, "Failed to start PyAres component {Name}", component.Name);
      }
    }

    _statusSubject.OnNext(statuses);
    _anyRunningSubject.OnNext(statuses.Any(s => s.IsRunning));
  }

  public async Task StopAll()
  {
    var tokens = _componentTokens.Values.ToArray();
    foreach(var cts in tokens)
    {
      try
      {
        await cts.CancelAsync();
      }
      catch(Exception)
      {
      }
    }

    _componentTokens.Clear();
    var statuses = (_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList();
    foreach(var status in statuses)
    {
      status.IsRunning = false;
    }
    _statusSubject.OnNext(statuses);
    _anyRunningSubject.OnNext(false);
  }

  private async Task StartComponentInternal(PyAresComponentConfig component, PyAresComponentStatus status)
  {
    var interpreter = string.IsNullOrWhiteSpace(component.PythonInterpreterPath) ? "python" : component.PythonInterpreterPath;
    var workingDir = string.IsNullOrWhiteSpace(component.WorkingDirectory) ? Directory.GetCurrentDirectory() : component.WorkingDirectory;

    if(!string.IsNullOrWhiteSpace(component.PythonInterpreterPath) && !File.Exists(component.PythonInterpreterPath))
    {
      throw new FileNotFoundException($"Python interpreter not found at {component.PythonInterpreterPath}");
    }

    if(!string.IsNullOrWhiteSpace(component.WorkingDirectory) && !Directory.Exists(component.WorkingDirectory))
    {
      throw new DirectoryNotFoundException($"Working directory not found: {component.WorkingDirectory}");
    }

    var cts = new CancellationTokenSource();
    _componentTokens[component.Name] = cts;

    // Ensure we have an output subject for this component and reset it
    if(!_outputSubjects.TryGetValue(component.Name, out var outputSubject))
    {
      outputSubject = new BehaviorSubject<string>(string.Empty);
      _outputSubjects[component.Name] = outputSubject;
    }
    else
    {
      outputSubject.OnNext(string.Empty);
    }

    var command = Cli.Wrap(interpreter)
      .WithWorkingDirectory(workingDir)
      .WithArguments(BuildArguments(component))
      .WithStandardOutputPipe(PipeTarget.ToDelegate(line => AppendOutput(component.Name, line)))
      .WithStandardErrorPipe(PipeTarget.ToDelegate(line => AppendOutput(component.Name, line)));

    var task = command.ExecuteAsync(cts.Token).Task;

    task.ContinueWith(t =>
    {
      if(t.IsFaulted)
      {
        status.IsRunning = false;
        status.LastError = t.Exception?.Message;
        _logger.LogError(t.Exception, "PyAres component {Name} faulted", component.Name);
      }
      else if(!t.IsCanceled)
      {
        status.IsRunning = false;
        _logger.LogInformation("PyAres component {Name} completed", component.Name);
      }

      _componentTokens.Remove(component.Name);
      _anyRunningSubject.OnNext(_componentTokens.Count > 0);
      _statusSubject.OnNext((_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList());
    }, TaskScheduler.Default);

    status.IsRunning = true;
    status.LastError = null;
    var statuses = (_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList();
    var existing = statuses.FirstOrDefault(s => s.Name == status.Name);
    if(existing is null)
      statuses.Add(status);
    _statusSubject.OnNext(statuses);
    _anyRunningSubject.OnNext(true);

    await Task.CompletedTask;
  }

  private void AppendOutput(string componentName, string line)
  {
    if(!_outputSubjects.TryGetValue(componentName, out var subject))
    {
      subject = new BehaviorSubject<string>(string.Empty);
      _outputSubjects[componentName] = subject;
    }

    var current = subject.Value ?? string.Empty;
    var updated = string.IsNullOrEmpty(current)
      ? line
      : current + Environment.NewLine + line;

    subject.OnNext(updated);
  }

  private static string BuildArguments(PyAresComponentConfig component)
  {
    var args = new List<string>();

    // Run Python unbuffered so print() output appears live
    args.Add("-u");

    if(!string.IsNullOrWhiteSpace(component.EntryPoint))
    {
      args.Add(component.EntryPoint);
    }
    if(!string.IsNullOrWhiteSpace(component.Arguments))
    {
      args.Add(component.Arguments);
    }
    return string.Join(" ", args);
  }
}
