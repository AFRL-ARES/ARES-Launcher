using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using ARESLauncher.Models;

namespace ARESLauncher.Configuration;

public class LauncherConfiguration
{
  [JsonIgnore] private static readonly string _appPath =
    Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) ?? Directory.GetCurrentDirectory();

  public AresSource CurrentAresRepo { get; set; } = new("AFRL-ARES", "ARES");

  public AresSource[] AvailableAresRepos { get; set; } = [new("AFRL-ARES", "ARES")];

  /// <summary>
  ///   BinariesRoot is used if a bundle is downloaded contining both the UI and Service
  /// </summary>
  public string BinariesRoot { get; set; } = Path.Combine(_appPath, "Binaries");

  public string UiBinaryPath { get; set; } = Path.Combine(_appPath, "Binaries");
  public string ServiceBinaryPath { get; set; } = Path.Combine(_appPath, "Binaries");

  public string AresDataPath { get; set; } = Path.Combine(_appPath, "Data");
  public string SqliteDatabasePath { get; set; } = Path.Combine(_appPath, "Data", "ares_database.db");

  public string SqlServerConnectionString { get; set; } =
    "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=ARES;Integrated Security=True;Pooling=False;";

  public string PostgresConnectionString { get; set; } =
    "Host=localhost;Database=ARES;Username=postgres;Password=postgres";

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.Sqlite;

  public string ServiceEndpoint { get; set; } = "https://localhost:5001";
  public string UiEndpoint { get; set; } = "https://localhost:7084";
  public string CertificatePath { get; } = Path.Combine(_appPath, "Data", "AresOS.pfx");
  public string CertificatePassword { get; } = "SecurePassword";
  public string GitToken { get; set; } = "";
}