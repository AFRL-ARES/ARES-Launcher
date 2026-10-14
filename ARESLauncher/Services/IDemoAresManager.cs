using System.Threading.Tasks;

namespace ARESLauncher.Services;

public interface IDemoAresManager
{
  Task StopOrphanedProcessesAsync();
  Task StartAll();
  Task StopAll();
}