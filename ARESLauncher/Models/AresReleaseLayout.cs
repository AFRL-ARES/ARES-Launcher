using System.Text.Json.Serialization;

namespace ARESLauncher.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AresReleaseLayout
{
  SplitUiAndService,
  UnifiedUiOnly
}
