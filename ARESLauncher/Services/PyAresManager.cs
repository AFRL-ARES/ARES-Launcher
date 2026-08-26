using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ARESLauncher.Configuration;
using ARESLauncher.Models.PyAres;
using ARESLauncher.Services.Configuration;
using CliWrap;
using Microsoft.Extensions.Logging;

namespace ARESLauncher.Services;

public class PyAresManager : IPyAresManager
{
  private readonly IAppConfigurationService _configurationService;
  private readonly ILogger<PyAresManager> _logger;
  private readonly BehaviorSubject<bool> _anyRunningSubject = new(false);
  private readonly BehaviorSubject<IReadOnlyList<PyAresComponentStatus>> _statusSubject = new(new List<PyAresComponentStatus>());
  private readonly Dictionary<string, CancellationTokenSource> _componentTokens = new();
  private readonly Dictionary<string, Task> _componentTasks = new();
  private readonly Dictionary<string, BehaviorSubject<string>> _outputSubjects = new();
  private readonly Dictionary<string, int> _attachedProcesses = new();
  private readonly object _runtimeStateLock = new();
  private bool _autoRestartEnabled = true;
  private readonly string _runtimeStatePath;

  public PyAresManager(IAppConfigurationService configurationService, ILogger<PyAresManager> logger)
  {
    _configurationService = configurationService;
    _logger = logger;
    AnyPyAresRunning = _anyRunningSubject.AsObservable();
    ComponentStatuses = _statusSubject.AsObservable();

    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    _runtimeStatePath = Path.Combine(appData, "ARESLauncher", "PyAresRuntime.json");
  }

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
    _autoRestartEnabled = true;
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
    _autoRestartEnabled = false;
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
    _componentTasks.Clear();
    _attachedProcesses.Clear();

    UpdateRuntimeState(state => state.Components.Clear());

    var statuses = (_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList();
    foreach(var status in statuses)
    {
      status.IsRunning = false;
    }
    _statusSubject.OnNext(statuses);
    _anyRunningSubject.OnNext(false);
  }

