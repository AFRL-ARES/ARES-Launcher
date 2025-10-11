using System.Threading.Tasks;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

public interface IDatabaseManager
{
  Task<DatabaseStatus> GetStatus();
  Task RunMigrations();
  Task<DatabaseProvider> GetCurrentProvider();
  Task<string> GetConnectionString();
  Task SetProvider(DatabaseProvider provider);
  Task SetConnectionString(string connectionString);
}