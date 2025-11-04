using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public interface IAresStarter
{
  IObservable<bool> AresUiRunning { get; }
  IObservable<bool> AresServiceRunning { get; }

  void Start();
  Task Stop();
  Task Restart();

  void TakeOwnershipUi(Process uiProcess);
  void TakeOwnershipService(Process serviceProcess);
}