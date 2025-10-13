using System;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public interface IAresStarter
{
  IObservable<bool> AresRunning { get; }
  void Start();
  Task Stop();
  Task Restart();
}