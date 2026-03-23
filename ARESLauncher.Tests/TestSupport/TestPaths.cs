using System;
using System.IO;

namespace ARESLauncher.Tests;

internal static class TestPaths
{
  public static string CreateTempDirectory()
  {
    var path = Path.Combine(Path.GetTempPath(), "ARESLauncher.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
  }

  public static void DeleteDirectoryIfExists(string path)
  {
    if(Directory.Exists(path))
      Directory.Delete(path, true);
  }
}
