namespace ARESLauncher.Models;

public record DownloadResult(bool Success, string Error, string? ResultingFilePath);