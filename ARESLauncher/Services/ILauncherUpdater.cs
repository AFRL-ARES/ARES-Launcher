using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public interface ILauncherUpdater
{
  Task<SemanticVersion[]> GetAvailableVersions();
}
