using System.Threading.Tasks;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

public interface ISettingsUpdater
{
  Task UpdateSettings(AresComponent component);
}