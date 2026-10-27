using System.Threading.Tasks;

namespace ARESLauncher.Services;

public interface ISettingsUpdater
{
  Task UpdateSettings();
}