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

  Task<SemanticVersion[]> GetAvailableVersions();

  Task Update(SemanticVersion version);

  Task UpdateLatest();
}