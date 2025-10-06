using System;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public class GithubDownloader : IAresDownloader
{
  public Task Download(Uri uri)
  {
    return Task.FromResult(true);
  }
}