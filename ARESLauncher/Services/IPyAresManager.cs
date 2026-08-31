using ARESLauncher.Models.PyAres;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public interface IPyAresManager
{
  IObservable<bool> AnyPyAresRunning { get; }
  IObservable<IReadOnlyList<PyAresComponentStatus>> ComponentStatuses { get; }

  IObservable<string> GetOutput(string componentName);

  Task StartAll();
  Task StopAll();
  Task RestartComponent(string name);

  Task StopComponent(string name);
  Task StartComponent(string name);

  Task<IReadOnlyList<PyAresProcessInfo>> GetOrphanedProcessesAsync();
  Task StopOrphanedProcessesAsync();
  Task AttachExistingProcessesAsync();
}

