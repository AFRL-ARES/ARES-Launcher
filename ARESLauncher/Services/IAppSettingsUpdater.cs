using ARESLauncher.Models;

namespace ARESLauncher.Services;

/// <summary>
/// This interface is responsible for grabbing existing AppSettings for given components, and updating them with
/// the values set within the Launcher Configuration
/// </summary>
public interface IAppSettingsUpdater
{
  void Update(AresComponent component);
  void UpdateAll();
}