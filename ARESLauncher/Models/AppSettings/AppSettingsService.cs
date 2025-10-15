namespace ARESLauncher.Models.AppSettings;

public class AppSettingsService : AppSettingsBase
{
  public TokensConfig? TokensConfig { get; set; }
  public string? AresDataPath { get; set; }
}