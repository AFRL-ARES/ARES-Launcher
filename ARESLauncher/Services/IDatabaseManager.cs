using System.Threading.Tasks;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

public interface IDatabaseManager
{
  DatabaseStatus DatabaseStatus { get; }
  Task RunMigrations();
  Task CreateSnapshot(NuGet.Versioning.SemanticVersion version);
  Task<bool> HasSnapshot(NuGet.Versioning.SemanticVersion version);
  Task RestoreSnapshot(NuGet.Versioning.SemanticVersion version);
  Task Reset();
  
  /// <summary>
  /// Refreshes the status of the database as reported from the Ares Service
  /// </summary>
  /// <returns></returns>
  Task Refresh();
}