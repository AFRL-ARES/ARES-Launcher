using System.Text.RegularExpressions;

namespace ARESLauncher.Tools;

public static partial class AresVersionRegex
{
  [GeneratedRegex("[vV](\\d+(?:\\.\\d+)*(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?)")]
  public static partial Regex VersionRegex();
}