  public async Task RestartComponent(string name)
  {
    var config = _configurationService.Current.PyAresComponents?.FirstOrDefault(c => c.Name == name);
    if(config is null)
    {
      _logger.LogWarning("Requested restart for PyAres component {Name}, but no configuration was found.", name);
      return;
    }

    if(_attachedProcesses.TryGetValue(name, out var attachedPid))
    {
      try
      {
        var proc = Process.GetProcessById(attachedPid);
        if(!proc.HasExited)
        {
          proc.Kill();
        }
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Failed to kill attached PyAres process {Name}", name);
      }

      _attachedProcesses.Remove(name);
      RemoveRuntimeEntry(name);
    }

    if(_componentTokens.TryGetValue(name, out var existingCts))
    {
      try
      {
        await existingCts.CancelAsync();
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Failed to cancel PyAres component {Name} during restart", name);
      }
    }

    if(_componentTasks.TryGetValue(name, out var existingTask))
    {
      try
      {
        var timeout = Task.Delay(TimeSpan.FromSeconds(3));
        var completed = await Task.WhenAny(existingTask, timeout);
        if(completed != existingTask)
        {
          _logger.LogWarning("Timeout waiting for PyAres component {Name} to stop during restart.", name);
        }
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Error while waiting for PyAres component {Name} to stop during restart.", name);
      }

      _componentTasks.Remove(name);
    }

    var statuses = (_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList();
    var status = statuses.FirstOrDefault(s => s.Name == name);
    if(status is null)
    {
      status = new PyAresComponentStatus { Name = name };
      statuses.Add(status);
      _statusSubject.OnNext(statuses);
    }

    try
    {
      await StartComponentInternal(config, status);
    }
    catch(Exception ex)
    {
      status.IsRunning = false;
      status.LastError = ex.Message;
      _logger.LogError(ex, "Failed to restart PyAres component {Name}", name);
      _statusSubject.OnNext(statuses);
      _anyRunningSubject.OnNext(statuses.Any(s => s.IsRunning));
    }
  }

  public Task<IReadOnlyList<PyAresProcessInfo>> GetOrphanedProcessesAsync()
  {
    var state = LoadRuntimeState();
    var infos = new List<PyAresProcessInfo>();

    foreach(var entry in state.Components)
    {
      var info = new PyAresProcessInfo
      {
        Name = entry.Name,
        Pid = entry.Pid,
        WorkingDirectory = entry.WorkingDirectory,
        EntryPoint = entry.EntryPoint,
        IsAlive = false
      };

      try
      {
        var proc = Process.GetProcessById(entry.Pid);
        info.IsAlive = !proc.HasExited;
      }
      catch(ArgumentException)
      {
        info.IsAlive = false;
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Error checking PyAres process {Pid}", entry.Pid);
        info.IsAlive = false;
      }

      if(info.IsAlive)
        infos.Add(info);
    }

    return Task.FromResult<IReadOnlyList<PyAresProcessInfo>>(infos);
  }

  public async Task StopOrphanedProcessesAsync()
  {
    var state = LoadRuntimeState();

    foreach(var entry in state.Components.ToArray())
    {
      try
      {
        var proc = Process.GetProcessById(entry.Pid);
        if(!proc.HasExited)
        {
          proc.Kill();
        }
      }
      catch(ArgumentException)
      {
        // Process already exited
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Failed to kill orphaned PyAres process {Name}", entry.Name);
      }
    }

    UpdateRuntimeState(s => s.Components.Clear());

    // Reflect that nothing is running anymore
    var statuses = (_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList();
    foreach(var status in statuses)
      status.IsRunning = false;

    _statusSubject.OnNext(statuses);
    _anyRunningSubject.OnNext(false);

    await Task.CompletedTask;
  }

  public Task AttachExistingProcessesAsync()
  {
    var state = LoadRuntimeState();
    var statuses = (_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList();

    foreach(var entry in state.Components)
    {
      try
      {
        var proc = Process.GetProcessById(entry.Pid);
        if(proc.HasExited)
          continue;
        

        _attachedProcesses[entry.Name] = entry.Pid;

        var status = statuses.FirstOrDefault(s => s.Name == entry.Name);
        if(status is null)
        {
          status = new PyAresComponentStatus { Name = entry.Name };
          statuses.Add(status);
        }

        status.IsRunning = true;
        status.LastError = null;
      }
      catch(ArgumentException)
      {
        // Process no longer exists; ignore
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Error attaching to existing PyAres process {Name}", entry.Name);
      }
    }

    _statusSubject.OnNext(statuses);
    _anyRunningSubject.OnNext(statuses.Any(s => s.IsRunning));

    return Task.CompletedTask;
  }

  private async Task StartComponentInternal(PyAresComponentConfig component, PyAresComponentStatus status)
  {
    var interpreter = string.IsNullOrWhiteSpace(component.PythonInterpreterPath) ? "python" : component.PythonInterpreterPath;
    var workingDir = string.IsNullOrWhiteSpace(component.WorkingDirectory) ? Directory.GetCurrentDirectory() : component.WorkingDirectory;

    if(!string.IsNullOrWhiteSpace(component.PythonInterpreterPath) && !File.Exists(component.PythonInterpreterPath))
      throw new FileNotFoundException($"Python interpreter not found at {component.PythonInterpreterPath}");

    if(!string.IsNullOrWhiteSpace(component.WorkingDirectory) && !Directory.Exists(component.WorkingDirectory))
      throw new DirectoryNotFoundException($"Working directory not found: {component.WorkingDirectory}");

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

    var commandTask = command.ExecuteAsync(cts.Token);
    var task = commandTask.Task;
    _componentTasks[component.Name] = task;

    // Persist runtime state with the new process id
    UpdateRuntimeState(state =>
    {
      var entry = state.Components.FirstOrDefault(c => c.Name == component.Name);
      if(entry is null)
      {
        entry = new PyAresRuntimeEntry { Name = component.Name };
        state.Components.Add(entry);
      }

      entry.Pid = commandTask.ProcessId;
      entry.WorkingDirectory = workingDir;
      entry.EntryPoint = component.EntryPoint ?? string.Empty;
    });

    task.ContinueWith(t =>

    {

      var autoRestart = false;
      var latestConfig = _configurationService.Current.PyAresComponents?.FirstOrDefault(c => c.Name == component.Name);
      var shouldAutoRestart = _autoRestartEnabled && ((latestConfig?.AutoRestart ?? component.AutoRestart));



      if(t.IsFaulted)

      {

        status.IsRunning = false;
        status.LastError = t.Exception?.Message;
        _logger.LogError(t.Exception, "PyAres component {Name} faulted", component.Name);

        if(shouldAutoRestart)
        {
          autoRestart = true;
        }

      }

      else if(!t.IsCanceled)

      {

        status.IsRunning = false;
        _logger.LogInformation("PyAres component {Name} completed", component.Name);

        if(shouldAutoRestart)
        {
          autoRestart = true;
        }

      }

      // If t.IsCanceled, we assume intentional stop and do not auto-restart here.

      _componentTokens.Remove(component.Name);
      _componentTasks.Remove(component.Name);
      RemoveRuntimeEntry(component.Name);



      if(autoRestart)

      {

        // Start a fresh instance; let the new call update status/subjects
        var nextConfig = latestConfig ?? component;
        _logger.LogInformation("PyAres component {Name} exited; auto-restarting", component.Name);

        _ = StartComponentInternal(nextConfig, status);
        return;

      }



      _anyRunningSubject.OnNext(_componentTokens.Count > 0);
      _statusSubject.OnNext((_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList());

    }, TaskScheduler.Default);

    status.IsRunning = true;
    status.LastError = null;
    var currentStatuses = (_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList();
    var existing = currentStatuses.FirstOrDefault(s => s.Name == status.Name);
    if(existing is null)
      currentStatuses.Add(status);
    _statusSubject.OnNext(currentStatuses);
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

  private PyAresRuntimeState LoadRuntimeState()
  {
    try
    {
      if(File.Exists(_runtimeStatePath))
      {
        var json = File.ReadAllText(_runtimeStatePath);
        var state = JsonSerializer.Deserialize<PyAresRuntimeState>(json);
        if(state is not null)
        {
          return state;
        }
      }
    }
    catch(Exception ex)
    {
      _logger.LogWarning(ex, "Failed to load PyAres runtime state from {Path}", _runtimeStatePath);
    }

    return new PyAresRuntimeState
    {
      LastUpdated = DateTime.UtcNow,
      Components = new List<PyAresRuntimeEntry>()
    };
  }

  private void SaveRuntimeState(PyAresRuntimeState state)
  {
    lock(_runtimeStateLock)
    {
      try
      {
        state.LastUpdated = DateTime.UtcNow;
        var directory = Path.GetDirectoryName(_runtimeStatePath);
        if(!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
          Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
          WriteIndented = true,
          DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        File.WriteAllText(_runtimeStatePath, json);
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Failed to save PyAres runtime state to {Path}", _runtimeStatePath);
      }
    }
  }

  private void UpdateRuntimeState(Action<PyAresRuntimeState> update)
  {
    var state = LoadRuntimeState();
    update(state);
    SaveRuntimeState(state);
  }

  private void RemoveRuntimeEntry(string name) 
  {
    UpdateRuntimeState(state =>
    {
      var entry = state.Components.FirstOrDefault(c => c.Name == name);
      if(entry is not null)
      {
        state.Components.Remove(entry);
      }
    });
  }

  public IObservable<bool> AnyPyAresRunning { get; }
  public IObservable<IReadOnlyList<PyAresComponentStatus>> ComponentStatuses { get; }
}
