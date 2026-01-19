using System.Text.RegularExpressions;

namespace ARESLauncher.Tools;

public static partial class AresVersionRegex
{
  [GeneratedRegex("v([\\d.]+)")]
  public static partial Regex VersionRegex();
}
