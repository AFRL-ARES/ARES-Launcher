using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

public static class Downloader
{
  public static async Task<DownloadResult> Download(Uri source, string destination, IProgress<double>? progress = null)
  {
    using var client = new HttpClient();
    using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);

    if (!response.IsSuccessStatusCode)
      return new DownloadResult(false, response.ReasonPhrase ?? "Unknown Error :)", null);

    if (!IsValidSource(response))
      return new DownloadResult(false, "The downloader only downloads files, not directories", null);

    var (ensureSuccess, ensureError, ensurePath) = EnsureDestinationIsGud(destination, response);
    if (!ensureSuccess) return new DownloadResult(false, ensureError, null);

    await using var fileStream = new FileStream(ensurePath, FileMode.Create, FileAccess.Write, FileShare.None);
    await using var networkStream = await response.Content.ReadAsStreamAsync();
    await CopyWithProgressAsync(networkStream, fileStream, response.Content.Headers.ContentLength, progress);

    return new DownloadResult(true, "", Path.GetFullPath(ensurePath));
  }

  private static bool IsValidSource(HttpResponseMessage response)
  {
    var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
    var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";

    return !(contentType.Contains("text/html") && finalUrl.EndsWith('/'));
  }

  private static (bool Success, string Error, string Destination) EnsureDestinationIsGud(string destination,
    HttpResponseMessage response)
  {
    if (ShouldTreatAsDirectory(destination))
    {
      var fileName = ResolveFileName(response) ?? "new_download";
      destination = Path.Combine(destination, fileName);
    }

    destination = Path.GetFullPath(destination);

    var directoryPath = Path.GetDirectoryName(destination);
    if (!string.IsNullOrEmpty(directoryPath))
      try
      {
        Directory.CreateDirectory(directoryPath);
      }
      catch (Exception e)
      {
        return new ValueTuple<bool, string, string>(false,
          $"Failed to ensure the destination directory exists. {e.Message}", "");
      }

    return new ValueTuple<bool, string, string>(true, "", destination);
  }

  private static bool ShouldTreatAsDirectory(string path)
  {
    if (File.Exists(path)) return false;

    if (Directory.Exists(path)) return true;

    if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) return true;

    return string.IsNullOrEmpty(Path.GetFileName(path));
  }

  private static string? ResolveFileName(HttpResponseMessage response)
  {
    var disposition = response.Content.Headers.ContentDisposition;
    if (disposition == null) return null;

    var candidate = !string.IsNullOrWhiteSpace(disposition.FileNameStar)
      ? disposition.FileNameStar
      : disposition.FileName;

    if (string.IsNullOrWhiteSpace(candidate)) return null;

    var trimmed = candidate.Trim().Trim('"');
    var safeName = Path.GetFileName(trimmed);

    return string.IsNullOrWhiteSpace(safeName) ? null : safeName;
  }

  private static async Task CopyWithProgressAsync(Stream source, Stream destination, long? contentLength,
    IProgress<double>? progress)
  {
    const int bufferSize = 81920;
    var buffer = new byte[bufferSize];
    long totalRead = 0;
    int read;

    while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
    {
      await destination.WriteAsync(buffer.AsMemory(0, read));
      totalRead += read;

      if (contentLength.HasValue && contentLength.Value > 0)
        progress?.Report(Math.Min(1.0, (double)totalRead / contentLength.Value));
    }

    if (!contentLength.HasValue || contentLength.Value == 0)
      progress?.Report(double.NaN);
    else
      progress?.Report(1.0);
  }
}