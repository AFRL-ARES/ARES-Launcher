using System;

namespace ARESLauncher.Models;

public record DownloadResult(bool Success, string Error, Uri? ResultingFilePath);