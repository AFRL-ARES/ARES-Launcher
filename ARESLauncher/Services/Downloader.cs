using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ARESLauncher.Models;
using Microsoft.Extensions.Logging;

namespace ARESLauncher.Services;

public class Downloader(ILogger<Downloader> _logger) : IDownloader
{
  public async Task<DownloadResult> Download(Uri source, Uri destination)
  {
    using var client = new HttpClient();
    using var response = await client.GetAsync(source);
    if (!response.IsSuccessStatusCode)
    {
      return new DownloadResult(false, response.ReasonPhrase ?? "Unknown Error :)", null);
    }
    
    if (!IsValidSource(response))
    {
      return new DownloadResult(false, "The downloader only downloads files, not directories", null);
    }
    
    var (ensureSuccess, ensureError, ensurePath) = EnsureDestinationIsGud(destination, response);
    if (!ensureSuccess)
    {
      return new DownloadResult(false, ensureError, null);
    }
    
    await using var fileStream = new FileStream(ensurePath, FileMode.Create);
    await response.Content.CopyToAsync(fileStream);

    return new DownloadResult(true, "", new Uri(Path.GetFullPath(ensurePath)));
  }

  private static bool IsValidSource(HttpResponseMessage response)
  {
    var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
    var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";

    return !(contentType.Contains("text/html") && finalUrl.EndsWith('/'));
  }

  private (bool Success, string Error, string Destination) EnsureDestinationIsGud(Uri destination, HttpResponseMessage response)
  {
    var filePath = destination.LocalPath;

    if (ShouldTreatAsDirectory(destination, filePath))
    {
      var fileName = ResolveFileName(response) ?? "new_download";
      filePath = Path.Combine(filePath, fileName);
    }

    filePath = Path.GetFullPath(filePath);

    var directoryPath = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrEmpty(directoryPath))
    {
      try
      {
        Directory.CreateDirectory(directoryPath);
      }
      catch (Exception e)
      {
        _logger.LogError("Failed to ensure the destination directory exists. {}", e);
        return new ValueTuple<bool, string, string>(false, $"Failed to ensure the destination directory exists. {e.Message}", "");
      }
    }

    return new ValueTuple<bool, string, string>(true, "", filePath);
  }

  private static bool ShouldTreatAsDirectory(Uri destination, string path)
  {
    if (!destination.IsFile)
    {
      return true;
    }

    if (Directory.Exists(path))
    {
      return true;
    }

    if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
    {
      return true;
    }

    return string.IsNullOrEmpty(Path.GetFileName(path));
  }

  private static string? ResolveFileName(HttpResponseMessage response)
  {
    var disposition = response.Content.Headers.ContentDisposition;
    if (disposition == null)
    {
      return null;
    }

    var candidate = !string.IsNullOrWhiteSpace(disposition.FileNameStar)
      ? disposition.FileNameStar
      : disposition.FileName;

    if (string.IsNullOrWhiteSpace(candidate))
    {
      return null;
    }

    var trimmed = candidate.Trim().Trim('"');
    var safeName = Path.GetFileName(trimmed);

    return string.IsNullOrWhiteSpace(safeName) ? null : safeName;
  }
}
