using System;
using System.IO;
using System.Text.Json;
using ARESLauncher.Configuration;

namespace ARESLauncher.Services.Configuration;

public class JsonAppConfigurationService : IAppConfigurationService
{
  private readonly object _syncRoot = new();
  private readonly JsonSerializerOptions _serializerOptions;
  private readonly string _configFilePath;

  public JsonAppConfigurationService()
  {
    _configFilePath = Path.Combine(AppContext.BaseDirectory, "areslauncher.config.json");
    _serializerOptions = new JsonSerializerOptions
    {
      WriteIndented = true
    };

    Current = LoadConfiguration();
    PersistCurrentInternal(Current);
  }

  public LauncherConfiguration Current { get; private set; }

  public void Update(Action<LauncherConfiguration> applyChanges)
  {
    if(applyChanges is null)
    {
      throw new ArgumentNullException(nameof(applyChanges));
    }

    lock(_syncRoot)
    {
      applyChanges(Current);
      PersistCurrentInternal(Current);
    }
  }

  private LauncherConfiguration LoadConfiguration()
  {
    if(!File.Exists(_configFilePath))
    {
      return new LauncherConfiguration();
    }

    try
    {
      var json = File.ReadAllText(_configFilePath);
      if(string.IsNullOrWhiteSpace(json))
      {
        return new LauncherConfiguration();
      }

      var configuration = JsonSerializer.Deserialize<LauncherConfiguration>(json, _serializerOptions);
      return configuration ?? new LauncherConfiguration();
    }
    catch(JsonException)
    {
      return new LauncherConfiguration();
    }
    catch(IOException)
    {
      return new LauncherConfiguration();
    }
  }

  private void PersistCurrentInternal(LauncherConfiguration configuration)
  {
    var directory = Path.GetDirectoryName(_configFilePath);
    if(!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    var json = JsonSerializer.Serialize(configuration, _serializerOptions);
    File.WriteAllText(_configFilePath, json);
  }
}
