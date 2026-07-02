using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;
using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public class ConflictManager(IAresStarter _aresStarter, IAppConfigurationService _configurationService, IAresBinaryManager _aresBinaryManager) : IConflictManager
{

  public bool FindPotentialService()
  {
    if(_aresBinaryManager.CurrentLayout == AresReleaseLayout.UnifiedUiOnly)
      return false;

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
    var serviceProcess = _aresBinaryManager.CurrentLayout == AresReleaseLayout.SplitUiAndService
      ? GetServiceProcess()
      : null;
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
    if(_aresBinaryManager.CurrentLayout == AresReleaseLayout.UnifiedUiOnly)
      return;

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

    var processOwner = GetProcessOwner(process.Id);
    var currentUser = GetCurrentUser();

    if(currentUser != processOwner)
    {
      IsCurrentUserProcessOwner = false;
      return;
    }

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

  private static string GetProcessOwner(int processId)
  {
    if(OperatingSystem.IsWindows())
      return GetProcessOwnerWindows(processId);

    else
      return GetProcessOwnerUnix(processId);
  }

  private static string GetProcessOwnerWindows(int processId)
  {
    if(!OperatingSystem.IsWindows())
      return "Unknown User";

    try
    {
      string query = "Select * From Win32_Process Where ProcessID = " + processId;
      using(ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
      {
        foreach(ManagementObject obj in searcher.Get())
        {
          string[] argList = new string[] { string.Empty, string.Empty };
          int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
          if(returnVal == 0)
          {
            return $"{argList[1]}\\{argList[0]}";
          }
        }
      }
    }
    catch
    {
      // Handle WMI/Permissions exceptions
    }
    return "Unknown User";
  }

  private static string GetProcessOwnerUnix(int processId)
  {
    try
    {
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "ps",
          Arguments = $"-o user= -p {processId}",
          RedirectStandardOutput = true,
          UseShellExecute = false,
          CreateNoWindow = true
        }
      };

      process.Start();
      string output = process.StandardOutput.ReadToEnd().Trim();
      process.WaitForExit();

      return string.IsNullOrEmpty(output) ? "Unknown User" : output;
    }
    catch
    {
      return "Unknown User";
    }
  }

  public static string GetCurrentUser()
  {
    if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return $"{Environment.UserDomainName}\\{Environment.UserName}";

    if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return Environment.UserName;

    return "Unknown User";
  }

  public bool IsCurrentUserProcessOwner { get; set; } = true;
}
