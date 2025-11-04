using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ARESLauncher.Services.Configuration;

namespace ARESLauncher.Services;

public class ConflictManager(IAresStarter _aresStarter, IAppConfigurationService _configurationService) : IConflictManager
{

  public bool FindPotentialService()
  {
    var process = GetServiceProcess();
    return process is not null; ;
  }

  public bool FindPotentialUi()
  {
    var process = GetUiProcess();
    return process is not null;
  }

  public async Task Kill()
  {
    var uiProcess = GetUiProcess();
    var serviceProcess = GetServiceProcess();
    if(uiProcess is not null)
    {
      uiProcess.Kill();
      await uiProcess.WaitForExitAsync();
    }

    if(serviceProcess is not null)
    {
      serviceProcess.Kill();
      await serviceProcess.WaitForExitAsync();
    }
  }

  public void TakeOverService()
  {
    var process = GetServiceProcess();
    if(process is null)
      return;

    _aresStarter.TakeOwnershipService(process);
  }

  public void TakeOverUi()
  {
    var process = GetUiProcess();
    if(process is null)
      return;

    _aresStarter.TakeOwnershipUi(process);
  }

  private Process? GetUiProcess()
  {
    var uiName = _configurationService.Current.AresUiProcessName;
    return GetProcess(uiName);
  }

  private Process? GetServiceProcess()
  {
    var serviceName = _configurationService.Current.AresServiceProcessName;
    return GetProcess(serviceName);
  }

  private static Process? GetProcess(string name)
  {
    var processes = Process.GetProcessesByName(name);
    if(!processes.Any())
      return null;

    return processes[0];
  }
}
