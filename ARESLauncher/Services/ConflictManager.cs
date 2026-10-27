using ARESLauncher.Models;
using ARESLauncher.Services.Configuration;
using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public class ConflictManager(IAresStarter _aresStarter, IAppConfigurationService _configurationService) : IConflictManager
{
  public bool FindPotentialAres()
  {
    var process = GetAresProcess();
    return process is not null;
  }

  public async Task Kill()
  {
    var aresProcess = GetAresProcess();

    if(aresProcess is not null)
    {
      aresProcess.Kill();
      await aresProcess.WaitForExitAsync();
    }
  }

  public void TakeOverAres()
  {
    var process = GetAresProcess();

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

  private Process? GetAresProcess()
  {
    var aresName = _configurationService.Current.AresUiProcessName;
    return GetProcess(aresName);
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