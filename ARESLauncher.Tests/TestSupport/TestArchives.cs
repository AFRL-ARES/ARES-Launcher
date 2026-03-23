using System.IO;
using System.IO.Compression;

namespace ARESLauncher.Tests;

internal static class TestArchives
{
  public static string CreateArchive(string root, string fileName, params (string Path, string Content)[] entries)
  {
    var archivePath = Path.Combine(root, fileName);

    using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
    foreach(var (path, content) in entries)
    {
      var entry = archive.CreateEntry(path);
      using var writer = new StreamWriter(entry.Open());
      writer.Write(content);
    }

    return archivePath;
  }
}
