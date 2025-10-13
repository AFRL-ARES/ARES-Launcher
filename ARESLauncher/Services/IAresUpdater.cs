using System;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace ARESLauncher.Services;

public interface IAresUpdater
{
  IObservable<string> UpdateStep { get; }

  IObservable<double> UpdateProgress { get; }
  Task<SemanticVersion[]> GetAvailableVersions();

  Task Update(SemanticVersion version);
}