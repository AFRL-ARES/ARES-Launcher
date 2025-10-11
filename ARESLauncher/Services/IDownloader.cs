using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ARESLauncher.Models;

namespace ARESLauncher.Services;

public interface IDownloader
{
  Task<DownloadResult> Download(Uri source, Uri destination);
}