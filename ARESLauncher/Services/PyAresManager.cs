using ARESLauncher.Configuration;
using ARESLauncher.Models.PyAres;
using ARESLauncher.Services.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

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
  private List<PyAresComponentStatus> _statuses = new List<PyAresComponentStatus>();

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
    _statuses = new List<PyAresComponentStatus>();

    foreach(var component in components)
    {
      var status = new PyAresComponentStatus { Name = component.Name };
      _statuses.Add(status);

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

    _statusSubject.OnNext(_statuses);
    _anyRunningSubject.OnNext(_statuses.Any(s => s.IsRunning));
  }

  public async Task StopAll()
  {
    _autoRestartEnabled = false;
    var tokens = _componentTokens.Values.ToArray();
    var componentTasks = _componentTasks.Values.ToArray();

    foreach(var cts in tokens)
    {
      try
      {
        await cts.CancelAsync();
      }
      catch(OperationCanceledException)
      {
        // The command task is expected to observe cancellation while stopping.
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Failed to request shutdown for a PyAres component.");
      }
    }

    try
    {
      await Task.WhenAll(componentTasks);
    }
    catch(OperationCanceledException)
    {
      // Cancellation is the normal completion path for launcher-managed components.
    }
    catch(Exception ex)
    {
      _logger.LogWarning(ex, "A PyAres component faulted while stopping.");
    }

    _componentTokens.Clear();
    _componentTasks.Clear();
    _attachedProcesses.Clear();

    UpdateRuntimeState(state => state.Components.Clear());

    var statuses = (_statusSubject.Value ?? Array.Empty<PyAresComponentStatus>()).ToList();
    foreach(var status in statuses)
      status.IsRunning = false;
    
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
          proc.Kill();
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
          _logger.LogWarning("Timeout waiting for PyAres component {Name} to stop during restart.", name);
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
          proc.Kill();
        
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

    foreach(var entry in state.Components)
    {
      try
      {
        var proc = Process.GetProcessById(entry.Pid);
        if(proc.HasExited)
          continue;
        

        _attachedProcesses[entry.Name] = entry.Pid;

        var status = _statuses.FirstOrDefault(s => s.Name == entry.Name);
        if(status is null)
        {
          status = new PyAresComponentStatus { Name = entry.Name };
          _statuses.Add(status);
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

    _statusSubject.OnNext(_statuses);
    _anyRunningSubject.OnNext(_statuses.Any(s => s.IsRunning));

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

    var startedProcess = StartPythonProcess(interpreter, workingDir, component, cts.Token);
    var task = startedProcess.Task;
    var processId = startedProcess.ProcessId;
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

      entry.Pid = processId;
      entry.WorkingDirectory = workingDir;
      entry.EntryPoint = component.EntryPoint ?? string.Empty;
    });

    _ = task.ContinueWith(t =>
    {
      var autoRestart = false;
      var latestConfig = _configurationService.Current.PyAresComponents?.FirstOrDefault(c => c.Name == component.Name);
      var shouldAutoRestart = _autoRestartEnabled && ((latestConfig?.AutoRestart ?? component.AutoRestart));

      var intentionallyStopped = cts.IsCancellationRequested;
      if(t.IsFaulted)
      {
        status.IsRunning = false;
        status.LastError = t.Exception?.Message;
        _logger.LogError(t.Exception, "PyAres component {Name} faulted", component.Name);

        if(!intentionallyStopped && shouldAutoRestart)
          autoRestart = true;
      }

      else if(!intentionallyStopped)
      {
        status.IsRunning = false;
        _logger.LogInformation("PyAres component {Name} completed", component.Name);

        if(shouldAutoRestart)
          autoRestart = true;

      }
      else
      {
        status.IsRunning = false;
        status.LastError = null;
      }

      if(_componentTokens.TryGetValue(component.Name, out var currentCts) && ReferenceEquals(currentCts, cts))
        _componentTokens.Remove(component.Name);

      if(_componentTasks.TryGetValue(component.Name, out var currentTask) && ReferenceEquals(currentTask, task))
        _componentTasks.Remove(component.Name);

      RemoveRuntimeEntry(component.Name, processId);

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

  private StartedPythonProcess StartPythonProcess(
    string interpreter,
    string workingDirectory,
    PyAresComponentConfig component,
    CancellationToken cancellationToken)
  {
    var process = new Process
    {
      StartInfo = new ProcessStartInfo
      {
        FileName = interpreter,
        Arguments = BuildArguments(component),
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      }
    };

    process.OutputDataReceived += (_, eventArgs) =>
    {
      if(eventArgs.Data is not null)
        AppendOutput(component.Name, eventArgs.Data);
    };
    process.ErrorDataReceived += (_, eventArgs) =>
    {
      if(eventArgs.Data is not null)
        AppendOutput(component.Name, eventArgs.Data);
    };

    try
    {
      if(!process.Start())
        throw new InvalidOperationException($"Failed to start PyAres component {component.Name}.");

      var processId = process.Id;
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();

      return new StartedPythonProcess(
        processId,
        WaitForExitAndTerminateOnCancellationAsync(process, cancellationToken));
    }
    catch
    {
      TerminateProcess(process);
      process.Dispose();
      throw;
    }
  }

  private static async Task WaitForExitAndTerminateOnCancellationAsync(Process process, CancellationToken cancellationToken)
  {
    var cancellationRegistration = cancellationToken.Register(() => TerminateProcess(process));
    try
    {
      await process.WaitForExitAsync();
      process.WaitForExit();
    }
    finally
    {
      cancellationRegistration.Dispose();
      process.Dispose();
    }
  }

  private static void TerminateProcess(Process process)
  {
    try
    {
      if(OperatingSystem.IsWindows())
      {
        if(TryStartWindowsProcessTreeTermination(process.Id))
          return;

        process.Kill();
        return;
      }

      process.Kill(entireProcessTree: true);
    }
    catch(InvalidOperationException)
    {
      // The process exited before the cancellation callback reached it.
    }
    catch(Win32Exception)
    {
      // Windows released the process handle while termination was in progress.
    }
  }

  private static bool TryStartWindowsProcessTreeTermination(int processId)
  {
    try
    {
      var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
      var executablePath = string.IsNullOrEmpty(systemDirectory) ? "taskkill.exe" : Path.Combine(systemDirectory, "taskkill.exe");
      var startInfo = new ProcessStartInfo
      {
        FileName = executablePath,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      startInfo.ArgumentList.Add("/PID");
      startInfo.ArgumentList.Add(processId.ToString());
      startInfo.ArgumentList.Add("/T");
      startInfo.ArgumentList.Add("/F");

      var terminator = Process.Start(startInfo);
      if(terminator is null)
        return false;

      _ = DisposeProcessWhenExitedAsync(terminator);

      return true;
    }
    catch(InvalidOperationException)
    {
      return false;
    }
    catch(Win32Exception)
    {
      return false;
    }
  }

  private static async Task DisposeProcessWhenExitedAsync(Process process)
  {
    try
    {
      await process.WaitForExitAsync();
    }
    catch(InvalidOperationException)
    {
    }
    catch(Win32Exception)
    {
    }
    finally
    {
      process.Dispose();
    }
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
      args.Add(component.EntryPoint);
    
    if(!string.IsNullOrWhiteSpace(component.Arguments))
      args.Add(component.Arguments);

    return string.Join(" ", args);
  }

  public async Task StartComponent(string name)
  {
    var component = _configurationService.Current.PyAresComponents?.FirstOrDefault(c => c.Name == name);
    if(component is null)
      return;

    if(_componentTokens.ContainsKey(name))
    {
      _logger.LogInformation("PyAres component {Name} is already running or stopping.", name);
      return;
    }

    var status = new PyAresComponentStatus { Name = name };
    _statuses.Add(status);
    _statusSubject.OnNext(_statuses);

    await StartComponentInternal(component, status);
  }

  public Task StopComponent(string name)
  {
    if(_attachedProcesses.TryGetValue(name, out var attachedPid))
    {
      try
      {
        var proc = Process.GetProcessById(attachedPid);
        
        if(!proc.HasExited)
          proc.Kill();
      }
      catch(ArgumentException)
      {
        // Process already exited
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Failed to kill attached PyAres process {Name}", name);
        Console.WriteLine($"Failed to kill attached PyAres process {name}");
      }

      _attachedProcesses.Remove(name);
      RemoveRuntimeEntry(name);
    }

    if(_componentTokens.TryGetValue(name, out var existingCts))
    {
      _ = RequestComponentCancellationAsync(name, existingCts);
    }

    return Task.CompletedTask;
  }

  private async Task RequestComponentCancellationAsync(string name, CancellationTokenSource cancellationTokenSource)
  {
    try
    {
      await cancellationTokenSource.CancelAsync();
    }
    catch(OperationCanceledException)
    {
      // The process task handles cancellation as part of its normal shutdown path.
    }
    catch(Exception ex)
    {
      _logger.LogWarning(ex, "Failed to cancel PyAres component {Name} during stop", name);
    }
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
          return state;
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
          Directory.CreateDirectory(directory);

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

  private void RemoveRuntimeEntry(string name, int? expectedPid = null)
  {
    UpdateRuntimeState(state =>
    {
      var entry = state.Components.FirstOrDefault(c => c.Name == name);
      if(entry is not null && (!expectedPid.HasValue || entry.Pid == expectedPid.Value))
      {
        state.Components.Remove(entry);
      }
    });
  }

  public IObservable<bool> AnyPyAresRunning { get; }
  public IObservable<IReadOnlyList<PyAresComponentStatus>> ComponentStatuses { get; }

  private sealed record StartedPythonProcess(int ProcessId, Task Task);
}
