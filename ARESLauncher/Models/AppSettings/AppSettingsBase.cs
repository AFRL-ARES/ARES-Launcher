using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ARESLauncher.Models.AppSettings;

/// <summary>
/// Represents the root application settings.
/// </summary>
public abstract class AppSettingsBase
{

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.None;

  public Dictionary<DatabaseProvider, string> ConnectionStrings { get; set; } = [];

  public LoggingOptions? Logging { get; set; }

  public string? AllowedHosts { get; set; }

  public KestrelOptions? Kestrel { get; set; }

  public CertificateSettings? CertificateSettings { get; set; }
}

public class RemoteServiceSettings
{
  public string? ServerHost { get; set; } = "localhost";
  public int ServerPort { get; set; } = 5001;
}

/// <summary>
/// Configuration for JWT tokens.
/// </summary>
public class TokensConfig
{
  public string? Issuer { get; set; }
  public string? Audience { get; set; }
  public string? Key { get; set; }
}

/// <summary>
/// Configuration for logging levels.
/// </summary>
public class LoggingOptions
{
  public Dictionary<string, string>? LogLevel { get; set; }
}

/// <summary>
/// Configuration for the Kestrel web server.
/// </summary>
public class KestrelOptions
{
  public EndpointDefaultsOptions? EndpointDefaults { get; set; }
  public EndpointsOptions? Endpoints { get; set; }
}

public class EndpointDefaultsOptions
{
  public string? Protocols { get; set; }
}

public class EndpointsOptions
{
  public HttpEndpoint? Http { get; set; }
  public HttpsEndpoint? Https { get; set; }
}

public class HttpEndpoint
{
  public string? Url { get; set; }
}

public class HttpsEndpoint
{
  public string? Url { get; set; }
  public CertificateOptions? Certificate { get; set; }
}

public class CertificateOptions
{
  public string? Path { get; set; }
  public string? Password { get; set; }
}

/// <summary>
/// General certificate settings.
/// </summary>
public class CertificateSettings
{
  public string? Path { get; set; }
  //public string? KeyPath { get; set; } = null; // not sure if this is needed
  public string? Password { get; set; }
}