using System.Collections.Generic;
using System.Threading.Tasks;
using ARESLauncher.Models.PyAres;

namespace ARESLauncher.ViewModels;

public partial class MainViewModel
{
  public Task<IReadOnlyList<PyAresProcessInfo>> GetOrphanedPyAresProcessesAsync()
    => _pyAresManager.GetOrphanedProcessesAsync();

  public Task StopOrphanedPyAresProcessesAsync()
    => _pyAresManager.StopOrphanedProcessesAsync();

  public Task AttachExistingPyAresProcessesAsync()
    => _pyAresManager.AttachExistingProcessesAsync();
}
