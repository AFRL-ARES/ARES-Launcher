using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace ARESLauncher.Tools;

public static class Unpacker
{
  public static Task Unpack(string compressedItem, string destinationDir)
  {
    ArgumentNullException.ThrowIfNull(compressedItem);
    ArgumentNullException.ThrowIfNull(destinationDir);

    if (!File.Exists(compressedItem))
      throw new ArgumentException("The compressed item must be a local file path.", nameof(compressedItem));

    if (File.Exists(destinationDir))
      throw new ArgumentException("The destination must be a local directory path.", nameof(destinationDir));

    var archivePath = Path.GetFullPath(compressedItem);
    if (!File.Exists(archivePath))
      throw new FileNotFoundException("The compressed file could not be found.", archivePath);

    var destinationPath = Path.GetFullPath(destinationDir);
    Directory.CreateDirectory(destinationPath);

    return Task.Run(() => ExtractArchive(archivePath, destinationPath));
  }

  private static void ExtractArchive(string archivePath, string destinationPath)
  {
    var extension = Path.GetExtension(archivePath);
    if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
      throw new NotSupportedException($"Unsupported archive type \"{extension}\".");

    ZipFile.ExtractToDirectory(archivePath, destinationPath, true);
  }
}