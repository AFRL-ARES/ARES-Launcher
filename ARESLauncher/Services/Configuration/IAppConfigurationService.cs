using System;
using ARESLauncher.Configuration;

namespace ARESLauncher.Services.Configuration;

public interface IAppConfigurationService
{
  LauncherConfiguration Current { get; }

  void Update(Action<LauncherConfiguration> applyChanges);

  event EventHandler ConfigUpdated;
}
