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
    var versions = await FetchAndNormalizeVersions(client, source.Owner, source.Repo);
    return versions.ToArray();
  }

  public async Task<SemanticVersion[]> GetAvailableVersions(LauncherSource source)
  {
    var client = CreateClient("");
    var versions = await FetchAndNormalizeVersions(client, source.Owner, source.Repo);
    return versions.ToArray();
  }

  public async Task<string> Download(LauncherSource source, SemanticVersion version, string destination, string? authToken,
    IProgress<double>? progress = null)
  {
    var client = CreateClient(authToken);
    var release = await GetReleaseForVersion(client, source, version);
    var asset = SelectAssetForLauncher(release) ??
                throw new InvalidOperationException(
                  $"No launcher asset found in release {release.TagName} for {OsBundleNameGetter.GetName()}.");

    var downloadUri = new Uri(asset.Url);
    var downloadResult = await Downloader.Download(downloadUri, destination, authToken, progress);

    return !downloadResult.Success
      ? throw new InvalidOperationException($"Failed to download launcher {version}: {downloadResult.Error}")
      : downloadResult.ResultingFilePath!;
  }

  private async Task<List<SemanticVersion>> FetchAndNormalizeVersions(GitHubClient client, string owner, string repo)
  {
    var versions = new List<SemanticVersion>();

    try
    {
      var releases = await client.Repository.Release.GetAll(owner, repo, _fetchOptions);

      foreach(var release in releases)
      {
        var normalizedTag = TagToVersion(release.TagName);
        if(string.IsNullOrEmpty(normalizedTag))
          continue;

        if(SemanticVersion.TryParse(normalizedTag, out var semanticVersion))
          versions.Add(semanticVersion);
      }
    }

    catch(NotFoundException)
    {
      _logger.LogError(
        "Repository not found for {SourceOwner}/{SourceRepo}. Maybe you're missing the git auth token?",
        owner, repo);
    }
    catch(Exception e)
    {
      _logger.LogError("Failed to fetch releases for {SourceOwner}/{SourceRepo}. {Exception}", owner, repo, e);
    }

    return versions;
  }

  public async Task<string> Download(AresSource source, SemanticVersion version,
    string destination, string? authToken, IProgress<double>? progress = null)
  {
    var client = CreateClient(authToken);
    var release = await GetReleaseForVersion(client, source, version);
    var asset = SelectAssetForAresRelease(release) ??
                throw new InvalidOperationException(
                  $"No ARES asset found in release {release.TagName} for {OsBundleNameGetter.GetName()}.");

    var downloadUri = new Uri(asset.Url);
    var downloadResult = await Downloader.Download(downloadUri, destination, authToken, progress);

    // Technically ResultingFilePath could be null, but if our download result is a success, there's no reason it should.
    return !downloadResult.Success
      ? throw new InvalidOperationException($"Failed to download ARES {version}: {downloadResult.Error}")
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

  private static async Task<Release> GetReleaseForVersion(GitHubClient client, LauncherSource source,
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

    throw new InvalidOperationException($"Could not locate launcher release for version {version}.");
  }

  private static ReleaseAsset? SelectAssetForAresRelease(Release release)
  {
    if(release.Assets is null || release.Assets.Count == 0)
      return null;

    var os = OsBundleNameGetter.GetName();
    var candidateAssets = release.Assets
      .Where(a => a.Name?.Contains(os, StringComparison.OrdinalIgnoreCase) is true)
      .ToArray();

    if(candidateAssets.Length == 0)
      candidateAssets = release.Assets.ToArray();

    var preferred = candidateAssets.FirstOrDefault(a =>
      a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) is true &&
      !a.Name!.Contains("offline", StringComparison.OrdinalIgnoreCase));

    if(preferred is not null)
      return preferred;

    preferred = candidateAssets.FirstOrDefault(a =>
      a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) is true);

    if(preferred is not null)
      return preferred;

    return candidateAssets.Length == 1 ? candidateAssets[0] : null;
  }

  private static ReleaseAsset? SelectAssetForLauncher(Release release)
  {
    if(release.Assets is null || release.Assets.Count == 0)
      return null;

    var os = OsBundleNameGetter.GetName();
    var candidateAssets = release.Assets
      .Where(a => a.Name?.Contains(os, StringComparison.OrdinalIgnoreCase) is true)
      .ToArray();

    if(candidateAssets.Length == 0)
      candidateAssets = release.Assets.ToArray();

    var preferred = candidateAssets.FirstOrDefault(a =>
      a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) is true &&
      !a.Name!.Contains("offline", StringComparison.OrdinalIgnoreCase));

    if(preferred is not null)
      return preferred;

    preferred = candidateAssets.FirstOrDefault(a =>
      a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) is true);

    if(preferred is not null)
      return preferred;

    return candidateAssets.Length == 1 ? candidateAssets[0] : null;
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

  [GeneratedRegex(".*[vV](.*)")]
  private static partial Regex VersionRegex();
}
