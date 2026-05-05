using System;
using System.Threading.Tasks;
using ARESLauncher.Models;
using NuGet.Versioning;

namespace ARESLauncher.Services;

public interface IAresUpdater
{
  IObservable<string> UpdateStepDescription { get; }

  IObservable<UpdateStep> CurrentUpdateStep { get; }

  IObservable<double> UpdateProgress { get; }

  Task<AresRelease[]> GetAvailableVersions();

  Task Update(SemanticVersion version);

  Task UpdateLatest();

  Task CreateSnapshot(SemanticVersion version);

  Task<bool> HasSnapshot(SemanticVersion version);

  Task RestoreSnapshot(SemanticVersion version);

  Task ResetDatabase();

  void InvalidateCache();
}