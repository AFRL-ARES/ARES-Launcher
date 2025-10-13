using System;
using System.IO;
using System.Text.Json;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

public static class AppSettingsHelper
{
  private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

  public static void Update(string path, Action<AppSettings> updateCallback)
  {
    var existingConfig = LoadConfiguration(path);
    updateCallback(existingConfig);
    PersistCurrentInternal(existingConfig, path);
  }

  private static AppSettings LoadConfiguration(string path)
  {
    if (!File.Exists(path)) return new AppSettings();

    try
    {
      var json = File.ReadAllText(path);
      if (string.IsNullOrWhiteSpace(json)) return new AppSettings();

      var configuration = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
      return configuration ?? new AppSettings();
    }
    catch (JsonException)
    {
      return new AppSettings();
    }
    catch (IOException)
    {
      return new AppSettings();
    }
  }

  private static void PersistCurrentInternal(AppSettings configuration, string path)
  {
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

    var json = JsonSerializer.Serialize(configuration, SerializerOptions);
    File.WriteAllText(path, json);
  }
}