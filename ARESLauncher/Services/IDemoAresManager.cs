using System.Threading.Tasks;

namespace ARESLauncher.Services;

public interface IDemoAresManager
{
  Task StartAll();
  Task StopAll();
}
