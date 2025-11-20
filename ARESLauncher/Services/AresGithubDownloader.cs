using ARESLauncher.Models;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ARESLauncher.Services;

public partial class AresGithubDownloader(ILogger<AresGithubDownloader> _logger) : IAresDownloader
{
  private static readonly ApiOptions _fetchOptions = new() { PageCount = 2, PageSize = 10 };

  public async Task<SemanticVersion[]> GetAvailableVersions(AresSource source, string? authToken)
  {
    var client = CreateClient(authToken);
    var versions = new List<SemanticVersion>();
    try
    {
      var releases = await client.Repository.Release.GetAll(source.Owner, source.Repo, _fetchOptions);

      foreach(var release in releases)
      {
        var normalizedTag = TagToVersion(release.TagName);
        if(string.IsNullOrEmpty(normalizedTag))
          continue;

        if(SemanticVersion.TryParse(normalizedTag, out var semanticVersion)) versions.Add(semanticVersion);
      }
    }
    catch(NotFoundException)
    {
      _logger.LogError(
        "ARES repository not found for {SourceOwner}/{SourceRepo}. Maybe you're missing the git auth token?",
        source.Owner, source.Repo);
    }

    return versions.ToArray();
  }

  public async Task<string> Download(AresSource source, SemanticVersion version, AresComponent component,
    string destination, string? authToken, IProgress<double>? progress = null)
  {
    var client = CreateClient(authToken);
    var release = await GetReleaseForVersion(client, source, version);
    var asset = SelectAssetForComponent(release, component) ??
                throw new InvalidOperationException(
                  $"No asset found in release {release.TagName} for component {component}.");

    var downloadUri = new Uri(asset.Url);
    var downloadResult = await Downloader.Download(downloadUri, destination, authToken, progress);

    // Technically ResultingFilePath could be null, but if our download result is a success, there's no reason it should.
    return !downloadResult.Success
      ? throw new InvalidOperationException($"Failed to download {component} {version}: {downloadResult.Error}")
      : downloadResult.ResultingFilePath!;
  }

  private static GitHubClient CreateClient(string? authtoken)
  {
    var client = new GitHubClient(new ProductHeaderValue("ares-launcher"));

    if(!string.IsNullOrEmpty(authtoken))
      client.Credentials = new Credentials(authtoken);

    return client;
  }

  private static async Task<Release> GetReleaseForVersion(GitHubClient client, AresSource source,
    SemanticVersion version)
  {
    var releases = await client.Repository.Release.GetAll(source.Owner, source.Repo, _fetchOptions);
    foreach(var release in releases)
    {
      var tag = release.TagName;
      var versionString = TagToVersion(tag);
      var isVersion = SemanticVersion.TryParse(versionString ?? "", out var parsedVersion);
      if(isVersion && version.Equals(parsedVersion))
      {
        return release;
      }
    }

    throw new InvalidOperationException($"Could not locate release for version {version}.");
  }

  private static ReleaseAsset? SelectAssetForComponent(Release release, AresComponent component)
  {
    if(release.Assets is null || release.Assets.Count == 0)
      return null;

    var os = OsBundleNameGetter.GetName();

    var keywords = component switch
    {
      AresComponent.Ui => ["ui", os],
      AresComponent.Service => ["service", os],
      AresComponent.Both => [os],
      _ => Array.Empty<string>()
    };

    var asset = release.Assets.FirstOrDefault(a =>
      keywords.All(keyword => a.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true));

    if(asset is not null)
      return asset;

    return release.Assets.Count == 1 ? release.Assets[0] : null;
  }

  private static string? TagToVersion(string? tag)
  {
    if(string.IsNullOrWhiteSpace(tag))
      return null;

    var trimmed = tag.Trim();
    var versionMatch = VersionRegex().Match(trimmed);
    var version = versionMatch.Groups.Values.ElementAtOrDefault(1)?.Value;

    return version;
  }

  [GeneratedRegex(".*[v](.*)")]
  private static partial Regex VersionRegex();
}