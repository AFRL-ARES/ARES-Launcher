using System;
using System.IO;
using System.Text.Json;
using ARESLauncher.Models;
using NuGet.Versioning;

namespace ARESLauncher.Services;

public static class BinaryMetadataHelper
{
  public const string MetadataFileName = "areslauncher.metadata.json";

  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    WriteIndented = true
  };

  public static void WriteMetadata(string directory, AresSource source, SemanticVersion version)
  {
    if (string.IsNullOrWhiteSpace(directory)) return;

    var path = Path.Combine(directory, MetadataFileName);
    var metadata = new AresBinaryMetadata
    {
      Source = source,
      Version = version.ToNormalizedString()
    };

    try
    {
      var json = JsonSerializer.Serialize(metadata, SerializerOptions);
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
      File.WriteAllText(path, json);
    }
    catch (Exception)
    {
      // Metadata is a best-effort feature; intentionally swallow exceptions to avoid breaking updates.
    }
  }

  public static AresBinaryMetadata? ReadMetadata(string directory)
  {
    if (string.IsNullOrWhiteSpace(directory)) return null;
    var path = Path.Combine(directory, MetadataFileName);
    if (!File.Exists(path)) return null;

    try
    {
      var json = File.ReadAllText(path);
      if (string.IsNullOrWhiteSpace(json)) return null;
      return JsonSerializer.Deserialize<AresBinaryMetadata>(json, SerializerOptions);
    }
    catch (Exception)
    {
      return null;
    }
  }
}
