using System;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public interface IAresStarter
{
  IObservable<bool> AresRunning { get; }
  bool CanStart { get; }
  Task Start();
  Task Stop();
  Task Restart();
}