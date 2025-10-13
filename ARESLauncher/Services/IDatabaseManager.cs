using System.Threading.Tasks;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

public interface IDatabaseManager
{
  DatabaseStatus DatabaseStatus { get; }
  Task RunMigrations();
  
  /// <summary>
  /// Refreshes the status of the database as reported from the Ares Service
  /// </summary>
  /// <returns></returns>
  Task Refresh();
}