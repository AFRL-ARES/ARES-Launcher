using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARESLauncher.Models;
using ARESLauncher.Models.AppSettings;

namespace ARESLauncher.Tools;

public static class AppSettingsHelper
{
  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    WriteIndented = true, 
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  public static void Update<T>(string path, Action<T> updateCallback) where T : AppSettingsBase
  {
    var existingConfig = LoadConfiguration<T>(path);
    updateCallback(existingConfig);
    PersistCurrentInternal(existingConfig, path);
  }

  private static T LoadConfiguration<T>(string path) where T : AppSettingsBase
  {
    if (!File.Exists(path))
      return Activator.CreateInstance<T>();

    try
    {
      var json = File.ReadAllText(path);
      if (string.IsNullOrWhiteSpace(json)) 
        return Activator.CreateInstance<T>();

      var configuration = JsonSerializer.Deserialize<T>(json, SerializerOptions);
      return configuration ?? Activator.CreateInstance<T>();
    }
    catch (JsonException)
    {
      return Activator.CreateInstance<T>();
    }
    catch (IOException)
    {
      return Activator.CreateInstance<T>();
    }
  }

  private static void PersistCurrentInternal<T>(T configuration, string path) where T : AppSettingsBase
  {
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

    // Use runtime type to include derived properties when serializing
    var json = JsonSerializer.Serialize(configuration, SerializerOptions);
    File.WriteAllText(path, json);
  }
}
